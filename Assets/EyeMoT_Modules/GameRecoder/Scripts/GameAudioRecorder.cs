using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace EyeMoT
{
    [RequireComponent(typeof(AudioListener))]
    public class GameAudioRecorder : MonoBehaviour
    {
        [Header("Buffer Settings")]
        [SerializeField]
        [Min(8)]
        private int _bufferPoolCount = 128;

        /// <summary>
        /// 7.1chまで想定。
        /// 通常のStereoなら2ch。
        /// </summary>
        [SerializeField]
        [Range(1, 8)]
        private int _maxChannels = 8;

        [Header("Debug")]
        [SerializeField]
        private bool _debugLog = false;


        private class AudioBuffer
        {
            public float[] Samples;
            public int SampleCount;
            public int Channels;
        }


        // AudioThread → WriterThread
        private ConcurrentQueue<AudioBuffer> _pendingBuffers;

        // WriterThread → AudioThread
        private ConcurrentQueue<AudioBuffer> _freeBuffers;

        private AutoResetEvent _dataAvailableEvent;

        private Thread _writerThread;

        private volatile bool _isRecording;
        private volatile bool _stopRequested;

        private int _activeAudioCallbacks;

        private string _wavPath;
        private int _sampleRate;
        private int _maxSampleCount;

        private int _recordedChannels;

        private long _dataBytesWritten;

        private int _droppedBufferCount;

        private Exception _writerException;


        public bool IsRecording => _isRecording;

        public string WavPath => _wavPath;

        public int DroppedBufferCount => _droppedBufferCount;


        /// <summary>
        /// 録音開始
        /// GameRecoder.RecordStart() から呼び出す。
        /// </summary>
        public bool StartRecording(string wavPath)
        {
            if (_isRecording)
            {
                Debug.LogWarning(
                    "<color=orange>[GameAudioRecorder]</color> 既に録音中です。"
                );

                return false;
            }

            if (string.IsNullOrEmpty(wavPath))
            {
                Debug.LogError(
                    "<color=orange>[GameAudioRecorder]</color> WAVパスが指定されていません。"
                );

                return false;
            }

            try
            {
                string directory = Path.GetDirectoryName(wavPath);

                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }


                _wavPath = wavPath;

                _sampleRate = AudioSettings.outputSampleRate;

                AudioSettings.GetDSPBufferSize(
                    out int dspBufferLength,
                    out _
                );


                /*
                 * OnAudioFilterRead の data.Length は
                 *
                 * DSP Buffer Size × Channel数
                 *
                 * になるので、最大Channel数分確保しておく。
                 */
                _maxSampleCount =
                    dspBufferLength * _maxChannels;


                _pendingBuffers =
                    new ConcurrentQueue<AudioBuffer>();

                _freeBuffers =
                    new ConcurrentQueue<AudioBuffer>();


                /*
                 * AudioThread内で new float[] しないよう
                 * 録音開始時にまとめて確保。
                 */
                for (int i = 0; i < _bufferPoolCount; i++)
                {
                    AudioBuffer buffer = new AudioBuffer
                    {
                        Samples = new float[_maxSampleCount]
                    };

                    _freeBuffers.Enqueue(buffer);
                }


                _dataAvailableEvent =
                    new AutoResetEvent(false);


                _recordedChannels = 0;
                _dataBytesWritten = 0;
                _droppedBufferCount = 0;

                _writerException = null;

                _stopRequested = false;


                /*
                 * WAV書き込み専用Thread
                 */
                _writerThread = new Thread(WriterLoop)
                {
                    IsBackground = true,
                    Name = "GameAudioRecorder_Writer"
                };


                _isRecording = true;

                _writerThread.Start();


                if (_debugLog)
                {
                    Debug.Log(
                        $"<color=orange>[GameAudioRecorder]</color> " +
                        $"録音開始\n" +
                        $"Path: {_wavPath}\n" +
                        $"SampleRate: {_sampleRate}\n" +
                        $"DSP Buffer: {dspBufferLength}"
                    );
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"<color=orange>[GameAudioRecorder]</color> " +
                    $"録音開始失敗: {e.Message}"
                );

                _isRecording = false;

                return false;
            }
        }


        /// <summary>
        /// Audio Threadから呼ばれる。
        ///
        /// ここでは絶対に
        ///
        /// ・File.Write
        /// ・Debug.Log
        /// ・Unity API
        /// ・重いlock
        ///
        /// 等を行わない。
        /// </summary>
        private void OnAudioFilterRead(
            float[] data,
            int channels)
        {
            if (!_isRecording)
                return;


            /*
             * StopRecordingとの競合防止。
             */
            Interlocked.Increment(
                ref _activeAudioCallbacks
            );

            try
            {
                /*
                 * Increment直後に
                 * StopRecordingされた可能性があるので
                 * もう一度確認。
                 */
                if (!_isRecording)
                    return;


                if (!_freeBuffers.TryDequeue(
                        out AudioBuffer buffer))
                {
                    /*
                     * Writer Threadが追いついていない場合。
                     *
                     * AudioThreadを止める方が危険なので
                     * このBufferは破棄する。
                     */
                    Interlocked.Increment(
                        ref _droppedBufferCount
                    );

                    return;
                }


                if (data.Length >
                    buffer.Samples.Length)
                {
                    _freeBuffers.Enqueue(buffer);

                    Interlocked.Increment(
                        ref _droppedBufferCount
                    );

                    return;
                }


                /*
                 * AudioThreadで行う主処理。
                 *
                 * Unity側のAudioBuffer
                 * ↓
                 * 自前Buffer
                 */
                Array.Copy(
                    data,
                    buffer.Samples,
                    data.Length
                );


                buffer.SampleCount = data.Length;
                buffer.Channels = channels;


                _pendingBuffers.Enqueue(buffer);


                /*
                 * Writer Threadを起こす。
                 */
                _dataAvailableEvent?.Set();
            }
            finally
            {
                Interlocked.Decrement(
                    ref _activeAudioCallbacks
                );
            }
        }


        /// <summary>
        /// WAV書き込みThread
        /// </summary>
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
                    new BinaryWriter(fileStream);


                /*
                 * とりあえず空Header。
                 *
                 * 録音終了時に正しいサイズへ書き直す。
                 */
                WriteWavHeader(
                    writer,
                    _sampleRate,
                    2,
                    0
                );


                /*
                 * float
                 * ↓
                 * PCM 16bit
                 *
                 * 変換用Buffer。
                 *
                 * Writer Thread上なので
                 * newして問題なし。
                 */
                byte[] pcmBuffer =
                    new byte[
                        _maxSampleCount * 2
                    ];


                while (true)
                {
                    bool processed = false;


                    while (_pendingBuffers.TryDequeue(
                               out AudioBuffer audioBuffer))
                    {
                        processed = true;


                        if (_recordedChannels == 0)
                        {
                            _recordedChannels =
                                audioBuffer.Channels;
                        }


                        int byteCount =
                            ConvertToPcm16(
                                audioBuffer.Samples,
                                audioBuffer.SampleCount,
                                pcmBuffer
                            );


                        writer.Write(
                            pcmBuffer,
                            0,
                            byteCount
                        );


                        _dataBytesWritten +=
                            byteCount;


                        /*
                         * 使用済みBufferをPoolへ返す。
                         */
                        audioBuffer.SampleCount = 0;
                        audioBuffer.Channels = 0;

                        _freeBuffers.Enqueue(
                            audioBuffer
                        );
                    }


                    /*
                     * Stop要求済み
                     * ＆
                     * Queueが空
                     *
                     * になったら終了。
                     */
                    if (_stopRequested &&
                        _pendingBuffers.IsEmpty)
                    {
                        break;
                    }


                    if (!processed)
                    {
                        /*
                         * AudioThreadから
                         * dataが来るまで待機。
                         */
                        _dataAvailableEvent
                            ?.WaitOne(10);
                    }
                }


                /*
                 * Channel数が最後まで取れなかった場合
                 * Stereoとして扱う。
                 */
                int channels =
                    _recordedChannels > 0
                        ? _recordedChannels
                        : 2;


                /*
                 * WAV Headerを書き直す。
                 */
                writer.Flush();

                fileStream.Seek(
                    0,
                    SeekOrigin.Begin
                );


                WriteWavHeader(
                    writer,
                    _sampleRate,
                    channels,
                    _dataBytesWritten
                );


                writer.Flush();
            }
            catch (Exception e)
            {
                /*
                 * WorkerThreadなので
                 * ここでDebug.Logしない。
                 *
                 * Main Thread側で表示する。
                 */
                _writerException = e;
            }
        }


        /// <summary>
        /// 録音終了。
        ///
        /// WAV Headerが完成するまで待つので
        /// この関数が返った時点で
        /// FFmpegへ渡してOK。
        /// </summary>
        public void StopRecording()
        {
            if (!_isRecording)
                return;


            /*
             * まずAudioThreadからの
             * 新規データ受付を停止。
             */
            _isRecording = false;


            /*
             * 実行中のOnAudioFilterReadが
             * 完全に終了するまで待つ。
             */
            while (
                Interlocked.CompareExchange(
                    ref _activeAudioCallbacks,
                    0,
                    0
                ) > 0)
            {
                Thread.Sleep(1);
            }


            /*
             * Writer Threadへ終了要求。
             */
            _stopRequested = true;

            _dataAvailableEvent?.Set();


            /*
             * Queueの残りを書き込み
             * WAV Header完成まで待機。
             */
            if (_writerThread != null &&
                _writerThread.IsAlive)
            {
                _writerThread.Join();
            }


            _writerThread = null;


            _dataAvailableEvent?.Dispose();
            _dataAvailableEvent = null;


            if (_writerException != null)
            {
                Debug.LogError(
                    $"<color=orange>[GameAudioRecorder]</color> " +
                    $"WAV書き込みエラー: " +
                    $"{_writerException.Message}"
                );
            }
            else
            {
                if (_debugLog)
                {
                    Debug.Log(
                        $"<color=orange>[GameAudioRecorder]</color> " +
                        $"録音終了\n" +
                        $"Path: {_wavPath}\n" +
                        $"Channels: {_recordedChannels}\n" +
                        $"Dropped Buffers: {_droppedBufferCount}"
                    );
                }
            }
        }


        /// <summary>
        /// float (-1 ～ 1)
        /// ↓
        /// PCM signed 16bit little endian
        /// </summary>
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
                float sample = input[i];


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
                        pcm & 0xff
                    );

                output[outputIndex++] =
                    (byte)(
                        (pcm >> 8) &
                        0xff
                    );
            }


            return outputIndex;
        }


        /// <summary>
        /// PCM 16bit WAV Header
        /// </summary>
        private static void WriteWavHeader(
            BinaryWriter writer,
            int sampleRate,
            int channels,
            long dataSize)
        {
            const short bitsPerSample = 16;


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
                Encoding.ASCII.GetBytes("RIFF")
            );

            writer.Write(
                (int)(36 + dataSize)
            );

            writer.Write(
                Encoding.ASCII.GetBytes("WAVE")
            );


            writer.Write(
                Encoding.ASCII.GetBytes("fmt ")
            );

            writer.Write(16);

            // PCM
            writer.Write((short)1);

            writer.Write(
                (short)channels
            );

            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);


            writer.Write(
                Encoding.ASCII.GetBytes("data")
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