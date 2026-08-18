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
        [SerializeField] private string _recorderFolderName = "YOUR_RECORD/GameRecord";
        [SerializeField] private string _ffmpegFolderName = "GameRecorder/ffmpeg.exe";
        [SerializeField] private string _outputPrefix = "GameRecoder_";
        [SerializeField] private bool _receiveDebugLog = false;
        [SerializeField] private Canvas _recordStateCanvas;
        public bool CanRecord = true;
        private Process _ffmpegProcess;
        private bool _isRecording = false;

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
        /// 録画開始（ddagrab 使用）
        /// </summary>
        public void RecordStart(string dirName = "", string fileName = "")
        {
            #if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"<color=orange>[GameRecoder]</color> WebGL platform does not support GameRecoder. Initialization skipped.");
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

            string exeFolder = GetExeFolderPath();

            string recordFolder = string.IsNullOrEmpty(dirName) ? Path.Combine(exeFolder, _recorderFolderName) : dirName;
            Directory.CreateDirectory(recordFolder);

            string timestamp = DateTime.Now.ToString("yyyyMMddHHmm");
            string currentfileName = string.IsNullOrEmpty(fileName) ? $"{_outputPrefix}_{timestamp}.mp4" : $"{fileName}.mp4";
            string outputPath = Path.Combine(recordFolder, currentfileName);

            string ffmpegPath = Path.Combine(Application.streamingAssetsPath, _ffmpegFolderName);

            if (!File.Exists(ffmpegPath))
            {
                Debug.LogError($"<color=orange>[GameRecoder]</color> ffmpeg が見つかりませんでした: {ffmpegPath}");
                return;
            }

            string windowTitle = Application.productName;

            string args =
                "-y " +
        "-filter_complex \"ddagrab=output_idx=0:framerate=30,hwdownload,format=bgra\" " +
        "-c:v libx264 -preset ultrafast -pix_fmt yuv420p " +
        $"\"{outputPath}\"";

            try
            {
                _ffmpegProcess = new Process();
                ProcessStartInfo StartInfo = new ProcessStartInfo()
                {
                    FileName = ffmpegPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true
                };
                _ffmpegProcess.StartInfo = StartInfo;

                if(_receiveDebugLog)
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
                _ffmpegProcess.BeginErrorReadLine();
                _ffmpegProcess.BeginOutputReadLine();

                // 起動直後に即終了していたら失敗とみなす
                System.Threading.Thread.Sleep(100);
                if (_ffmpegProcess.HasExited)
                {
                    Debug.LogError("<color=orange>[GameRecoder]</color> ffmpeg がすぐに終了しました（ddagrab 非対応 or エラーの可能性）。");
                    _ffmpegProcess.Dispose();
                    _ffmpegProcess = null;
                    return;
                }

                _isRecording = true;
                Debug.Log($"<color=orange>[GameRecoder]</color> 録画開始 {outputPath}");
                _recordStateCanvas.enabled = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"<color=orange>[GameRecoder]</color> ffmpeg 起動時に例外が発生しました: {ex.Message}");
                _ffmpegProcess?.Dispose();
                _ffmpegProcess = null;
            }
        }

        /// <summary>
        /// 録画終了
        /// </summary>
        public void RecordEnd()
        {
            if (!_isRecording) return;
            _recordStateCanvas.enabled = false;
            try
            {
                if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
                {
                    // ★ 正常終了：q を送る
                    _ffmpegProcess.StandardInput.WriteLine("q");
                    _ffmpegProcess.StandardInput.Flush();

                    // moov が書かれるまで待つ
                    _ffmpegProcess.WaitForExit(2000); // 最大 2 秒待機
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"<color=orange>[GameRecoder]</color> FFmpeg stop error: {e.Message}");
            }
            finally
            {
                _ffmpegProcess?.Dispose();
                _ffmpegProcess = null;
                _isRecording = false;

                Debug.Log("<color=orange>[GameRecoder]</color> 録画正常終了");
            }
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
            // アプリ終了時に録画が残っていたら止める
            if (_isRecording)
            {
                RecordEnd();
            }
        }
    }
}