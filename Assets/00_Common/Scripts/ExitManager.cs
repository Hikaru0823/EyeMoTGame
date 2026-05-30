using System.Collections;
using System.Collections.Generic;
using EyeMoT.Fusion;
using Fusion;
using UnityEngine;

namespace EyeMoT
{
    public class ExitManager : MonoBehaviour
    {
        #region Singleton
        public static ExitManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
            {    
                Destroy(gameObject);
            }
        }
        #endregion

        [Header("Resources")]
        [SerializeField] private GameObject _exitButton;

        void Start()
        {
            #if !UNITY_EDITOR && UNITY_WEBGL
            _exitButton.SetActive(false);
            #endif
        }

        void Update()
        {
            if(Input.GetKeyDown(KeyCode.Escape))
            {
                QuitApplication();
            }
        }

        public void QuitApplication()
        {
            if(LobbyManager.Instance.Runner.GameMode != GameMode.Single)
            {
                PopupUI.OnVisible("ゲームを終了しますか？", "再度同じルームには入れませんが、よろしいですか？", PopupUI.Type.Alert, () =>
                {
                    #if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
                    #elif UNITY_WEBGL
                    
                    #else            
                    Application.Quit();
                    #endif
                }, true);
                return;
            }

            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #elif UNITY_WEBGL
            
            #else            
            Application.Quit();
            #endif
        }
    }
}