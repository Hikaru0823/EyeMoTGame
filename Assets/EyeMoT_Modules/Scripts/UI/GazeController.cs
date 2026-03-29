using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EyeMoT
{
    public class GazeController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Resources")]
        public Image gazeGage;
        public Button button;

        [Header("Settings")]
        public float gazeTime = 1.5f;
        public bool isSelectable = false;
        public bool isContinueClickable = true;
        bool _isStart = false;
        bool _isSelect = false;
        float _time = 0;

        void Awake()
        {
            if (gazeGage == null)
                gazeGage = transform.Find("Content").transform.Find("GazeGage").GetComponent<Image>();
            if(button == null)
                button = GetComponent<Button>();

            //連続で押せるボタンはクリックしたときにGazeGageをリセットしたい
            if(isContinueClickable)
                button.onClick.AddListener(() => SetState(false));
        }

        void LateUpdate()
        {
            if(_isStart)
            {
                if(!button.interactable)
                    SetState(false);
                //注視時間が経過したらボタンを押す
                _time += Time.deltaTime;
                gazeGage.fillAmount = _time / gazeTime;
                if(_time > gazeTime)
                {
                    button.onClick.Invoke();
                    SetState(true);
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if(_isSelect)
                return;
            if(!button.interactable)
                return;
            _isStart = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if(_isSelect)
                return;
            if(!button.interactable)
                return;
            SetState(false);
        }

        public void SetState(bool isSelected)
        {
            if(isSelected)
            {
                _isStart = false;
                _time = 0;
                if(isSelectable)
                {    
                    _isSelect = true;
                    gazeGage.fillAmount = 1;
                }
                else
                    gazeGage.fillAmount = 0;
            }
            else
            {
                _isSelect = false;
                _isStart = false;
                _time = 0;
                gazeGage.fillAmount = 0;
            }
        }
    }
}