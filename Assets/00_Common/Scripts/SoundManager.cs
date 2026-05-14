using System.Collections;
using System.Collections.Generic;
using KanKikuchi.AudioManager;
using UnityEngine;

namespace EyeMoT
{
    public class SoundManager : Singleton<SoundManager>
    {
        [Header("Resources")]
        [SerializeField] private GameObject _bgmHidePanel;
        [SerializeField] private GameObject _seHidePanel;

        private int _currentState = 0; // 0: both on, 1: bgm off, 2: se off, 3: both off

        void Start()
        {
            _currentState = ES3.Load<int>(SaveKeys.SoundState, defaultValue: 0);
            UpdateState(_currentState);
        }

        public void OnClicked()
        {
            _currentState = (_currentState + 1) % 4;
            UpdateState(_currentState);
        }

        private void UpdateState(int state)
        {
            _currentState = state;
            bool isBgmOff = _currentState == 1 || _currentState == 3;
            bool isSeOff = _currentState == 2 || _currentState == 3;
            _bgmHidePanel.SetActive(isBgmOff);
            _seHidePanel.SetActive(isSeOff);
            BGMManager.Instance.ChangeBaseVolume(isBgmOff ? 0 : 1);
            SEManager.Instance.ChangeBaseVolume(isSeOff ? 0 : 1);
            ES3.Save<int>(SaveKeys.SoundState, _currentState);
        }
    }
}