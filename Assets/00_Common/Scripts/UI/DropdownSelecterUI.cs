using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace EyeMoT
{
    public class DropdownSelecterUI : SelecterUI
    {
        [Header("Resources")]
        [SerializeField] private TMP_Dropdown _dropdown;
        [SerializeField] private UnityEvent<int, string> _onStatusChanged;

        [Header("Setting")]
        [SerializeField] private string[] _itemTexts;
        [SerializeField] private int _defaultValue = 0;
        [SerializeField] private bool _useSaveData = false;

        public int CurrentValue { get; private set; } = 0;

        void Awake()
        {
            if(!_useSaveData)
            {    
                CurrentValue = _defaultValue;
                UpdateStatus();
            }
        }

        public override void Initialize(int idx)
        {
            CurrentValue = idx == -1 ? _defaultValue : idx;
            UpdateStatus();
        }

        public void SetItems(string[] itemTexts, int defaultIdx = 0)
        {
            _itemTexts = itemTexts;
            CurrentValue = defaultIdx;
            _dropdown.ClearOptions();
            _dropdown.AddOptions(itemTexts.ToList());
            UpdateStatus();
        }

        public void OnValueChanged(int value)
        {
            CurrentValue = value;
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            _dropdown.value = CurrentValue;
            _onStatusChanged?.Invoke(CurrentValue,_dropdown.options[CurrentValue].text);
        }
    }
}