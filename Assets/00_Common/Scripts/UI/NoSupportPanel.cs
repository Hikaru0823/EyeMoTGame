using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NoSupportPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UnityEngine.UI.Button _noSupportButton;
    [SerializeField] private GameObject _noSupportPanel;

    private void Awake()
    {
        #if UNITY_WEBGL && !UNITY_EDITOR
        _noSupportButton.interactable = false;
        _noSupportPanel.SetActive(true);
        #endif
    }
}
