using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EyeMoT
{
    public class HideByModule : MonoBehaviour
    {
        [Header("Resources")]
        [SerializeField] private GameObject[] _hideObjects;

        void OnEnable()
        {
            SetVisible(ModuleManager.Instance.IsButtonGroupVisible);
        }

        public void SetVisible(bool isVisible)
        {
            foreach (var obj in _hideObjects)
            {
                obj.SetActive(isVisible);
            }
        }
    }
}