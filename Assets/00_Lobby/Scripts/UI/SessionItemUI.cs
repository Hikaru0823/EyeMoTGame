using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KanKikuchi.AudioManager;

namespace EyeMoT.Fusion
{
    public class SessionItemUI : MonoBehaviour
    {
        [Header("Resources")]
        [SerializeField] private Image _sessionIcon;
        [SerializeField] private TMP_Text _playerCount;
        [SerializeField] private GameObject _statusIcon;
        [SerializeField] private Button[] _joinButtons;
        [SerializeField] private GameObject[] _modeStatuses; // 0 : Collabo, 1 : Mustch;
        [HideInInspector] public string SessionName{get; private set;}
        [HideInInspector] public SessionDef.Name SessionDefName;

        public void Init(string sessionName, int Players, bool isOpen, int modeIdx)
        {
            foreach (var joinButton in _joinButtons)
                joinButton.interactable = isOpen;

            SessionDef.Name sessionDefName = SessionCodeUtility.ParseSessionName(sessionName);
            SessionDefName = sessionDefName;
            LobbyManager.Instance.SessionHolder.TryGet(sessionDefName, out SessionData data);
            _sessionIcon.sprite = data.Sprite;
            SessionName = sessionName;
            _playerCount.text = $"{Players}";
            _statusIcon.SetActive(!isOpen);

            foreach(var statusObj in _modeStatuses)
                statusObj.SetActive(false);
            _modeStatuses[modeIdx].SetActive(true);
        }

        public void Join()
        {
            LobbyManager.Instance.TryJoinSession(SessionName);
        }

    }
}
