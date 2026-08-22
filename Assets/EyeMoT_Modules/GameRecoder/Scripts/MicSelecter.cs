using System.Collections;
using System.Collections.Generic;
using EyeMoT;
using TMPro;
using UnityEngine;

public class MicSelecter : MonoBehaviour
{
    [Header("Resources")]
    [SerializeField] private DropdownSelecterUI _dropdownSelecter;
    [SerializeField] private GameObject _hidePanel;
    public void VisibleHidePanel(bool isVisible) => _hidePanel.SetActive(!isVisible);

    void Start()
    {
        var devices = GameRecoder.Instance.GetMicDevices();
        _dropdownSelecter.SetItems(devices);
    }
}
