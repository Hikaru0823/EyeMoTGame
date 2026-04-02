using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace EyeMoT
{
    public class SingleSelecterUI : MonoBehaviour
    {
        [Header("Resources")]
        [SerializeField] private TMP_Text _windowText;
        [SerializeField] private Image _upButtonImage;
        [SerializeField] private Image _downButtonImage;
        [SerializeField] private UnityEvent<string> _onStatusChanged;

        [Header("Setting")]
        [SerializeField] private string[] _itemTexts;
        [SerializeField] private int _defaultIdx = 0;

        private int _currentIdx = 0;

        void Awake()
        {
            _currentIdx = _defaultIdx; //Load
            UpdateStatus();
        }

        public void OnButtonClicked(bool isIncrease)
        {
            _currentIdx = isIncrease ? ++_currentIdx : --_currentIdx;
            _currentIdx = Math.Clamp(_currentIdx, 0, _itemTexts.Length-1);
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            _upButtonImage.color = _currentIdx == _itemTexts.Length-1 ? Color.gray : Color.white;
            _downButtonImage.color = _currentIdx == 0 ? Color.gray : Color.white;
            _windowText.text = _itemTexts[_currentIdx];
            _onStatusChanged?.Invoke(_itemTexts[_currentIdx]);
        }
    }
}