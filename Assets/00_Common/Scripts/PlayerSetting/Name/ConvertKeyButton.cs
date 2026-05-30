using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace EyeMoT
{
    public class ConvertKeyButton : MonoBehaviour
    {
        [Header("Resources")]
        [SerializeField] private TMP_Text _text;

        [Header("Settings")]
        [SerializeField] private NameEditor.ConvertType _convertType;

        public void OnClick()
        {
            PlayerSettingManager.Instance.NameEditor.ConvertMainKey(_convertType);
        }
    }
}