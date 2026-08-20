using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug; // System.Diagnostics.Debug と被るのでエイリアス

namespace EyeMoT
{
    public class GameRecoder : MonoBehaviour
    {
        public static GameRecoder Instance { get; private set; }

        [Header("Record Settings")]
        [SerializeField] private string _recorderFolderName = "YOUR_RECORD/GameRecord";
        [SerializeField] private string _ffmpegFolderName = "GameRecorder/ffmpeg.exe";
        [SerializeField] private string _outputPrefix = "GameRecoder_";

        [Header("Audio")]
        [Tooltip("AudioListener と同じ GameObject にアタッチした GameAudioRecorder を指定します。")]
        [SerializeField] private GameAudioRecorder _audioRecorder;

        [Header("Debug")]
        [SerializeField] private bool _receiveDebugLog = false;
        [SerializeField] private Canvas _recordStateCanvas;

        public bool CanRecord = true;

        private Process _ffmpegProcess;
        private bool _isRecording = false;

        // 録画1回ごとのパス
        private string _ffmpegPath;
        private string _finalOutputPath;
        private string _tempVideoPath;
        private string _tempAudioPath;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        /// <summary>
        /// 録画開始
        ///
        /// FFmpeg : 画面のみ -> temp_video.mp4
        /// Unity  : 音声のみ -> temp_audio.wav
        ///
        /// RecordEnd() で最終 MP4 に結合する。
        /// </summary>
        public void RecordStart(string dirName = "", string fileName = "")
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log("<color=orange>[GameRecoder]</color> WebGL platform does not support GameRecoder. Initialization skipped.");
            return;
#endif

            if (_isRecording)
            {
                Debug.LogWarning("<color=orange>[GameRecoder]</color> 既に録画中です。");
                return;
            }

            if (!CanRecord)
            {
                Debug.LogWarning("<color=orange>[GameRecoder]</color> 録画は無効化されています。");
                return;
            }

            if (_audioRecorder == null)
            {
                // Scene変更などで参照が切れている場合の保険。
                _audioRecorder = FindObjectOfType<GameAudioRecorder>();
            }

            if (_audioRecorder == null)
            {
                Debug.LogError(
                    "<color=orange>[GameRecoder]</color> GameAudioRecorder が見つかりません。" +
                    "AudioListener に GameAudioRecorder をアタッチし、GameRecoder の Audio Recorder に設定してください。"
                );
                return;
            }

            string exeFolder = GetExeFolderPath();
            string recordFolder = string.IsNullOrEmpty(dirName)
                ? Path.Combine(exeFolder, _recorderFolderName)
                : dirName;

            Directory.CreateDirectory(recordFolder);

            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string currentFileName = string.IsNullOrEmpty(fileName)
                ? $"{_outputPrefix}_{timestamp}.mp4"
                : $"{fileName}.mp4";

            _finalOutputPath = Path.Combine(recordFolder, currentFileName);

            // 同時録画や前回異常終了時の一時ファイルと衝突しないよう GUID を使用。
            string tempId = Guid.NewGuid().ToString("N");
            _tempVideoPath = Path.Combine(recordFolder, $"temp_video_{tempId}.mp4");
            _tempAudioPath = Path.Combine(recordFolder, $"temp_audio_{tempId}.wav");

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            _ffmpegPath = "/opt/homebrew/bin/ffmpeg";
#else
            _ffmpegPath = Path.Combine(Application.streamingAssetsPath, _ffmpegFolderName);
#endif

            if (!File.Exists(_ffmpegPath))
            {
                Debug.LogError($"<color=orange>[GameRecoder]</color> ffmpeg が見つかりませんでした: {_ffmpegPath}");
                ClearRecordPaths();
                return;
            }

