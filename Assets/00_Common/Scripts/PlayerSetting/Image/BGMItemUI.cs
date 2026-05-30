using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EyeMoT
{
    public class BGMItemUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Resources")]
        [SerializeField] private TMP_Text _bgmNameText;
        [SerializeField] private Button _button;
        [SerializeField] private Animator _buttonAnimator;

        public AudioClip AudioClip { get; private set; }
        public string Name { get; private set; }
        public Animator ButtonAnimator => _buttonAnimator;

        public void Init(string name, AudioClip audioClip, Action<BGMItemUI> onClick = null)
        {
            _bgmNameText.text = name;
            Name = name;
            AudioClip = audioClip;
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