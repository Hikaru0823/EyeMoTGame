using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EyeMoT
{
    public class ModuleManager : Singleton<ModuleManager>
    {
        [Header("Resources")]
        [SerializeField] private GameObject _buttonGroup;
        [SerializeField] private GameObject _visibleKeyText;

        public bool IsButtonGroupVisible => _buttonGroup.activeSelf;

        public void SetVisibleButtonGroup(bool isVisible)
        {
            _buttonGroup.SetActive(isVisible);
            _visibleKeyText.SetActive(!isVisible);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.H) && PlayerData.Instance.CanUseShortCut)
            {
                foreach (var hideByModule in FindObjectsOfType<HideByModule>())
                {
                    hideByModule.SetVisible(!_buttonGroup.activeSelf);
                }
                SetVisibleButtonGroup(!_buttonGroup.activeSelf);
            }
        }
    }
}