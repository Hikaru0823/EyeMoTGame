using UnityEngine;
using System.Diagnostics;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EyeMoT
{
    public class EyeMoTMouse : MonoBehaviour
    {
        #region Singleton
        private static EyeMoTMouse instance = null;
        void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(this.gameObject);
            }
            else
            {
                Destroy(this.gameObject);
            }
        }
        #endregion
        [Header("Resources")]
        [SerializeField] private Sprite[] icons = default;
        [SerializeField] private Button gazeButton = default;
        [SerializeField] private Image gazeImage = default;

        private Image gazeButtonImage = default;
        private AudioSource audioSource = default;
        private Keyboard keyboard = default;
        private Process cmdProcess = default;

        private bool isTrackable = false;
        private bool isInitialized = false;

        void Start()
        {
            #if UNITY_WEBGL || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            gazeButton.interactable = false;
            Destroy(this.gameObject);
            return;
            #endif
            Process[] eyeMoTProceses = Process.GetProcessesByName("EyeMoTMouse");

            if (eyeMoTProceses.Length > 0)
            {
                foreach (Process eyeMoTProcess in eyeMoTProceses)
                    eyeMoTProcess.Kill();
            }

            if (this.cmdProcess == null)
            {
                this.cmdProcess = new Process();

                this.cmdProcess.StartInfo.FileName = Application.dataPath + "/../EyeMoTMouse/EyeMoTMouse.exe";

                this.cmdProcess.StartInfo.Arguments = "30";

                // this.cmdProcess.StartInfo.CreateNoWindow = true; 

                this.cmdProcess.EnableRaisingEvents = true;

                this.cmdProcess.Exited += CmdProcessExited;

                this.cmdProcess.StartInfo.UseShellExecute = false;

                this.cmdProcess.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;

                this.cmdProcess.StartInfo.RedirectStandardOutput = true;

                this.cmdProcess.StartInfo.RedirectStandardInput = true;

                this.cmdProcess.OutputDataReceived += OutputHandler;

                this.cmdProcess.Start();

                this.cmdProcess.BeginOutputReadLine();

                this.gazeButtonImage = this.gazeImage;
                this.audioSource = this.GetComponent<AudioSource>();
                this.keyboard = Keyboard.current;

                this.OnStatusChanged(!this.isTrackable);
                this.isInitialized = true;
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (this.keyboard != null)
            {
                if (this.keyboard.xKey.wasReleasedThisFrame)
                    this.OnStatusChanged(this.isTrackable);
            }
        }

        // EyeMoTMouse
        private void OutputHandler(object sender, DataReceivedEventArgs args)
        {
            if (!string.IsNullOrEmpty(args.Data))
            {
                string trimedArgs = args.Data.Trim();

                switch (trimedArgs)
                {
                    case "0":
                        this.gazeButtonImage.sprite = this.icons[1];
                        return;

                    case "1":
                        this.gazeButtonImage.sprite = this.icons[0];
                        return;

                    case "StartUp":
                        this.gazeButtonImage.sprite = this.icons[0];
                        return;
                }
            }
        }

        void CmdProcessExited(object sender, System.EventArgs e)
        {
            this.cmdProcess.Dispose();
            this.cmdProcess = null;
        }

        private void OnApplicationQuit()
        {
            #if UNITY_WEBGL || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            return;
            #endif
            if (this.cmdProcess != null)
            {
                this.cmdProcess.StandardInput.WriteLine("exit");

                this.cmdProcess.WaitForExit(1500);
                this.cmdProcess.Kill();
                this.cmdProcess.Dispose();
                this.cmdProcess = null;
            }
        }

        public void OnButtonClicked(Button button)
        {
            #if UNITY_WEBGL || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            return;
            #endif
            this.OnStatusChanged(this.isTrackable);
        }

        private void OnStatusChanged(bool isTrackable)
        {
            #if UNITY_WEBGL || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            return;
            #endif
            if (isTrackable)
            {
                UnityEngine.Debug.Log("<color=orange>[EyeMoTMouse]</color> EyeMoTMouse is turned off.");
                this.gazeButtonImage.sprite = this.icons[0];
                this.cmdProcess.StandardInput.WriteLine("mouse_off");
                this.isTrackable = false;
            }
            else
            {
                UnityEngine.Debug.Log("<color=orange>[EyeMoTMouse]</color> EyeMoTMouse is turned on.");
                this.gazeButtonImage.sprite = this.icons[1];
                this.cmdProcess.StandardInput.WriteLine("mouse_on");
                this.isTrackable = true;
            }

            // if (this.isInitialized)
            //     this.audioSource.Play();
        }
    }
}
