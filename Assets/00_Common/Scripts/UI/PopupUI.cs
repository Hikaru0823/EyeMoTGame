using System;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PopupUI : Singleton<PopupUI>
{
    [Header("Resources")]
	[SerializeField] private Canvas ui;
	[SerializeField] private TMP_Text title;
	[SerializeField] private TMP_Text description;
	[SerializeField] private Button okButton;
    [SerializeField] private Button noButton;
    [SerializeField] private Image icon;
    [SerializeField] private Sprite[] _typeIcons;

    public static void OnVisible(string title, string description, Sprite sprite, UnityEngine.Events.UnityAction onClose = null)
    {
        Instance.noButton.gameObject.SetActive(true);
        Instance.title.text = title;
        Instance.description.text = description;
        Instance.icon.sprite = sprite;

        Instance.ui.enabled = true;
        Cursor.lockState = CursorLockMode.None;

        Instance.okButton.onClick.RemoveAllListeners();
        if (onClose != null)
        {
            Instance.okButton.onClick.AddListener(() =>
            {
                Instance.CloseButton();
                onClose.Invoke();
            });
        }
    }

    public static void OnVisible(string title, string description, Type type, UnityEngine.Events.UnityAction onClose = null)
    {
        Instance.noButton.gameObject.SetActive(false);
        Instance.title.text = title;
        Instance.description.text = description;
        Instance.icon.sprite = Instance._typeIcons[(int)type];

        Instance.ui.enabled = true;
        Cursor.lockState = CursorLockMode.None;

        Instance.okButton.onClick.RemoveAllListeners();
        if (onClose != null)
        {
            Instance.okButton.onClick.AddListener(() =>
            {
                Instance.CloseButton();
                onClose.Invoke();
            });
        }
    }

	public void CloseButton()
	{
		ui.enabled = false;
	}

    public enum Type
    {
        Confirm,
        Alert,
    }
}