            string args;

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            // Mac: 画面だけ録画。音声は GameAudioRecorder が WAV に保存する。
            args =
                "-y " +
                "-f avfoundation " +
                "-framerate 30 " +
                "-capture_cursor 1 " +
                "-i \"3:none\" " +
                "-an " +
                "-c:v libx264 " +
                "-preset ultrafast " +
                "-pix_fmt yuv420p " +
                $"\"{_tempVideoPath}\"";
#else
            // Windows: ddagrab で画面だけ録画。
            args =
                "-y " +
                "-filter_complex \"ddagrab=output_idx=0:framerate=30,hwdownload,format=bgra\" " +
                "-an " +
                "-c:v libx264 " +
                "-preset ultrafast " +
                "-pix_fmt yuv420p " +
                $"\"{_tempVideoPath}\"";
#endif

            try
            {
                _ffmpegProcess = new Process();

                ProcessStartInfo startInfo = new ProcessStartInfo()
                {
                    FileName = _ffmpegPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true
                };

                _ffmpegProcess.StartInfo = startInfo;

                if (_receiveDebugLog)
                {
                    _ffmpegProcess.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            Debug.Log($"<color=orange>[GameRecoder]</color> [ffmpeg stderr] {e.Data}");
                    };

                    _ffmpegProcess.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            Debug.Log($"<color=orange>[GameRecoder]</color> [ffmpeg stdout] {e.Data}");
                    };
                }

                bool started = _ffmpegProcess.Start();
                if (!started)
                {
                    throw new Exception("ffmpeg process could not be started.");
                }

                // stderr/stdout を常に読み捨てることで、FFmpeg側のバッファ詰まりを防ぐ。
                _ffmpegProcess.BeginErrorReadLine();
                _ffmpegProcess.BeginOutputReadLine();

                // FFmpeg起動直後からUnity音声を録音する。
                // 映像/音声の開始タイミングを可能な限り近づける。
                if (!_audioRecorder.StartRecording(_tempAudioPath))
                {
                    StopFfmpegProcessImmediately();
                    DeleteFileIfExists(_tempVideoPath);
                    DeleteFileIfExists(_tempAudioPath);
                    ClearRecordPaths();

                    Debug.LogError("<color=orange>[GameRecoder]</color> Unity音声の録音開始に失敗しました。");
                    return;
                }

                // 起動直後に即終了していたら失敗とみなす。
                System.Threading.Thread.Sleep(100);

                if (_ffmpegProcess.HasExited)
                {
                    _audioRecorder.StopRecording();

                    Debug.LogError(
                        "<color=orange>[GameRecoder]</color> " +
                        "ffmpeg がすぐに終了しました（ddagrab / avfoundation のエラーの可能性）。"
                    );

                    _ffmpegProcess.Dispose();
                    _ffmpegProcess = null;

                    DeleteFileIfExists(_tempVideoPath);
                    DeleteFileIfExists(_tempAudioPath);
                    ClearRecordPaths();
                    return;
                }

                _isRecording = true;

                if (_recordStateCanvas != null)
                    _recordStateCanvas.enabled = true;

