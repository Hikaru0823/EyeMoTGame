using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EyeMoT
{
    public class ImageItemUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Resources")]
        [SerializeField] private Image _imageDisplay;
        [SerializeField] private Button _button;
        [SerializeField] private Animator _buttonAnimator;

        public string Name { get; private set; }
        public Sprite Sprite => _imageDisplay.sprite;
        public Animator ButtonAnimator => _buttonAnimator;

        public void Init(string name, Sprite sprite, Color color, Action<ImageItemUI> onClick = null)
        {
            _imageDisplay.color = color;
            _imageDisplay.sprite = sprite;
            _imageDisplay.color = color;
            Name = name;
            if (onClick != null)
            {
                _button.onClick.AddListener(() => onClick.Invoke(this));
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
    #if !UNITY_ANDROID && !UNITY_IOS
            if (!_buttonAnimator.GetCurrentAnimatorStateInfo(0).IsName("Normal to Pressed"))
                _buttonAnimator.Play("Dissolve to Normal");
    #endif
        }

        public void OnPointerExit(PointerEventData eventData)
        {
    #if !UNITY_ANDROID && !UNITY_IOS
            if (!_buttonAnimator.GetCurrentAnimatorStateInfo(0).IsName("Normal to Pressed"))
                _buttonAnimator.Play("Normal to Dissolve");
    #endif
        }
    }
}