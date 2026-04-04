using System.Collections;
using System.Collections.Generic;
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

        public void QuitApplication()
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #elif UNITY_WEBGL
            
            #else            
            Application.Quit();
            #endif
        }
    }
}