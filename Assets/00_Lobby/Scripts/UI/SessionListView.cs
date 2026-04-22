using Fusion;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace EyeMoT.Fusion
{
    public class SessionListView : MonoBehaviour
    {
        [Header("Resources")]
        [SerializeField] private SessionItemUI _sessionItemPrefab;
        [SerializeField] private Transform _sessionItemHolder;
        [SerializeField] private TMP_Text _noSessionText;

        readonly List<SessionItemUI> _sessionItems = new List<SessionItemUI>();

        public void UpdateSessions(List<SessionInfo> sessionList)
        {
            if (_noSessionText != null)
            {
                _noSessionText.enabled = sessionList.Count == 0;
            }

            for (int i = _sessionItems.Count - 1; i >= 0; i--)
            {
                SessionItemUI item = _sessionItems[i];
                if (item == null || !sessionList.Any(info => info.Name == item.SessionName))
                {
                    if (item != null)
                    {
                        Destroy(item.gameObject);
                    }

                    _sessionItems.RemoveAt(i);
                }
            }

            foreach (SessionInfo info in sessionList)
            {
                bool isFull = info.PlayerCount >= info.MaxPlayers;
                bool canJoin = info.IsOpen && !isFull;
                GetSessionItem(info.Name).Init(info.Name, info.PlayerCount, canJoin);
            }
        }

        public SessionDef.Name? GetAvailableSessionName()
        {
            HashSet<SessionDef.Name> usedNames = new HashSet<SessionDef.Name>();

            foreach (SessionItemUI item in _sessionItems)
            {
                if (item == null || string.IsNullOrEmpty(item.SessionName))
                {
                    continue;
                }

                string[] parts = item.SessionName.Split('_');
                string rawName = parts[parts.Length - 1];
                if (Enum.TryParse(rawName, out SessionDef.Name sessionName))
                {
                    usedNames.Add(sessionName);
                }
            }

            foreach (SessionDef.Name sessionName in Enum.GetValues(typeof(SessionDef.Name)))
            {
                if (!usedNames.Contains(sessionName))
                {
                    return sessionName;
                }
            }

            return null;
        }

        SessionItemUI TrackItem(SessionItemUI item)
        {
            _sessionItems.Add(item);
            return item;
        }

        SessionItemUI GetSessionItem(string sessionName)
        {
            return _sessionItems.FirstOrDefault(item => item.SessionName == sessionName) ??
                   TrackItem(Instantiate(_sessionItemPrefab, _sessionItemHolder));
        }
    }
}
