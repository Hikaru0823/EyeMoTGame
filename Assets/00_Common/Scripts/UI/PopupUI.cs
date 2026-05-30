using System;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EyeMoT;

public class PopupUI : Singleton<PopupUI>
{
    [Header("Resources")]
	[SerializeField] private GameObject _panel;
    [SerializeField] private GameObject _bgPanel;
	[SerializeField] private TMP_Text title;
	[SerializeField] private TMP_Text description;
	[SerializeField] private Button okButton;
    [SerializeField] private Button noButton;
    [SerializeField] private Image icon;
    [SerializeField] private Sprite[] _typeIcons;

    private bool _isCursorVisibleBeforePopup;

    public static void OnVisible(string title, string description, Sprite sprite, UnityEngine.Events.UnityAction onClose = null)
    {
        Instance._isCursorVisibleBeforePopup = CursorManager.Instance.IsCursorVisible;
        CursorManager.Instance.SetCursorVisible(true);
        Instance.noButton.gameObject.SetActive(true);
        Instance.title.text = title;
        Instance.description.text = description;
        Instance.icon.sprite = sprite;

        Instance._panel.SetActive(true);
        Instance._bgPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;

        Instance.okButton.onClick.RemoveAllListeners();
        Instance.okButton.onClick.AddListener(() =>
        {
            Instance.CloseButton();
            if (onClose != null)
            {
                onClose.Invoke();
            }
        });
    }

    public static void OnVisible(string title, string description, Type type, UnityEngine.Events.UnityAction onClose = null, bool isNoButtonActive = false)
    {
        Instance._isCursorVisibleBeforePopup = CursorManager.Instance.IsCursorVisible;
        CursorManager.Instance.SetCursorVisible(true);
        Instance.noButton.gameObject.SetActive(isNoButtonActive);
        Instance.title.text = title;
        Instance.description.text = description;
        Instance.icon.sprite = Instance._typeIcons[(int)type];

        Instance._panel.SetActive(true);
        Instance._bgPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;

        Instance.okButton.onClick.RemoveAllListeners();
        Instance.okButton.onClick.AddListener(() =>
        {
            Instance.CloseButton();
            if (onClose != null)
            {
                onClose.Invoke();
            }
        });
    }

	public void CloseButton()
	{
		_panel.SetActive(false);
        _bgPanel.SetActive(false);
        CursorManager.Instance.SetCursorVisible(_isCursorVisibleBeforePopup);
	}

    public enum Type
    {
        Confirm,
        Alert,
    }
}