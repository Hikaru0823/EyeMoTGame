using System;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EyeMoT.Balloon;

namespace EyeMoT.Fusion
{
    public class AnalyzeModePopupUI : SceneSingleton<AnalyzeModePopupUI>
    {
        [Header("Resources")]
        [SerializeField] private Canvas ui;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text description;
        [SerializeField] private Button okButton;
        [SerializeField] private Button noButton;
        [SerializeField] private Image icon;
        [SerializeField] private MultipleSelecterUI selecterUI;

        public BalloonSpawnManager.GenerationPatern _currentMode = BalloonSpawnManager.GenerationPatern.Fix;

        public static void OnVisible(string title, string description)
        {
            Instance.selecterUI.OnButtonClicked(1);
            Instance.noButton.gameObject.SetActive(true);
            Instance.title.text = title;
            Instance.description.text = description;

            Instance.ui.enabled = true;
            Cursor.lockState = CursorLockMode.None;

            Instance.okButton.onClick.RemoveAllListeners();
            Instance.okButton.onClick.AddListener(() =>
            {
                Instance.CloseButton();
                GameManager.Instance.GameStart();
            });
            
        }

        public void OnChangedMode(int idx)
        {
            Instance._currentMode = (BalloonSpawnManager.GenerationPatern)idx;
        }

        public void CloseButton()
        {
            ui.enabled = false;
        }
    }
}