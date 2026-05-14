using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace EyeMoT
{
    public class MultipleSelecterUI : SelecterUI
    {
        [Header("Resources")]
        [SerializeField] private Transform _buttonContent;
        [SerializeField] private UnityEvent<int> _onStatusChanged;

        [Header("Setting")]
        [SerializeField] private Color _highlightColor;
        [SerializeField] private Color _disabledColor;
        [SerializeField] private int _defaultIdx = 0;
        [SerializeField] private bool _useSaveData = false;

        private int _currentIdx = 0;
        private ButtonData[] _buttons;

        void Awake()
        {
            int idx = 0;
            _buttons = new ButtonData[_buttonContent.childCount];
            foreach(Transform child in _buttonContent.transform)
            {
                if(child == this.transform) continue;

                int buttonIndex = idx;
                child.GetComponent<Button>().onClick.AddListener(() => OnButtonClicked(buttonIndex));
                _buttons[buttonIndex] = new ButtonData(child.Find("Content").GetComponent<Image>(), child.Find("Outline").GetComponent<Image>());
                idx++;
            }

            if(!_useSaveData)
            {    
                _currentIdx = _defaultIdx;
                UpdateStatus();
            }
        }

        public void OnButtonClicked(int idx)
        {
            if(_currentIdx == idx) return;

            _currentIdx = idx;
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            foreach(var button in _buttons)
            {
                button.Outline.enabled = false;
                button.Background.color = _disabledColor;
            }

            _buttons[_currentIdx].Outline.enabled = true;
            _buttons[_currentIdx].Background.color = _highlightColor;

            _onStatusChanged?.Invoke(_currentIdx);
        }

        private class ButtonData
        {
            public Image Background;
            public Image Outline;

            public ButtonData(Image bg, Image outline)
            {
                Background = bg;
                Outline = outline;
            }
        }
    
        public override void Initialize(int idx)
        {
            int i = 0;
            _buttons = new ButtonData[_buttonContent.childCount];
            foreach(Transform child in _buttonContent.transform)
            {
                if(child == this.transform) continue;

                int buttonIndex = i;
                child.GetComponent<Button>().onClick.AddListener(() => OnButtonClicked(buttonIndex));
                _buttons[buttonIndex] = new ButtonData(child.Find("Content").GetComponent<Image>(), child.Find("Outline").GetComponent<Image>());
                i++;
            }
            _currentIdx = idx == -1 ? _defaultIdx : idx;
            UpdateStatus();
        }

        public override string[] GetItems()
        {
            string[] items = new string[_buttons.Length];
            for(int i = 0; i < _buttons.Length; i++)
            {
                items[i] = _buttons[i].Background.GetComponentInChildren<TMP_Text>().text;
            }
            return items;
        }
    }
}
