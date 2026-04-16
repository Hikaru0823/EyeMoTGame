using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KanKikuchi.AudioManager;
using UnityEngine.SceneManagement;

namespace EyeMoT.Fusion
{
    public class SessionItemUI : MonoBehaviour
    {
        [Header("Resources")]
        [SerializeField] private Image _sessionIcon;
        [SerializeField] private TMP_Text _playerCount;
        [SerializeField] private Image _statusIcon;
        [SerializeField] private Button[] _joinButtons;
        [HideInInspector] public string SessionName;
        [HideInInspector] public SessionDef.Name SessionDefName;

        public void Init(string sessionName, int Players, bool isOpen)
        {
            foreach (var joinButton in _joinButtons)
                joinButton.interactable = isOpen;

            SessionDef.Name sessionDefName = (SessionDef.Name)System.Enum.Parse(typeof(SessionDef.Name), sessionName.Replace(SceneManager.GetActiveScene().name + "_", ""));
            SessionDefName = sessionDefName;
            LobbyManager.Instance.SessionHolder.TryGet(sessionDefName, out SessionData data);
            _sessionIcon.sprite = data.Sprite;
            SessionName = sessionName;
            _playerCount.text = $"{Players}";
            _statusIcon.enabled = !isOpen;
        }

        public void Join()
        {
            LobbyManager.Instance.TryJoinSession(SessionName);
        }

    }
}