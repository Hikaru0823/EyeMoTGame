using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EyeMoT
{
    public class GazeControllerToggle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Resources")]
        public Image gazeGage;
        public Toggle _toggle;

        [Header("Settings")]
        public float gazeTime = 1.5f;
        public bool Enable = true;
        bool _isStart = false;
        float _time = 0;

        void Awake()
        {
            if (gazeGage == null)
                gazeGage = transform.Find("Content").transform.Find("GazeGage").GetComponent<Image>();
            if(_toggle == null)
                _toggle = GetComponent<Toggle>();

            _toggle.onValueChanged.AddListener((a) => SetState());
        }

        void LateUpdate()
        {
            if(!Enable)
                return;
            if(_isStart)
            {
                if(!_toggle.interactable)
                    SetState();
                //注視時間が経過したらボタンを押す
                _time += Time.deltaTime;
                gazeGage.fillAmount = _time / gazeTime;
                if(_time > gazeTime)
                {
                    _toggle.isOn = !_toggle.isOn;
                    SetState();
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if(!Enable)
                return;
            if(!_toggle.interactable)
                return;
            _isStart = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if(!Enable)
                return;
            if(!_toggle.interactable)
                return;
            SetState();
        }

        public void SetState()
        {
            _isStart = false;
            _time = 0;
            gazeGage.fillAmount = 0;
        }
    }
}