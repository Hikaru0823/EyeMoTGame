using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace EyeMoT
{
    public class SliderSelecterUI : SelecterUI
    {
        [Header("Resources")]
        [SerializeField] private TMP_Text _windowText;
        [SerializeField] private Slider _slider;
        [SerializeField] private UnityEvent<string> _onStatusChanged;

        [Header("Setting")]
        [SerializeField] private float _defaultValue = 0;
        [SerializeField] private bool _useSaveData = false;
        [SerializeField] private float _minValue = 0;
        [SerializeField] private float _maxValue = 1;

        public float CurrentValue { get; private set; } = 0;

        void Awake()
        {
            _slider.minValue = _minValue;
            _slider.maxValue = _maxValue;
            if(!_useSaveData)
            {    
                CurrentValue = _defaultValue;
                UpdateStatus();
            }
        }

        public override void Initialize(int idx)
        {
            CurrentValue = idx == -1 ? _defaultValue : Mathf.Clamp(idx, _minValue, _maxValue);
            UpdateStatus();
        }

        public void OnValueChanged(float value)
        {
            CurrentValue = value;
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            _slider.value = CurrentValue;
            _windowText.text = CurrentValue.ToString("F3");
            _onStatusChanged?.Invoke(CurrentValue.ToString("F3"));
        }
    }
}