                Debug.Log(
                    $"<color=orange>[GameRecoder]</color> 録画開始\n" +
                    $"Video: {_tempVideoPath}\n" +
                    $"Audio: {_tempAudioPath}\n" +
                    $"Output: {_finalOutputPath}"
                );
            }
            catch (Exception ex)
            {
                Debug.LogError($"<color=orange>[GameRecoder]</color> 録画開始時に例外が発生しました: {ex.Message}");

                if (_audioRecorder != null && _audioRecorder.IsRecording)
                {
                    _audioRecorder.StopRecording();
                }

                StopFfmpegProcessImmediately();

                DeleteFileIfExists(_tempVideoPath);
                DeleteFileIfExists(_tempAudioPath);
                ClearRecordPaths();
            }
        }

        /// <summary>
        /// 録画終了
        ///
        /// 1. Unity音声録音終了
        /// 2. FFmpeg画面録画終了
        /// 3. temp_video.mp4 + temp_audio.wav を最終MP4へ結合
        /// 4. 結合成功時のみ一時ファイル削除
        /// </summary>
        public void RecordEnd()
        {
            if (!_isRecording)
                return;

            _isRecording = false;

            if (_recordStateCanvas != null)
                _recordStateCanvas.enabled = false;

            bool audioStoppedSuccessfully = false;
            bool videoStoppedSuccessfully = false;

            try
            {
                // Unity音声録音を止め、WAVヘッダーまで完成させる。
                if (_audioRecorder != null && _audioRecorder.IsRecording)
                {
                    _audioRecorder.StopRecording();
                }

                audioStoppedSuccessfully = File.Exists(_tempAudioPath) && new FileInfo(_tempAudioPath).Length > 44;

                // FFmpegに q を送ってMP4を正常終了させる。
                videoStoppedSuccessfully = StopVideoRecording();

                if (!audioStoppedSuccessfully)
                {
                    Debug.LogError(
                        $"<color=orange>[GameRecoder]</color> 音声一時ファイルが正常に作成されていません: {_tempAudioPath}"
                    );
                    return;
                }

                if (!videoStoppedSuccessfully)
                {
                    Debug.LogError(
                        $"<color=orange>[GameRecoder]</color> 映像一時ファイルが正常に作成されていません: {_tempVideoPath}"
                    );
                    return;
                }

                // temp_video.mp4 + temp_audio.wav -> 最終MP4
                bool muxSucceeded = MuxVideoAndAudio();

                if (!muxSucceeded)
                {
                    Debug.LogError(
                        "<color=orange>[GameRecoder]</color> 映像と音声の結合に失敗しました。" +
                        "一時ファイルは削除せず残します。"
                    );
                    return;
                }

                // 結合成功時だけ削除。
                DeleteFileIfExists(_tempVideoPath);
                DeleteFileIfExists(_tempAudioPath);

                Debug.Log($"<color=orange>[GameRecoder]</color> 録画正常終了: {_finalOutputPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"<color=orange>[GameRecoder]</color> RecordEnd error: {e.Message}");
                Debug.LogWarning(
                    "<color=orange>[GameRecoder]</color> エラー時は復旧できるよう一時ファイルを残します。"
                );
            }
            finally
            {
                _ffmpegProcess?.Dispose();
                _ffmpegProcess = null;

                // 成功/失敗に関係なく、次回録画用に内部参照だけ初期化。
                // 実ファイルは成功時のみ上で削除している。
                ClearRecordPaths();
            }
        }

        /// <summary>
        /// 録画中のFFmpegへ q を送り、temp_video.mp4 を正常終了させる。
        /// </summary>
        private bool StopVideoRecording()
        {
            if (_ffmpegProcess == null)
                return false;

            try
            {
                if (!_ffmpegProcess.HasExited)
                {
                    _ffmpegProcess.StandardInput.WriteLine("q");
                    _ffmpegProcess.StandardInput.Flush();

                    // MP4のmoov atomが書き込まれるまで待つ。
                    if (!_ffmpegProcess.WaitForExit(5000))
                    {
                        Debug.LogError("<color=orange>[GameRecoder]</color> FFmpegの終了がタイムアウトしました。");

                        try
                        {
                            _ffmpegProcess.Kill();
                            _ffmpegProcess.WaitForExit();
                        }
                        catch
                        {
                            // Kill失敗時は下のファイルチェックで失敗扱いにする。
                        }

                        return false;
                    }
                }

                return File.Exists(_tempVideoPath) && new FileInfo(_tempVideoPath).Length > 0;
            }
            catch (Exception e)
            {
                Debug.LogError($"<color=orange>[GameRecoder]</color> FFmpeg stop error: {e.Message}");
                return false;
            }
            finally
            {
                _ffmpegProcess?.Dispose();
                _ffmpegProcess = null;
            }
        }

        /// <summary>
        /// temp_video.mp4 + temp_audio.wav を最終MP4へMuxする。
        ///
        /// 映像は -c:v copy のため再エンコードしない。
        /// WAVだけAACへ変換する。
        /// </summary>
        private bool MuxVideoAndAudio()
        {
            if (string.IsNullOrEmpty(_ffmpegPath) ||
                string.IsNullOrEmpty(_tempVideoPath) ||
                string.IsNullOrEmpty(_tempAudioPath) ||
                string.IsNullOrEmpty(_finalOutputPath))
            {
                return false;
            }

            if (!File.Exists(_tempVideoPath) || !File.Exists(_tempAudioPath))
            {
                return false;
            }

            string args =
                "-y " +
                "-loglevel error " +
                $"-i \"{_tempVideoPath}\" " +
                $"-i \"{_tempAudioPath}\" " +
                "-map 0:v:0 " +
                "-map 1:a:0 " +
                "-c:v copy " +
                "-c:a aac " +
                "-b:a 192k " +
                "-shortest " +
                "-movflags +faststart " +
                $"\"{_finalOutputPath}\"";

            try
            {
                using Process muxProcess = new Process();

                muxProcess.StartInfo = new ProcessStartInfo()
                {
                    FileName = _ffmpegPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                muxProcess.Start();

                // -loglevel error なので出力量は少ない。
                string stderr = muxProcess.StandardError.ReadToEnd();
                string stdout = muxProcess.StandardOutput.ReadToEnd();

                muxProcess.WaitForExit();

                if (_receiveDebugLog && !string.IsNullOrEmpty(stdout))
                {
                    Debug.Log($"<color=orange>[GameRecoder]</color> [mux stdout] {stdout}");
                }

                if (muxProcess.ExitCode != 0)
                {
                    Debug.LogError(
                        $"<color=orange>[GameRecoder]</color> FFmpeg mux failed. ExitCode={muxProcess.ExitCode}\n{stderr}"
                    );
                    return false;
                }

                if (!File.Exists(_finalOutputPath) || new FileInfo(_finalOutputPath).Length <= 0)
                {
                    Debug.LogError("<color=orange>[GameRecoder]</color> Mux後のMP4が作成されませんでした。");
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"<color=orange>[GameRecoder]</color> FFmpeg mux error: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// RecordStart失敗時などにFFmpegを即時終了する。
        /// </summary>
        private void StopFfmpegProcessImmediately()
        {
            if (_ffmpegProcess == null)
                return;

            try
            {
                if (!_ffmpegProcess.HasExited)
                {
                    _ffmpegProcess.Kill();
                    _ffmpegProcess.WaitForExit();
                }
            }
            catch
            {
                // 開始失敗時の後処理なので握り潰す。
            }
            finally
            {
                _ffmpegProcess.Dispose();
                _ffmpegProcess = null;
            }
        }

        private static void DeleteFileIfExists(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"<color=orange>[GameRecoder]</color> 一時ファイル削除失敗: {path}\n{e.Message}");
            }
        }

        private void ClearRecordPaths()
        {
            _finalOutputPath = null;
            _tempVideoPath = null;
            _tempAudioPath = null;
        }

        /// <summary>
        /// exe のあるフォルダパスを取得する
        /// （エディタ / ビルド 両対応）
        /// </summary>
        private string GetExeFolderPath()
        {
#if UNITY_EDITOR
            string dataPath = Application.dataPath; // .../ProjectName/Assets
            return Directory.GetParent(dataPath).FullName;
#else
            // ビルド後:
            // dataPath: .../YourGame_Data
            // exeFolder: その親
            string dataPath = Application.dataPath;
            DirectoryInfo dir = Directory.GetParent(dataPath);
            return dir.FullName;
#endif
        }

        private void OnApplicationQuit()
        {
            // アプリ終了時に録画が残っていたら止める。
            if (_isRecording)
            {
                RecordEnd();
            }
        }
    }
}
