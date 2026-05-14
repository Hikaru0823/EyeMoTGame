using System;
using System.Linq;
using EyeMoT.Fusion;
using EyeMoT.Heatmap;
using Fusion;
using Fusion.Sockets;
using TMPro;
using UnityEngine;

namespace EyeMoT.Balloon
{
    public class ResultManager : SceneSingleton<ResultManager>
    {
        [Header("Resources")]
        [SerializeField] private TabManager _resultTabManager;
        [SerializeField] private GameObject _retryButton;
        [SerializeField] private TMP_Text _balloonCountText;
        [SerializeField] private SingleSelecterUI _heatmapSelecter;
        [SerializeField] private GameObject _notReceivedHeatmapPanel;
        [SerializeField] private ResultItemUI _resultPropertyPrefab;
        [SerializeField] private ResultItemUI _resultItemPrefab;
        [SerializeField] private Transform _resultItemContent;

        private RenderTexture[] _heatmapTextures = new RenderTexture[4];
        private bool[] _heatmapTextureReady = new bool[4]{false, false, false, false};
        private PlayerObject[] _heatmapPlayerOrder = Array.Empty<PlayerObject>();

        void Start()
        {
            LobbyManager.OnReliableDataReceivedEvent -= OnReliableDataReceived;
            LobbyManager.OnReliableDataReceivedEvent += OnReliableDataReceived;
        }
        
        public void StartRecordHeatmap(PlayerObject[] players)
        {
            _resultTabManager.OpenPanel("Score");

            if(PlayerObject.Local.Team != PlayerRegistry.TeamState.Spectator)
                HeatmapRenderer.Instance.StartHeatmap("01_Balloon", true);
            HeatmapRenderer.Instance.ClearHeatmap();
            _heatmapTextures = new RenderTexture[4];
            _heatmapTextureReady = new bool[4]{false, false, false, false};
            _balloonCountText.text = "× 0";
            _heatmapPlayerOrder = CreateLocalFirstPlayerOrder(players);
            _heatmapSelecter.SetItems(_heatmapPlayerOrder.Select(player => player.Nickname).ToArray(), 0);
        }

        public void StopRecordHeatmap()
        {
            if(PlayerObject.Local.Team != PlayerRegistry.TeamState.Spectator)
                HeatmapRenderer.Instance.StopHeatmap(false);
            HeatmapRenderer.Instance.VisibleHeatmap(false);
        }

        public void ShowResult()
        {
            foreach(var item in _resultItemContent.GetComponentsInChildren<ResultItemUI>())
            {
                Destroy(item.gameObject);
            }

            _retryButton.SetActive(PlayerObject.Local.Team != PlayerRegistry.TeamState.Spectator);

            if(PlayerObject.Local.Team != PlayerRegistry.TeamState.Spectator)
            {
                var heatmapResult = HeatmapRenderer.Instance.StopHeatmap();
                _heatmapTextures[PlayerObject.Local.IndexByTeam] = heatmapResult.HeatmapTexture;
                _heatmapTextureReady[PlayerObject.Local.IndexByTeam] = true;
                SendHeatmapTextureToServer(heatmapResult.HeatmapTexture);
            }

            var rankedPlayers = PlayerContent.Everyone
                    .OrderByDescending(kvp => kvp.NetwrokedBalloonCount)
                    .ToArray();
            
            Instantiate(_resultPropertyPrefab, _resultItemContent);
            for(int i = 0; i < rankedPlayers.Length; i++)
            {
                var player = rankedPlayers[i];
                var plObj = PlayerRegistry.GetPlayer(player.Object.InputAuthority);
                var item = Instantiate(_resultItemPrefab, _resultItemContent);
                int modeIdx = 0;
                if (LobbyManager.Instance.Runner.SessionInfo.Properties.TryGetValue("Mode", out SessionProperty modeProperty) && modeProperty.IsInt)
                    modeIdx = modeProperty;
                item.Init(plObj.Nickname, player.NetwrokedBalloonCount, (SessionDef.Mode)modeIdx == SessionDef.Mode.COLLABOLATION ? -1 : i);
            }
        }

        //UIHook
        public void GameRestart()
        {
            GameManager.Instance.GameRestart();
            HeatmapRenderer.Instance.VisibleHeatmap(false);
            LineBeam.Local?.Rpc_SetReadyState(true);
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

            int playerIndex = PlayerObject.Local != null ? PlayerObject.Local.IndexByTeam : 255;
            ReliableKey reliableKey = ReliableKey.FromInts(1, playerIndex, Time.frameCount, 0);

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
            if (dataType != 1)
            {
                return;
            }

            byte[] heatmapPngBytes = new byte[data.Count];
            Buffer.BlockCopy(data.Array, data.Offset, heatmapPngBytes, 0, data.Count);

            if (runner.IsServer && reserved == 0)
            {
                key = ReliableKey.FromInts(1, playerIndex, frameCount, 1);
                BroadcastHeatmapBytesToClients(runner, key, heatmapPngBytes, player);
                return;
            }

            RenderTexture heatmapTexture = DecodeImageBytesToRenderTexture(heatmapPngBytes);
            _heatmapTextures[playerIndex] = heatmapTexture;
            _heatmapTextureReady[playerIndex] = true;
            ShowHeatmapChange();

            Debug.Log($"<color=orange>[HeatMap]</color> Received heatmap from {player} index {playerIndex}: {heatmapPngBytes.Length} bytes");
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

            int playerIndex = _heatmapPlayerOrder[_heatmapSelecter.CurrentIdx].IndexByTeam;
            _balloonCountText.text = "× " + (PlayerContent.GetPlayer(_heatmapPlayerOrder[_heatmapSelecter.CurrentIdx].Ref)?.NetwrokedBalloonCount.ToString() ?? "0");
            if (!_heatmapTextureReady[playerIndex])
            {
                _notReceivedHeatmapPanel.SetActive(true);
                HeatmapRenderer.Instance.VisibleHeatmap(false);
                return;
            }
            _notReceivedHeatmapPanel.SetActive(false);
            HeatmapRenderer.Instance.VisibleHeatmap(true, _heatmapTextures[playerIndex]);
        }

        private PlayerObject[] CreateLocalFirstPlayerOrder(PlayerObject[] sortedPlayers)
        {
            PlayerObject localPlayer = PlayerObject.Local;
            if (localPlayer == null || localPlayer.Team == PlayerRegistry.TeamState.Spectator)
            {
                return sortedPlayers;
            }

            PlayerObject[] localFirstPlayers = new PlayerObject[sortedPlayers.Length];
            localFirstPlayers[0] = sortedPlayers[localPlayer.IndexByTeam];

            int targetIndex = 1;
            for (int i = 0; i < sortedPlayers.Length; i++)
            {
                if (i == localPlayer.IndexByTeam)
                    continue;

                localFirstPlayers[targetIndex] = sortedPlayers[i];
                targetIndex++;
            }

            return localFirstPlayers;
        }
    }
}
