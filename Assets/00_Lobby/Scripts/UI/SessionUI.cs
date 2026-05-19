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
        [SerializeField] private GameObject _readyButton;
        [SerializeField] private Transform[] _playerItemHolders;
        [SerializeField] private TMP_Text _spectatorCountText;
        [SerializeField] private GameObject _hidePanel;

        private Dictionary<PlayerRef, PlayerItemUI> _playerItems = new Dictionary<PlayerRef, PlayerItemUI>();

        void Init()
        {
            Reset();
            PlayerRegistry.OnPlayerRegistered -= OnPlayerRegistered;
            PlayerRegistry.OnPlayerRegistered += OnPlayerRegistered;
            PlayerRegistry.OnPlayerLeft -= OnPlayerLeft;
            PlayerRegistry.OnPlayerLeft += OnPlayerLeft;
        }

        void Start()
        {
            LobbyManager.OnInitAll -= Init;
            LobbyManager.OnInitAll += Init;

        }

        void OnPlayerRegistered(NetworkRunner runner, PlayerRef pRef)
        {
            var plObj = PlayerRegistry.GetPlayer(pRef);
            Debug.Log($"<color=orange>[Fusion]</color> Player Registered: {plObj?.Nickname}, Team: {plObj?.Team}, IndexByTeam: {plObj?.IndexByTeam}");
            _readyButton.SetActive(PlayerObject.Local.Team != PlayerRegistry.TeamState.Spectator);
            UpdateText();

            if(plObj.Team == PlayerRegistry.TeamState.None || plObj.Team == PlayerRegistry.TeamState.Spectator)
                return;

            var teamitem = GetPlayerItem(pRef, plObj.Index);
            var color = PlayerRegistry.TeamColor[(int)plObj.Team];
            teamitem.Init(pRef, plObj.Nickname, new Color(color.r, color.g, color.b, 0.6f));
            teamitem.SetReady(plObj.IsReady);
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
            var spectatorCount = PlayerRegistry.CountSpectators;
            _spectatorCountText.text = spectatorCount.ToString();
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

        PlayerItemUI GetPlayerItem(PlayerRef playerRef, int index)
        {
            return _playerItems.TryGetValue(playerRef, out var item) ? item : TrackItem(playerRef, Instantiate(_playerItemPrefab, _playerItemHolders[index]));
        }
    }
}