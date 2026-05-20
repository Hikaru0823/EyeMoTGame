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
        [SerializeField] public Button Button;
        [SerializeField] private Animator _buttonAnimator;

        public void SetImage(Sprite sprite)
        {
            _imageDisplay.sprite = sprite;
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