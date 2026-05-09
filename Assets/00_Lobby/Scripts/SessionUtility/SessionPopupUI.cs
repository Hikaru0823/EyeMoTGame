using System;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace EyeMoT.Fusion
{
    public class SessionPopupUI : Singleton<SessionPopupUI>
    {
        [Header("Resources")]
        [SerializeField] private Canvas ui;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text description;
        [SerializeField] private Button okButton;
        [SerializeField] private Button noButton;
        [SerializeField] private Image icon;
        [SerializeField] private MultipleSelecterUI selecterUI;

        public SessionDef.Mode _currentMode = SessionDef.Mode.COLLABOLATION;

        public static void OnVisible(string title, string description, Sprite sprite, string sessionCode)
        {
            Instance.selecterUI.OnButtonClicked(0);
            Instance.noButton.gameObject.SetActive(true);
            Instance.title.text = title;
            Instance.description.text = description;
            Instance.icon.sprite = sprite;

            Instance.ui.enabled = true;
            Cursor.lockState = CursorLockMode.None;

            Instance.okButton.onClick.RemoveAllListeners();
            Instance.okButton.onClick.AddListener(() =>
            {
                Instance.CloseButton();
                LobbyManager.Instance.TryHostSession(sessionCode, Instance._currentMode);
            });
            
        }

        public void OnChangedMode(int idx)
        {
            Instance._currentMode = (SessionDef.Mode)idx;
        }

        public void CloseButton()
        {
            ui.enabled = false;
        }
    }
}