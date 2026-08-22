using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace EyeMoT
{
    /// <summary>
    /// macOS向け外部マイク録音。
    ///
    /// 重要:
    /// Microphone.Start()直後にはAudioClip/recording cursorが
    /// まだ初期化されていない場合があるため、
    /// Update()上でMicrophone.GetPosition()が進むまで待ってから
    /// 実際の読み取り/Writerを開始する。
    ///
    /// AudioSourceは使わず、Microphoneが書き込むリングAudioClipを
    /// Main Threadで直接読み、Writer Threadへ渡す。
    /// </summary>
    public class GameMicRecorder : MonoBehaviour
    {
        [Header("Microphone Buffer")]
        [Tooltip("Microphone.Startが使用するリングバッファ秒数。")]
        [SerializeField]
        [Range(3, 30)]
        private int _ringBufferSeconds = 10;

        [Tooltip("録音開始後、この時間分の実データが溜まってから読み始めます。")]
        [SerializeField]
        [Range(50, 1000)]
        private int _startupBufferMs = 250;

        [Tooltip("通常時にwrite head直前を読まないための安全マージン(ms)。")]
        [SerializeField]
        [Range(20, 500)]
        private int _readSafetyMarginMs = 100;

        [Tooltip("1回でWriter Threadへ渡す音声長(ms)。")]
        [SerializeField]
        [Range(10, 200)]
        private int _chunkMilliseconds = 50;

        [Header("Writer Buffer")]
        [SerializeField]
        [Min(16)]
        private int _bufferPoolCount = 128;

        [Header("Debug")]
        [SerializeField]
        private bool _debugLog = true;

        [Tooltip("Microphone.Start後、この秒数GetPositionが0のままならエラーにします。")]
        [SerializeField]
        [Range(1f, 10f)]
        private float _startTimeoutSeconds = 3f;

        private class AudioBuffer
        {
            public float[] Samples;
            public int SampleCount;
        }

        private ConcurrentQueue<AudioBuffer> _pendingBuffers;
        private ConcurrentQueue<AudioBuffer> _freeBuffers;
        private AutoResetEvent _dataAvailableEvent;
        private Thread _writerThread;

        private volatile bool _isRecording;
        private volatile bool _writerInitialized;
        private volatile bool _stopRequested;

        private AudioClip _microphoneClip;
        private string _deviceName;
        private string _wavPath;

        private int _requestedSampleRate;
        private int _sampleRate;
        private int _channels;

        private int _ringFrames;
        private int _chunkFrames;
        private int _safetyFrames;
        private int _startupFrames;
        private int _readPositionFrames;

        private bool _readCursorInitialized;
        private bool _startTimeoutLogged;
        private float _recordingRequestedAt;
        private float _nextDebugLogTime;

        private long _dataBytesWritten;
        private long _capturedSampleCount;
        private int _droppedBufferCount;

        private float _peakAmplitude;
        private double _squareSum;
        private long _levelSampleCount;

        // 「現在のwrite head直前」を直接読んだProbe値。
        private float _probePeak;
        private float _probeRms;

        private Exception _writerException;

        public bool IsRecording => _isRecording;
        public string WavPath => _wavPath;
        public int DroppedBufferCount => _droppedBufferCount;
        public float PeakAmplitude => _peakAmplitude;
        public float ProbePeak => _probePeak;
        public float ProbeRms => _probeRms;
        public long CapturedSampleCount => _capturedSampleCount;

        public float RmsAmplitude
        {
            get
            {
                if (_levelSampleCount <= 0)
                    return 0f;

                return Mathf.Sqrt(
                    (float)(_squareSum / _levelSampleCount)
                );
            }
        }

        public static string[] GetDevices()
        {
            return Microphone.devices ?? Array.Empty<string>();
        }

        public bool StartRecording(
            string wavPath,
            string deviceName)
        {
            if (_isRecording)
            {
                Debug.LogWarning(
                    "<color=orange>[GameMicRecorder]</color> 既に録音中です。"
                );
                return false;
            }

            if (!isActiveAndEnabled)
            {
                Debug.LogError(
                    "<color=orange>[GameMicRecorder]</color> GameMicRecorderが非アクティブです。"
                );
                return false;
            }

            if (string.IsNullOrWhiteSpace(wavPath))
            {
                Debug.LogError(
                    "<color=orange>[GameMicRecorder]</color> WAVパスが指定されていません。"
                );
                return false;
            }

            string[] devices = Microphone.devices;

            if (devices == null || devices.Length == 0)
            {
                Debug.LogError(
                    "<color=orange>[GameMicRecorder]</color> Unityから利用可能なマイクが見つかりません。"
                );
                return false;
            }

            if (string.IsNullOrWhiteSpace(deviceName))
            {
                deviceName = devices[0];
            }

            bool found = false;

            foreach (string device in devices)
            {
                if (device == deviceName)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Debug.LogError(
                    "<color=orange>[GameMicRecorder]</color> " +
                    $"指定されたマイクが見つかりません: {deviceName}\n" +
                    $"Available:\n{string.Join("\n", devices)}"
                );
                return false;
            }

            try
            {
                string directory =
                    Path.GetDirectoryName(wavPath);

                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                _wavPath = wavPath;
                _deviceName = deviceName;

                _requestedSampleRate =
                    AudioSettings.outputSampleRate;

                if (_requestedSampleRate <= 0)
                {
                    _requestedSampleRate = 48000;
                }

                Microphone.GetDeviceCaps(
                    _deviceName,
                    out int minFrequency,
                    out int maxFrequency
                );

                if (!(minFrequency == 0 && maxFrequency == 0))
                {
                    int min =
                        minFrequency > 0
                            ? minFrequency
                            : _requestedSampleRate;

                    int max =
                        maxFrequency > 0
                            ? maxFrequency
                            : _requestedSampleRate;

                    _requestedSampleRate =
                        Mathf.Clamp(
                            _requestedSampleRate,
                            min,
                            max
                        );
                }

                ResetRuntimeState();

                /*
                 * ここではMicrophone.Startするだけ。
                 * clip.channels / frequency / samples は直後に信用しない。
                 */
                _microphoneClip =
                    Microphone.Start(
                        _deviceName,
                        true,
                        Mathf.Max(3, _ringBufferSeconds),
                        _requestedSampleRate
                    );

                if (_microphoneClip == null)
                {
                    Debug.LogError(
                        "<color=orange>[GameMicRecorder]</color> " +
                        $"Microphone.Start()に失敗しました: {_deviceName}"
                    );
                    return false;
                }

                _recordingRequestedAt =
                    Time.realtimeSinceStartup;

                _nextDebugLogTime =
                    Time.realtimeSinceStartup + 0.5f;

                _isRecording = true;

                Debug.Log(
                    "<color=orange>[GameMicRecorder]</color> Microphone.Start要求\n" +
                    $"Device: {_deviceName}\n" +
                    $"Path: {_wavPath}\n" +
                    $"Requested Rate: {_requestedSampleRate}\n" +
                    $"HasAuthorization: {Application.HasUserAuthorization(UserAuthorization.Microphone)}"
                );

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(
                    "<color=orange>[GameMicRecorder]</color> " +
                    $"録音開始失敗: {e.Message}"
                );

                CleanupMicrophone();
                _isRecording = false;
                return false;
            }
        }

        private void ResetRuntimeState()
        {
            _writerInitialized = false;
            _stopRequested = false;
            _readCursorInitialized = false;
            _startTimeoutLogged = false;

            _sampleRate = 0;
            _channels = 0;
            _ringFrames = 0;
            _chunkFrames = 0;
            _safetyFrames = 0;
            _startupFrames = 0;
            _readPositionFrames = 0;

            _dataBytesWritten = 0;
            _capturedSampleCount = 0;
            _droppedBufferCount = 0;

            _peakAmplitude = 0f;
            _squareSum = 0.0;
            _levelSampleCount = 0;

            _probePeak = 0f;
            _probeRms = 0f;

            _writerException = null;
        }

        private void Update()
        {
            if (!_isRecording ||
                _microphoneClip == null)
            {
                return;
            }

            int writePosition =
                Microphone.GetPosition(
                    _deviceName
                );

            bool micIsRecording =
                Microphone.IsRecording(
                    _deviceName
                );

            /*
             * Microphone.Start直後はGetPosition==0でも正常。
             * Cursorが進むまで絶対にAudioClipを読まない。
             */
            if (!_writerInitialized)
            {
                if (writePosition <= 0)
                {
                    LogStartingStatus(
                        writePosition,
                        micIsRecording
                    );

                    if (!_startTimeoutLogged &&
                        Time.realtimeSinceStartup -
                        _recordingRequestedAt >=
                        _startTimeoutSeconds)
                    {
                        _startTimeoutLogged = true;

                        Debug.LogError(
                            "<color=orange>[GameMicRecorder]</color> " +
                            "Microphone.GetPosition() が0のまま進みません。\n" +
                            $"Device={_deviceName}\n" +
                            $"Microphone.IsRecording={micIsRecording}\n" +
                            $"Authorization={Application.HasUserAuthorization(UserAuthorization.Microphone)}"
                        );
                    }

                    return;
                }

                /*
                 * Startから最低1frame以上経過し、
                 * recording cursorが進んだ後で初めてAudioClip情報を確定する。
                 */
                if (!InitializeWriterAfterMicrophoneStarted())
                {
                    return;
                }
            }

            /*
             * まずwrite head直前のサンプルを直接Probeする。
             * これが0ならリング管理とは無関係にMicrophone AudioClip自体が無音。
             */
            ProbeLatestSamples(
                writePosition
            );

            /*
             * 初回はstartupBuffer分たまるまで待つ。
             *
             * 前版のバグ:
             * writePosition < safetyFrames の時に負値をリング末尾へwrapし、
             * まだ一度も書かれていない0領域を「大量の有効データ」と誤認していた。
             */
            if (!_readCursorInitialized)
            {
                if (writePosition <
                    _startupFrames)
                {
                    LogLiveStatus(
                        writePosition,
                        micIsRecording,
                        "Buffering"
                    );
                    return;
                }

                /*
                 * 録音開始地点0から読む。
                 * この時点で0..writePositionまでは確実にMicrophoneが書き込み済み。
                 */
                _readPositionFrames = 0;
                _readCursorInitialized = true;

                Debug.Log(
                    "<color=orange>[GameMicRecorder]</color> " +
                    $"実データ読み取り開始 / Device={_deviceName}, MicPos={writePosition}"
                );
            }

            int safeEndPosition;

            /*
             * 最初のリング1周まではwritePosition-safetyが負になった時に
             * wrapしてはいけない。
             * ただしreadCursorInitialized時点ではstartupFrames>safetyFramesなので通常正値。
             */
            if (writePosition >=
                _safetyFrames)
            {
                safeEndPosition =
                    writePosition -
                    _safetyFrames;
            }
            else
            {
                /*
                 * リングが一周した後のみ意味があるwrap。
                 * _readPositionFramesがwritePositionより大きい = wrap済みの可能性が高い。
                 */
                if (_readPositionFrames >
                    writePosition)
                {
                    safeEndPosition =
                        _ringFrames +
                        writePosition -
                        _safetyFrames;
                }
                else
                {
                    safeEndPosition = 0;
                }
            }

            int availableFrames =
                RingDistance(
                    _readPositionFrames,
                    safeEndPosition,
                    _ringFrames
                );

            while (availableFrames >=
                   _chunkFrames)
            {
                if (!_freeBuffers.TryDequeue(
                        out AudioBuffer buffer))
                {
                    _droppedBufferCount++;

                    AdvanceReadPosition(
                        _chunkFrames
                    );

                    availableFrames -=
                        _chunkFrames;

                    continue;
                }

                if (!ReadFrames(
                        _readPositionFrames,
                        _chunkFrames,
                        buffer.Samples))
                {
                    _freeBuffers.Enqueue(
                        buffer
                    );

                    _droppedBufferCount++;

                    AdvanceReadPosition(
                        _chunkFrames
                    );

                    availableFrames -=
                        _chunkFrames;

                    continue;
                }

                int sampleCount =
                    _chunkFrames *
                    _channels;

                UpdateLevels(
                    buffer.Samples,
                    sampleCount
                );

                buffer.SampleCount =
                    sampleCount;

                _pendingBuffers.Enqueue(
                    buffer
                );

                _capturedSampleCount +=
                    sampleCount;

                _dataAvailableEvent?.Set();

                AdvanceReadPosition(
                    _chunkFrames
                );

                availableFrames -=
                    _chunkFrames;
            }

            LogLiveStatus(
                writePosition,
                micIsRecording,
                "Recording"
            );
        }

        private bool InitializeWriterAfterMicrophoneStarted()
        {
            /*
             * Microphone.Start直後ではなくcursorが進んだ後なので
             * この時点ではclip metadataが初期化済みである可能性が高い。
             */
            _sampleRate =
                _microphoneClip.frequency;

            _channels =
                _microphoneClip.channels;

            _ringFrames =
                _microphoneClip.samples;

            if (_sampleRate <= 0 ||
                _channels <= 0 ||
                _ringFrames <= 0)
            {
                Debug.LogWarning(
                    "<color=orange>[GameMicRecorder]</color> " +
                    $"Microphone Clip metadata待機中: " +
                    $"Rate={_sampleRate}, Channels={_channels}, Samples={_ringFrames}"
                );
                return false;
            }

            _chunkFrames =
                Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        _sampleRate *
                        (_chunkMilliseconds / 1000f)
                    )
                );

            _safetyFrames =
                Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        _sampleRate *
                        (_readSafetyMarginMs / 1000f)
                    )
                );

            _startupFrames =
                Mathf.Max(
                    _safetyFrames + 1,
                    Mathf.RoundToInt(
                        _sampleRate *
                        (_startupBufferMs / 1000f)
                    )
                );

            /*
             * startup bufferはリングの半分未満に制限。
             */
            _startupFrames =
                Mathf.Min(
                    _startupFrames,
                    Mathf.Max(
                        1,
                        _ringFrames / 2
                    )
                );

            int samplesPerBuffer =
                _chunkFrames *
                _channels;

            _pendingBuffers =
                new ConcurrentQueue<AudioBuffer>();

            _freeBuffers =
                new ConcurrentQueue<AudioBuffer>();

            for (int i = 0;
                 i < _bufferPoolCount;
                 i++)
            {
                _freeBuffers.Enqueue(
                    new AudioBuffer
                    {
                        Samples =
                            new float[
                                samplesPerBuffer
                            ]
                    }
                );
            }

            _dataAvailableEvent =
                new AutoResetEvent(false);

            _writerThread =
                new Thread(WriterLoop)
                {
                    IsBackground = true,
                    Name = "GameMicRecorder_Writer"
                };

            _writerThread.Start();

            _writerInitialized = true;

            Debug.Log(
                "<color=orange>[GameMicRecorder]</color> Microphone初期化完了\n" +
                $"Device: {_deviceName}\n" +
                $"SampleRate: {_sampleRate}\n" +
                $"Channels: {_channels}\n" +
                $"RingFrames: {_ringFrames}\n" +
                $"StartupFrames: {_startupFrames}\n" +
                $"SafetyFrames: {_safetyFrames}"
            );

            return true;
        }

        private void ProbeLatestSamples(
            int writePosition)
        {
            if (!_writerInitialized ||
                writePosition <= 0)
            {
                return;
            }

            /*
             * 最新約20msを直接読む。
             * 録音ロジックとは独立した検査。
             */
            int probeFrames =
                Mathf.Max(
                    64,
                    _sampleRate / 50
                );

            probeFrames =
                Mathf.Min(
                    probeFrames,
                    Mathf.Max(
                        1,
                        _ringFrames / 4
                    )
                );

            if (writePosition <
                probeFrames)
            {
                return;
            }

            int startFrame =
                writePosition -
                probeFrames;

            float[] probe =
                new float[
                    probeFrames *
                    _channels
                ];

            if (!_microphoneClip.GetData(
                    probe,
                    startFrame))
            {
                return;
            }

            float peak = 0f;
            double squareSum = 0.0;

            for (int i = 0;
                 i < probe.Length;
                 i++)
            {
                float sample =
                    probe[i];

                float abs =
                    Mathf.Abs(sample);

                if (abs > peak)
                {
                    peak = abs;
                }

                squareSum +=
                    sample *
                    sample;
            }

            _probePeak = peak;

            _probeRms =
                probe.Length > 0
                    ? Mathf.Sqrt(
                        (float)(
                            squareSum /
                            probe.Length
                        )
                    )
                    : 0f;
        }

        private void LogStartingStatus(
            int writePosition,
            bool micIsRecording)
        {
            if (!_debugLog)
                return;

            if (Time.realtimeSinceStartup <
                _nextDebugLogTime)
            {
                return;
            }

            _nextDebugLogTime =
                Time.realtimeSinceStartup +
                0.5f;

            Debug.Log(
                "<color=orange>[GameMicRecorder]</color> STARTING " +
                $"Device={_deviceName} " +
                $"IsRecording={micIsRecording} " +
                $"MicPos={writePosition}"
            );
        }

        private void LogLiveStatus(
            int writePosition,
            bool micIsRecording,
            string state)
        {
            if (!_debugLog)
                return;

            if (Time.realtimeSinceStartup <
                _nextDebugLogTime)
            {
                return;
            }

            _nextDebugLogTime =
                Time.realtimeSinceStartup +
                1f;

            Debug.Log(
                "<color=orange>[GameMicRecorder]</color> " +
                $"{state} " +
                $"Device={_deviceName} " +
                $"IsRecording={micIsRecording} " +
                $"MicPos={writePosition} " +
                $"ProbePeak={_probePeak:F6} " +
                $"ProbeRMS={_probeRms:F6} " +
                $"RecordedPeak={_peakAmplitude:F6} " +
                $"Samples={_capturedSampleCount}"
            );
        }

        private bool ReadFrames(
            int startFrame,
            int frameCount,
            float[] destination)
        {
            if (_microphoneClip == null)
                return false;

            int firstFrames =
                Mathf.Min(
                    frameCount,
                    _ringFrames -
                    startFrame
                );

            int secondFrames =
                frameCount -
                firstFrames;

            if (secondFrames == 0)
            {
                return _microphoneClip.GetData(
                    destination,
                    startFrame
                );
            }

            int firstSamples =
                firstFrames *
                _channels;

            int secondSamples =
                secondFrames *
                _channels;

            float[] first =
                new float[
                    firstSamples
                ];

            float[] second =
                new float[
                    secondSamples
                ];

            bool firstOk =
                _microphoneClip.GetData(
                    first,
                    startFrame
                );

            bool secondOk =
                _microphoneClip.GetData(
                    second,
                    0
                );

            if (!firstOk ||
                !secondOk)
            {
                return false;
            }

            Array.Copy(
                first,
                0,
                destination,
                0,
                firstSamples
            );

            Array.Copy(
                second,
                0,
                destination,
                firstSamples,
                secondSamples
            );

            return true;
        }

        private void UpdateLevels(
            float[] samples,
            int sampleCount)
        {
            float localPeak = 0f;
            double localSquareSum = 0.0;

            for (int i = 0;
                 i < sampleCount;
                 i++)
            {
                float sample =
                    samples[i];

                float abs =
                    Mathf.Abs(sample);

                if (abs > localPeak)
                {
                    localPeak = abs;
                }

                localSquareSum +=
                    sample *
                    sample;
            }

            if (localPeak >
                _peakAmplitude)
            {
                _peakAmplitude =
                    localPeak;
            }

            _squareSum +=
                localSquareSum;

            _levelSampleCount +=
                sampleCount;
        }

        public void StopRecording()
        {
            if (!_isRecording)
                return;

            /*
             * Microphone.End前に最終cursorまで回収。
             */
            if (_writerInitialized &&
                _readCursorInitialized)
            {
                int finalWritePosition =
                    Microphone.GetPosition(
                        _deviceName
                    );

                if (finalWritePosition >= 0)
                {
                    FlushUntil(
                        finalWritePosition
                    );
                }
            }

            _isRecording = false;

            CleanupMicrophone();

            if (_writerInitialized)
            {
                _stopRequested = true;
                _dataAvailableEvent?.Set();

                if (_writerThread != null &&
                    _writerThread.IsAlive)
                {
                    _writerThread.Join();
                }

                _writerThread = null;

                _dataAvailableEvent?.Dispose();
                _dataAvailableEvent = null;
            }

            if (_writerException != null)
            {
                Debug.LogError(
                    "<color=orange>[GameMicRecorder]</color> " +
                    $"WAV書き込みエラー: {_writerException.Message}"
                );
            }

            Debug.Log(
                "<color=orange>[GameMicRecorder]</color> 録音終了\n" +
                $"Device: {_deviceName}\n" +
                $"WriterInitialized: {_writerInitialized}\n" +
                $"Captured Samples: {_capturedSampleCount}\n" +
                $"ProbePeak: {_probePeak:F6}\n" +
                $"ProbeRMS: {_probeRms:F6}\n" +
                $"RecordedPeak: {_peakAmplitude:F6}\n" +
                $"RecordedRMS: {RmsAmplitude:F6}\n" +
                $"Data Bytes: {_dataBytesWritten}\n" +
                $"Dropped Buffers: {_droppedBufferCount}"
            );

            /*
             * Probeも0なら「WAV化のバグ」ではなく
             * Microphone AudioClipそのものが無音。
             */
            if (_probePeak < 0.00001f)
            {
                Debug.LogError(
                    "<color=orange>[GameMicRecorder]</color> " +
                    "Microphone AudioClip自体から有効な波形を取得できていません。 " +
                    $"Device={_deviceName}, ProbePeak={_probePeak:F6}"
                );
            }
            else if (_peakAmplitude < 0.00001f)
            {
                Debug.LogError(
                    "<color=orange>[GameMicRecorder]</color> " +
                    "Microphone AudioClipには波形がありますが、WAV録音経路へ渡せていません。 " +
                    $"ProbePeak={_probePeak:F6}, RecordedPeak={_peakAmplitude:F6}"
                );
            }
        }

        private void FlushUntil(
            int endPosition)
        {
            if (!_writerInitialized)
                return;

            int remainingFrames =
                RingDistance(
                    _readPositionFrames,
                    endPosition,
                    _ringFrames
                );

            while (remainingFrames > 0)
            {
                int frames =
                    Mathf.Min(
                        _chunkFrames,
                        remainingFrames
                    );

                int sampleCount =
                    frames *
                    _channels;

                AudioBuffer buffer;

                if (frames ==
                    _chunkFrames &&
                    _freeBuffers.TryDequeue(
                        out AudioBuffer pooled))
                {
                    buffer = pooled;
                }
                else
                {
                    buffer =
                        new AudioBuffer
                        {
                            Samples =
                                new float[
                                    sampleCount
                                ]
                        };
                }

                if (!ReadFrames(
                        _readPositionFrames,
                        frames,
                        buffer.Samples))
                {
                    break;
                }

                UpdateLevels(
                    buffer.Samples,
                    sampleCount
                );

                buffer.SampleCount =
                    sampleCount;

                _pendingBuffers.Enqueue(
                    buffer
                );

                _capturedSampleCount +=
                    sampleCount;

                _dataAvailableEvent?.Set();

                AdvanceReadPosition(
                    frames
                );

                remainingFrames -=
                    frames;
            }
        }

        private void AdvanceReadPosition(
            int frames)
        {
            _readPositionFrames +=
                frames;

            if (_readPositionFrames >=
                _ringFrames)
            {
                _readPositionFrames %=
                    _ringFrames;
            }
        }

        private static int RingDistance(
            int start,
            int end,
            int length)
        {
            if (length <= 0)
                return 0;

            if (end >= start)
            {
                return end - start;
            }

            return
                (length - start) +
                end;
        }

        private void CleanupMicrophone()
        {
            try
            {
                if (!string.IsNullOrEmpty(
                        _deviceName) &&
                    Microphone.IsRecording(
                        _deviceName))
                {
                    Microphone.End(
                        _deviceName
                    );
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    "<color=orange>[GameMicRecorder]</color> " +
                    $"Microphone.End error: {e.Message}"
                );
            }

            _microphoneClip = null;
        }

        private void WriterLoop()
        {
            try
            {
                using FileStream fileStream =
                    new FileStream(
                        _wavPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.Read
                    );

                using BinaryWriter writer =
                    new BinaryWriter(
                        fileStream
                    );

                WriteWavHeader(
                    writer,
                    _sampleRate,
                    _channels,
                    0
                );

                byte[] pcmBuffer =
                    new byte[
                        _chunkFrames *
                        _channels *
                        2
                    ];

                while (true)
                {
                    bool processed = false;

                    while (
                        _pendingBuffers.TryDequeue(
                            out AudioBuffer buffer
                        ))
                    {
                        processed = true;

                        int requiredBytes =
                            buffer.SampleCount *
                            2;

                        if (pcmBuffer.Length <
                            requiredBytes)
                        {
                            pcmBuffer =
                                new byte[
                                    requiredBytes
                                ];
                        }

                        int byteCount =
                            ConvertToPcm16(
                                buffer.Samples,
                                buffer.SampleCount,
                                pcmBuffer
                            );

                        writer.Write(
                            pcmBuffer,
                            0,
                            byteCount
                        );

                        _dataBytesWritten +=
                            byteCount;

                        if (buffer.Samples.Length ==
                            _chunkFrames *
                            _channels)
                        {
                            buffer.SampleCount = 0;

                            _freeBuffers.Enqueue(
                                buffer
                            );
                        }
                    }

                    if (_stopRequested &&
                        _pendingBuffers.IsEmpty)
                    {
                        break;
                    }

                    if (!processed)
                    {
                        _dataAvailableEvent
                            ?.WaitOne(10);
                    }
                }

                writer.Flush();

                fileStream.Seek(
                    0,
                    SeekOrigin.Begin
                );

                WriteWavHeader(
                    writer,
                    _sampleRate,
                    _channels,
                    _dataBytesWritten
                );

                writer.Flush();
            }
            catch (Exception e)
            {
                _writerException = e;
            }
        }

        private static int ConvertToPcm16(
            float[] input,
            int sampleCount,
            byte[] output)
        {
            int outputIndex = 0;

            for (int i = 0;
                 i < sampleCount;
                 i++)
            {
                float sample =
                    input[i];

                short pcm;

                if (sample >= 1f)
                {
                    pcm = short.MaxValue;
                }
                else if (sample <= -1f)
                {
                    pcm = short.MinValue;
                }
                else
                {
                    pcm =
                        (short)(
                            sample *
                            short.MaxValue
                        );
                }

                output[outputIndex++] =
                    (byte)(
                        pcm &
                        0xff
                    );

                output[outputIndex++] =
                    (byte)(
                        (pcm >> 8) &
                        0xff
                    );
            }

            return outputIndex;
        }

        private static void WriteWavHeader(
            BinaryWriter writer,
            int sampleRate,
            int channels,
            long dataSize)
        {
            const short bitsPerSample =
                16;

            int byteRate =
                sampleRate *
                channels *
                bitsPerSample /
                8;

            short blockAlign =
                (short)(
                    channels *
                    bitsPerSample /
                    8
                );

            writer.Write(
                Encoding.ASCII.GetBytes(
                    "RIFF"
                )
            );

            writer.Write(
                (int)(
                    36 +
                    dataSize
                )
            );

            writer.Write(
                Encoding.ASCII.GetBytes(
                    "WAVE"
                )
            );

            writer.Write(
                Encoding.ASCII.GetBytes(
                    "fmt "
                )
            );

            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);

            writer.Write(
                Encoding.ASCII.GetBytes(
                    "data"
                )
            );

            writer.Write(
                (int)dataSize
            );
        }

        private void OnDestroy()
        {
            if (_isRecording)
            {
                StopRecording();
            }
        }
    }
}
