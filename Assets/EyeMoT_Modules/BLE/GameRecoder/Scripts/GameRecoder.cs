using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug; // System.Diagnostics.Debug と被るのでエイリアス

namespace EyeMoT.GameRecoder
{
    public class GameRecoder : MonoBehaviour
    {
        public static GameRecoder Instance { get; private set; }
        [SerializeField] private string recorderFolderName = "YOUR_RECORD/GameRecord";
        [SerializeField] private string ffmpegFolderName = "GameRecorder/ffmpeg.exe";
        [SerializeField] private string outputPrefix = "GameRecoder_";
        [SerializeField] private bool receiveDebugLog = false;
        [SerializeField] private Canvas recordStateCanvas;
        public bool canRecord = true;
        private string micName = "";
        private Process ffmpegProcess;
        private bool isRecording = false;

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

            SceneManager.sceneLoaded += (scene, mode) =>
            {
                // 自分以外にAudioListenerがあったらそのAudioListenerを削除
                AudioListener[] listeners = FindObjectsOfType<AudioListener>();
                foreach (AudioListener listener in listeners)
                {
                    if (listener.gameObject != gameObject)
                    {
                        Destroy(listener);
                    }
                }

                if(scene.buildIndex == 0)
                {
                    RecordEnd();
                }
            };
        }

        /// <summary>
        /// 録画開始（ddagrab 使用）
        /// </summary>
        public void RecordStart(string dirName = "")
        {
            #if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"<color=orange>[GameRecoder]</color> WebGL platform does not support GameRecoder. Initialization skipped.");
            return;
            #endif

            if (isRecording)
            {
                Debug.LogWarning("<color=orange>[GameRecoder]</color> 既に録画中です。");
                return;
            }

            if (!canRecord)
            {
                Debug.LogWarning("<color=orange>[GameRecoder]</color> 録画は無効化されています。");
                return;
            }

            recordStateCanvas.enabled = true;
            string exeFolder = GetExeFolderPath();

            string recordFolder = Path.Combine(exeFolder, recorderFolderName + (string.IsNullOrEmpty(dirName) ? "" : $"/{dirName}"));
            Directory.CreateDirectory(recordFolder);

            string timestamp = DateTime.Now.ToString("yyyyMMddHHmm");
            string fileName = $"{outputPrefix}_{timestamp}.mp4";
            string outputPath = Path.Combine(recordFolder, fileName);

            string ffmpegPath = Path.Combine(Application.streamingAssetsPath, ffmpegFolderName);

            if (!File.Exists(ffmpegPath))
            {
                Debug.LogError($"<color=orange>[GameRecoder]</color> ffmpeg が見つかりませんでした: {ffmpegPath}");
                return;
            }

            string windowTitle = Application.productName;

            // micName = GetDefaultMic();
            // string audioPart = "";
            // string mapPart   = "-map \"[v]\""; // 映像だけ

            // // if (!string.IsNullOrEmpty(micName))
            // // {
            // //     audioPart =
            // //         $"-f dshow -i audio=\"{micName}\" ";
            // //     mapPart =
            // //         "-map \"[v]\" -map 1:a ";
            // // }

            string args =
                "-y " +
        "-filter_complex \"ddagrab=output_idx=0:framerate=30,hwdownload,format=bgra\" " +
        "-c:v libx264 -preset ultrafast -pix_fmt yuv420p " +
        $"\"{outputPath}\"";

            try
            {
                ffmpegProcess = new Process();
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
                ffmpegProcess.StartInfo = StartInfo;

                if(receiveDebugLog)
                {
                    ffmpegProcess.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            Debug.Log($"<color=orange>[GameRecoder]</color> [ffmpeg stderr] {e.Data}");
                    };
                    ffmpegProcess.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            Debug.Log($"<color=orange>[GameRecoder]</color> [ffmpeg stdout] {e.Data}");
                    };
                }

                bool started = ffmpegProcess.Start();
                ffmpegProcess.BeginErrorReadLine();
                ffmpegProcess.BeginOutputReadLine();

                // 起動直後に即終了していたら失敗とみなす
                System.Threading.Thread.Sleep(100);
                if (ffmpegProcess.HasExited)
                {
                    Debug.LogError("<color=orange>[GameRecoder]</color> ffmpeg がすぐに終了しました（ddagrab 非対応 or エラーの可能性）。");
                    ffmpegProcess.Dispose();
                    ffmpegProcess = null;
                    return;
                }

                isRecording = true;
                Debug.Log($"<color=orange>[GameRecoder]</color> 録画開始 {outputPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"<color=orange>[GameRecoder]</color> ffmpeg 起動時に例外が発生しました: {ex.Message}");
                ffmpegProcess?.Dispose();
                ffmpegProcess = null;
            }
        }

        /// <summary>
        /// 録画終了
        /// </summary>
        public void RecordEnd()
        {
            if (!isRecording) return;
            recordStateCanvas.enabled = false;
            try
            {
                if (ffmpegProcess != null && !ffmpegProcess.HasExited)
                {
                    // ★ 正常終了：q を送る
                    ffmpegProcess.StandardInput.WriteLine("q");
                    ffmpegProcess.StandardInput.Flush();

                    // moov が書かれるまで待つ
                    ffmpegProcess.WaitForExit(2000); // 最大 2 秒待機
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"<color=orange>[GameRecoder]</color> FFmpeg stop error: {e.Message}");
            }
            finally
            {
                ffmpegProcess?.Dispose();
                ffmpegProcess = null;
                isRecording = false;

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
            if (isRecording)
            {
                RecordEnd();
            }
        }

        /// <summary>
        /// マイクが未設定の場合、接続されているマイクの一つを割り当てる
        /// </summary>
        /// <returns>マイクが存在すればtrue、なければfalse</returns>
        private string GetDefaultMic()
        {
            if (string.IsNullOrEmpty(micName))
            {
                string[] devices = Microphone.devices;
                if (devices.Length > 0)
                {
                    return devices[1];
                }
                return "";
            }
            return micName;
        }
    }
}