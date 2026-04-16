using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using TMPro;
using UnityEngine;

namespace EyeMoT.Fusion
{
    public class TeamSelectUI : SceneSingleton<TeamSelectUI>
    {
        [Header("Resources")]
        [SerializeField] private PlayerItemUI _teamPlayerItemPrefab;
        [SerializeField] private TMP_Text[] _playerCountTexts;
        [SerializeField] private TMP_Text _spectatorCountText;
        [SerializeField] private Transform[] _teamPlayerItemHolders;
        [SerializeField] private TabManager _tabManager;

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
            foreach (var teamText in _playerCountTexts)
                teamText.text = "0";
            _spectatorCountText.text = "0";
        }

        void OnPlayerRegistered(NetworkRunner runner, PlayerRef pRef)
        {
            var plObj = PlayerRegistry.GetPlayer(pRef);

            UpdateText();

            if(plObj.Team == PlayerRegistry.TeamState.None || plObj.Team == PlayerRegistry.TeamState.Spectator || plObj.IndexByTeam == 255)
                return;

            var teamitem = GetPlayerItem(pRef, plObj.Team);
            teamitem.Init(pRef, plObj.Nickname);
        }

        void OnPlayerLeft(NetworkRunner runner, PlayerRef pRef)
        {
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
            foreach (var teamText in _playerCountTexts)
            {
                teamText.text = "0";
            }
            _spectatorCountText.text = "0";
        }

        void UpdateText()
        {
            int idx = 0;
            foreach (var teamText in _playerCountTexts)
            {
                var team = (PlayerRegistry.TeamState)idx;
                var count = PlayerRegistry.CountWhere(t => t.Team == team);
                teamText.text = count.ToString();
                idx++;
            }
            var spectatorCount = PlayerRegistry.CountWhere(t => t.Team == PlayerRegistry.TeamState.Spectator);
            _spectatorCountText.text = spectatorCount.ToString();
        }

        public void OnTeamSelectButtonClicked(int teamIndex)
        {
            PlayerObject.Local.Rpc_SetTeam((PlayerRegistry.TeamState)(byte)teamIndex);
            _tabManager.OpenPanel("Network_Session");
        }

        PlayerItemUI TrackItem(PlayerRef playerRef, PlayerItemUI item)
        {
            _playerItems.Add(playerRef, item);
            return item;
        }

        PlayerItemUI GetPlayerItem(PlayerRef playerRef, PlayerRegistry.TeamState team)
        {
            return _playerItems.TryGetValue(playerRef, out var item) ? item : TrackItem(playerRef, Instantiate(_teamPlayerItemPrefab, _teamPlayerItemHolders[(int)team]));
        }
    }
}