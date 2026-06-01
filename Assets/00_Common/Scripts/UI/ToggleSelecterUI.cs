using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace EyeMoT
{
    public class ToggleSelecterUI : SelecterUI
    {
        [Header("Resources")]
        [SerializeField] private Toggle _toggle;

        [Header("Setting")]
        [SerializeField] private bool _defaultState = false;
        [SerializeField] private bool _useSaveData = false;

        public bool CurrentState { get; private set; } = false;

        void Awake()
        {
            if(!_useSaveData)
            {    
                CurrentState = _defaultState;
                UpdateStatus();
            }
        }

        public override void Initialize(int isOn)
        {
            CurrentState = isOn == -1 ? _defaultState : isOn == 0;
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            _toggle.isOn = CurrentState;
        }
    }
}