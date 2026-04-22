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

        public int CurrentIdx { get; private set; } = 0;

        void Awake()
        {
            CurrentIdx = _defaultIdx; //Load
            UpdateStatus();
        }

        public void SetItems(string[] itemTexts, int defaultIdx = 0)
        {
            _itemTexts = itemTexts;
            _defaultIdx = defaultIdx;
            CurrentIdx = _defaultIdx;
            UpdateStatus();
        }

        public void OnButtonClicked(bool isIncrease)
        {
            CurrentIdx = isIncrease ? ++CurrentIdx : --CurrentIdx;
            CurrentIdx = Math.Clamp(CurrentIdx, 0, _itemTexts.Length-1);
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if(_itemTexts == null || _itemTexts.Length == 0)
            {
                return;
            }
            _upButtonImage.color = CurrentIdx == _itemTexts.Length-1 ? Color.gray : Color.white;
            _downButtonImage.color = CurrentIdx == 0 ? Color.gray : Color.white;
            _windowText.text = _itemTexts[CurrentIdx];
            _onStatusChanged?.Invoke(_itemTexts[CurrentIdx]);
        }
    }
}