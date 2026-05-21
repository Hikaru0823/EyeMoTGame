using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EyeMoT
{
    public class ModuleManager : Singleton<ModuleManager>
    {
        [Header("Resources")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private GameObject _exitButton;
        [SerializeField] private GameObject _eyemotMouseButton;
        [SerializeField] private GameObject _soundButton;
        [SerializeField] private GameObject _playerSettingButton;

        void Start()
        {
            #if UNITY_WEBGL || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            _exitButton.SetActive(false);
            _eyemotMouseButton.SetActive(false);
            #endif
        }

        public void Visible(bool isVisible)
        {
            _canvas.enabled = isVisible;
        }

        public void VisiblePlayerSettingButton(bool isVisible)
        {
            _playerSettingButton.SetActive(isVisible);
        }
    }
}