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
        public bool isContinueClickable = true;
        public bool Enable = true;
        bool _isStart = false;
        float _time = 0;

        void Awake()
        {
            if (gazeGage == null)
                gazeGage = transform.Find("Content").transform.Find("GazeGage").GetComponent<Image>();
            if(button == null)
                button = GetComponent<Button>();

            //連続で押せるボタンはクリックしたときにGazeGageをリセットしたい
            if(isContinueClickable)
                button.onClick.AddListener(() => Reset());
        }

        void OnDisable()
        {
            Reset();
        }

        void LateUpdate()
        {
            if(!Enable)
                return;
            if(_isStart)
            {
                if(!button.interactable)
                    Reset();
                //注視時間が経過したらボタンを押す
                _time += Time.deltaTime;
                gazeGage.fillAmount = _time / gazeTime;
                if(_time > gazeTime)
                {
                    button.onClick.Invoke();
                    Reset();
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if(!Enable)
                return;

            if(!button.interactable)
                return;
            _isStart = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if(!Enable)
                return;

            if(!button.interactable)
                return;
            Reset();
        }

        public void Reset()
        {
            _isStart = false;
            _time = 0;
            gazeGage.fillAmount = 0;
        }
    }
}