using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EyeMoT.Fusion;
using EyeMoT.Heatmap;
using Fusion;
using Fusion.Sockets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EyeMoT.Balloon
{
    public class ResultManager : SceneSingleton<ResultManager>
    {
        [Header("Resources")]
        [SerializeField] private TabManager _resultTabManager;
        [SerializeField] private GameObject _retryButton;
        [SerializeField] private TMP_Text _balloonCountText;
        [SerializeField] private SingleSelecterUI _heatmapSelecter;
        [SerializeField] private Transform _teamResultItemHolder;
        [SerializeField] private TeamResultItemUI[] _teamResultItems;
        [SerializeField] private HeatmapTextureData[] _heatmapTextureData;
        private PlayerObject[] _heatmapPlayer_FirstIsLocal;
        void Start()
        {
            LobbyManager.OnReliableDataReceivedEvent -= OnReliableDataReceived;
            LobbyManager.OnReliableDataReceivedEvent += OnReliableDataReceived;
            LobbyManager.OnReliableDataProgressEvent -= OnReliableDataProgress;
            LobbyManager.OnReliableDataProgressEvent += OnReliableDataProgress;
        }

        void ResetHeatmapData()
        {
            foreach (var data in _heatmapTextureData)
            {
                data._heatmapImage.texture = null;
                data.IsReady = false;
                data._heatmapImage.enabled = false;
                data._noneReceivedPanel.SetActive(false);
                data._progressBar.fillAmount = 0f;
            }
        }

        public void StartRecordHeatmap(PlayerObject[] players)
        {
            _resultTabManager.OpenPanel("Score");

            HeatmapRenderer.Instance.ClearHeatmap();
            if(PlayerObject.Local.Team != PlayerRegistry.TeamState.Spectator)
                HeatmapRenderer.Instance.StartHeatmap("01_Balloon", true);
            _balloonCountText.text = "× 0";
            ResetHeatmapData();
            for (int i = 0; i < players.Length; i++)
            {
                _heatmapTextureData[i].Player = players[i];
            }
            _heatmapPlayer_FirstIsLocal = CreateLocalFirstPlayerOrder(players);
            _heatmapSelecter.SetItems(_heatmapPlayer_FirstIsLocal.Select(player => player.Nickname).ToArray(), 0);
        }

        public void StopRecordHeatmap()
        {
            if(PlayerObject.Local.Team != PlayerRegistry.TeamState.Spectator)
                HeatmapRenderer.Instance.StopHeatmap(false);
        }

        public void ShowResult()
        {
            StartCoroutine(ShowResultRoutine());
        }

        private IEnumerator ShowResultRoutine()
        {
            foreach (var item in _teamResultItems)
            {
                item.ClearPlayerResults();
            }

            yield return null;

            _retryButton.SetActive(PlayerObject.Local.Team != PlayerRegistry.TeamState.Spectator);

            if (PlayerObject.Local.Team != PlayerRegistry.TeamState.Spectator)
            {
                var heatmapResult = HeatmapRenderer.Instance.StopHeatmap();
                _heatmapTextureData[PlayerObject.Local.Index]._heatmapImage.texture = heatmapResult.HeatmapTexture;
                _heatmapTextureData[PlayerObject.Local.Index].IsReady = true;
                SendHeatmapTextureToServer(heatmapResult.HeatmapTexture);
            }

            var rankedPlayers = PlayerContent.Everyone
                .OrderByDescending(kvp => kvp.NetwrokedBalloonCount)
                .ToArray();

            var teamScores = new Dictionary<PlayerRegistry.TeamState, int>()
            {
                { PlayerRegistry.TeamState.Red, 0 },
                { PlayerRegistry.TeamState.Blue, 0 },
                { PlayerRegistry.TeamState.Green, 0 },
                { PlayerRegistry.TeamState.Yellow, 0 },
            };

            for (int i = 0; i < rankedPlayers.Length; i++)
            {
                var player = rankedPlayers[i];
                var plObj = PlayerRegistry.GetPlayer(player.Object.InputAuthority);

                var teamResultItem = _teamResultItems[(int)plObj.Team];

                teamScores[plObj.Team] += player.NetwrokedBalloonCount;
                teamResultItem.AddPlayerResult(plObj.Nickname, player.NetwrokedBalloonCount);
            }

            var sortedTeamScores = teamScores.OrderByDescending(s => s.Value).ToArray();

            var topScore = sortedTeamScores[0].Value;
            for (int i = 0; i < _teamResultItems.Length; i++)
            {
                var team = sortedTeamScores[i].Key;
                var item = _teamResultItems[(int)team];

                item.Init(sortedTeamScores[i].Value, topScore == sortedTeamScores[i].Value ? 0 : 1);
                item.transform.SetAsLastSibling();
    }

            yield return null;

            Canvas.ForceUpdateCanvases();

            foreach (var item in _teamResultItems)
            {
                item.RebuildLayout();
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(
                _teamResultItemHolder.GetComponent<RectTransform>()
            );

            Canvas.ForceUpdateCanvases();
        }

        //UIHook
        public void GameRestart()
        {
            GameManager.Instance.GameRestart();
            LineBeam.Local?.Rpc_SetReadyState(true);
        }
        //UIHook
        public void OnHeatmapButtonClicked()
        {
            _resultTabManager.OpenPanel("Heatmap");
            ShowHeatmapChange();
        }


        private void SendHeatmapTextureToServer(RenderTexture texture)
        {
            NetworkRunner runner = LobbyManager.Instance != null ? LobbyManager.Instance.Runner : null;
            if (runner == null || !runner.IsRunning || texture == null)
            {
                return;
            }

            byte[] pngBytes = EncodeRenderTextureToPng(texture);
            if (pngBytes == null || pngBytes.Length == 0)
            {
                return;
            }

            int playerIndex = PlayerObject.Local != null ? PlayerObject.Local.Index : 255;
            ReliableKey reliableKey = ReliableKeys.GetHeatMapKey(playerIndex, false);

            runner.SendReliableDataToServer(reliableKey, pngBytes);
        }

        private byte[] EncodeRenderTextureToPng(RenderTexture texture)
        {
            RenderTexture previousActive = RenderTexture.active;
            Texture2D readableTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);

            try
            {
                RenderTexture.active = texture;
                readableTexture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                readableTexture.Apply();
                return readableTexture.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previousActive;
                Destroy(readableTexture);
            }
        }

        private RenderTexture DecodeImageBytesToRenderTexture(byte[] imageBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                return null;
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(imageBytes))
            {
                Destroy(texture);
                return null;
            }

            RenderTexture renderTexture = new RenderTexture(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
            renderTexture.Create();

            Graphics.Blit(texture, renderTexture);
            Destroy(texture);

            return renderTexture;
        }

        private void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
        {
            key.GetInts(out int dataType, out int playerIndex, out int frameCount, out int reserved);
            if (dataType != ReliableKeys.HeatmapIndex)
            {
                return;
            }

            byte[] heatmapPngBytes = new byte[data.Count];
            Buffer.BlockCopy(data.Array, data.Offset, heatmapPngBytes, 0, data.Count);

            if (runner.IsServer && reserved == 0)
            {
                key = ReliableKeys.GetHeatMapKey(playerIndex, true);
                BroadcastHeatmapBytesToClients(runner, key, heatmapPngBytes, player);
                return;
            }

            RenderTexture heatmapTexture = DecodeImageBytesToRenderTexture(heatmapPngBytes);
            _heatmapTextureData[playerIndex]._heatmapImage.texture = heatmapTexture;
            _heatmapTextureData[playerIndex].IsReady = true;
            _heatmapTextureData[playerIndex]._progressBar.fillAmount = 1f;
            ShowHeatmapChange();

            Debug.Log($"<color=orange>[HeatMap]</color> Received heatmap from {player} index {playerIndex}: {heatmapPngBytes.Length} bytes");
        }

        private void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
        {
            key.GetInts(out int dataType, out int playerIndex, out int frameCount, out int reserved);
            if (dataType != ReliableKeys.HeatmapIndex)
            {
                return;
            }

            //Debug.Log($"<color=yellow>[HeatMap]</color> Receiving heatmap from {player} index {playerIndex}: {progress * 100f}%");
            _heatmapTextureData[playerIndex]._progressBar.fillAmount = progress;
        }

        private void BroadcastHeatmapBytesToClients(NetworkRunner runner, ReliableKey key, byte[] heatmapPngBytes, PlayerRef excludePlayer = default)
        {
            foreach (PlayerObject playerObject in PlayerRegistry.Everyone)
            {
                if (playerObject == null || !playerObject.Ref.IsRealPlayer || playerObject.Ref == excludePlayer)
                {
                    continue;
                }

                runner.SendReliableDataToPlayer(playerObject.Ref, key, heatmapPngBytes);
            }
        }
        public void ShowHeatmapChange()
        {
            if(_resultTabManager.GetCurrentPanelName() != "Heatmap")
                return;

            foreach (var data in _heatmapTextureData)
                data._heatmapImage.gameObject.SetActive(false);

            int playerIndex = _heatmapPlayer_FirstIsLocal[_heatmapSelecter.CurrentIdx].Index;
            _balloonCountText.text = "× " + (PlayerContent.GetPlayer(_heatmapTextureData[playerIndex].Player.Ref)?.NetwrokedBalloonCount.ToString() ?? "0");
            _heatmapTextureData[playerIndex]._heatmapImage.gameObject.SetActive(true);
            _heatmapTextureData[playerIndex]._heatmapImage.enabled = _heatmapTextureData[playerIndex].IsReady;
            _heatmapTextureData[playerIndex]._noneReceivedPanel.SetActive(!_heatmapTextureData[playerIndex].IsReady);
        }

        private PlayerObject[] CreateLocalFirstPlayerOrder(PlayerObject[] sortedPlayers)
        {
            PlayerObject localPlayer = PlayerObject.Local;
            if (localPlayer == null || localPlayer.Team == PlayerRegistry.TeamState.Spectator)
            {
                return sortedPlayers;
            }

            PlayerObject[] localFirstPlayers = new PlayerObject[sortedPlayers.Length];
            localFirstPlayers[0] = sortedPlayers[localPlayer.Index];

            int targetIndex = 1;
            for (int i = 0; i < sortedPlayers.Length; i++)
            {
                if (i == localPlayer.Index)
                    continue;

                localFirstPlayers[targetIndex] = sortedPlayers[i];
                targetIndex++;
            }

            return localFirstPlayers;
        }
    }

    [Serializable]
    public class HeatmapTextureData
    {
        [SerializeField] public RawImage _heatmapImage;
        [SerializeField] public GameObject _noneReceivedPanel;
        [SerializeField] public Image _progressBar;
        [SerializeField] public bool IsReady;
        public PlayerObject Player;
    }
}
