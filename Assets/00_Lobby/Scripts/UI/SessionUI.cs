using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using TMPro;
using UnityEngine;

namespace EyeMoT.Fusion
{
    public class SessionUI : SceneSingleton<SessionUI>
    {
        [Header("Resources")]
        [SerializeField] private PlayerItemUI _playerItemPrefab;
        [SerializeField] private Transform[] _playerItemHolders;
        [SerializeField] private TMP_Text _spectatorCountText;
        [SerializeField] private GameObject _hidePanel;

        private Transform[][] _playerItemParents;
        private Dictionary<PlayerRef, PlayerItemUI> _playerItems = new Dictionary<PlayerRef, PlayerItemUI>();

        void RegisterEvents()
        {
            PlayerRegistry.OnPlayerRegistered -= OnPlayerRegistered;
            PlayerRegistry.OnPlayerRegistered += OnPlayerRegistered;
            PlayerRegistry.OnPlayerLeft -= OnPlayerLeft;
            PlayerRegistry.OnPlayerLeft += OnPlayerLeft;
        }

        void Start()
        {
            LobbyManager.OnInitAll -= RegisterEvents;
            LobbyManager.OnInitAll += RegisterEvents;
            LobbyManager.OnResetAll -= Reset;
            LobbyManager.OnResetAll += Reset;

            _playerItemParents = new Transform[_playerItemHolders.Length][];

            for (int i = 0; i < _playerItemHolders.Length; i++)
            {
                var children = new Transform[_playerItemHolders[i].childCount];   
                for (int j = 0; j < _playerItemHolders[i].childCount; j++)
                    children[j] = _playerItemHolders[i].GetChild(j);

                _playerItemParents[i] = children;
            }
        }

        void OnPlayerRegistered(NetworkRunner runner, PlayerRef pRef)
        {
            var plObj = PlayerRegistry.GetPlayer(pRef);
            Debug.Log($"<color=orange>[Fusion]</color> Player Registered: {plObj?.Nickname}, Team: {plObj?.Team}, IndexByTeam: {plObj?.IndexByTeam}");
            UpdateText();

            if(plObj.Team == PlayerRegistry.TeamState.None || plObj.Team == PlayerRegistry.TeamState.Spectator || plObj.IndexByTeam == 255)
                return;

            var teamitem = GetPlayerItem(pRef, plObj.Team, plObj.IndexByTeam);
            teamitem.Init(pRef, plObj.Nickname);
            plObj.OnReadyStateChanged += () => teamitem.SetReady(plObj.IsReady);
        }

        void OnPlayerLeft(NetworkRunner runner, PlayerRef pRef)
        {
            Debug.Log($"<color=orange>[Fusion]</color> Player Left: {pRef}");
            UpdateText();

            if (!_playerItems.TryGetValue(pRef, out var item))
                return;
                
            _playerItems.Remove(pRef);
            Destroy(item.gameObject);
        }

        void Reset()
        {
            foreach (var item in _playerItems.Values)
                Destroy(item.gameObject);
            _playerItems.Clear();
            _spectatorCountText.text = "0";
        }

        void UpdateText()
        {
            var spectatorCount = PlayerRegistry.CountWhere(t => t.Team == PlayerRegistry.TeamState.Spectator, true);
            _spectatorCountText.text = spectatorCount.ToString();
        }

        public void OnTeamSelectButtonClicked(int teamIndex)
        {
            var team = (PlayerRegistry.TeamState)(byte)teamIndex;
            PlayerObject.Local.Rpc_SetTeam(team);
            _hidePanel.SetActive(team == PlayerRegistry.TeamState.Spectator);
        }

        public void OnReadyButtonClicked()
        {
            PlayerObject.Local.Rpc_SetReadyState(!PlayerObject.Local.IsReady);
        }

        PlayerItemUI TrackItem(PlayerRef playerRef, PlayerItemUI item)
        {
            _playerItems.Add(playerRef, item);
            return item;
        }

        PlayerItemUI GetPlayerItem(PlayerRef playerRef, PlayerRegistry.TeamState team, int index)
        {
            return _playerItems.TryGetValue(playerRef, out var item) ? item : TrackItem(playerRef, Instantiate(_playerItemPrefab, _playerItemParents[(int)team][index]));
        }
    }
}