using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class BleDeviceButton : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _nameText;
    //[SerializeField] private TextMeshProUGUI _rawNameText;

    public void Init(string name, UnityAction onClicked)
    {
        _button.onClick.AddListener(onClicked);
        _nameText.text = name;
       // _rawNameText.text = rawName;
    }
}
