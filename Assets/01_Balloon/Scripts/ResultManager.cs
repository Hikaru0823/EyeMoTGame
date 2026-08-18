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
        [SerializeField] private AnalyzeItemUI _analyzeItemPrefab;
        [SerializeField] private Transform _analyzeItemHolder;
        [SerializeField] private TMP_Text[] _totalAnalyzeText;
        [SerializeField] private GameObject _analyzeButton;
        [SerializeField] private TabManager _resultTabManager;
        [SerializeField] private GameObject _retryButton;
        [SerializeField] private TMP_Text _balloonCountText;
        [SerializeField] private SingleSelecterUI _heatmapSelecter;
        [SerializeField] private Transform _teamResultItemHolder;
        [SerializeField] private TeamResultItemUI[] _teamResultItems;
        [SerializeField] private HeatmapTextureData[] _heatmapTextureData;
        [SerializeField] private GameObject _tablePanel;
        [SerializeField] private Graph.GraphGenerater[] _graphGeneraters;
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
                data._gazeLineImage.texture = null;
                data.IsReady = false;
                data.IsHeatmapReady = false;
                data.IsGazeLineReady = false;
                data.HeatmapProgress = 0f;
                data.GazeLineProgress = 0f;
                data._heatmapImage.enabled = false;
                data._gazeLineImage.enabled = false;
                data._noneReceivedPanel.SetActive(false);
                data._progressBar.fillAmount = 0f;
            }
        }

        public void StartRecordHeatmap(PlayerObject[] players)
        {
            
            _resultTabManager.OpenPanel("Score");

            if(PlayerObject.Local.Team != PlayerRegistry.TeamState.Spectator)
                RecordManager.Instance.StartRecord(SettingManager.Instance.GameData.ActiveRecord == 0);
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
                RecordManager.Instance.StopRecord();
        }

        public void SetAnalyze(GazeSessionResult result)
        {
            for (int j = 1; j < _analyzeItemHolder.childCount; j++)
            {
                Destroy(_analyzeItemHolder.GetChild(j).gameObject);
            }
            
            if(result == null)
                return;
            
            int i = 0;
            List<float> accuracyScores = new List<float>();
            List<float> stabilityScores = new List<float>();
            List<float> attentionScores = new List<float>();

            foreach (var analized in result.targetResults)
            {
                var item = Instantiate(_analyzeItemPrefab, _analyzeItemHolder);
                item.Init(analized, BalloonSpawnManager.Instance.balloonColorHistory[i]);
                accuracyScores.Add(analized.accuracyScore);
                stabilityScores.Add(analized.stabilityScore);
                attentionScores.Add(analized.attentionScore);
                i++;
                Debug.Log($"Reason: {analized.endReason}");
            }

            _graphGeneraters[0].GenerateGraph(accuracyScores, result.averageAccuracyScore, 1, BalloonSpawnManager.Instance.balloonColorHistory);
            _graphGeneraters[1].GenerateGraph(stabilityScores, result.averageStabilityScore, 1, BalloonSpawnManager.Instance.balloonColorHistory);
            _graphGeneraters[2].GenerateGraph(attentionScores, result.averageAttentionScore, 1, BalloonSpawnManager.Instance.balloonColorHistory);
            StartCoroutine(RefreshAnalyzeScroll());
            _totalAnalyzeText[0].text = result.averageAccuracyScore.ToString("F2");
            _totalAnalyzeText[1].text = result.averageStabilityScore.ToString("F2");
            _totalAnalyzeText[2].text = result.averageAttentionScore.ToString("F2");
        }

        public void ChangeAnalyzePanel(string panelName)
        {
            _tablePanel.SetActive(false);
            foreach (var graph in _graphGeneraters)
            {
                graph.gameObject.SetActive(false);
            }
            switch (panelName)
            {
                case "評価":
                    _tablePanel.SetActive(true);
                    break;
                case "正確性":
                    _graphGeneraters[0].gameObject.SetActive(true);
                    break;
                case "安定性":
                    _graphGeneraters[1].gameObject.SetActive(true);
                    break;
                case "注視性":
                    _graphGeneraters[2].gameObject.SetActive(true);
                    break;
            }
        }

        private IEnumerator RefreshAnalyzeScroll()
        {
            yield return null;

            Canvas.ForceUpdateCanvases();
            var content = (RectTransform)_analyzeItemHolder;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

            ScrollRect scrollRect = content.GetComponentInParent<ScrollRect>();
            if (scrollRect != null)
                scrollRect.horizontalNormalizedPosition = 0f;
        }

        public void ShowResult()
        {
            _analyzeButton.SetActive(GameManager.Instance.IsAnalyze);
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
                RecordManager.Instance.StopRecord();
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
                teamResultItem.AddPlayerResult(plObj.Nickname, player.NetwrokedBalloonCount, plObj.PlayerImage);
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

        private void SendHeatmapTextureToServer(HeatmapResult heatmap)
        {
            if (heatmap == null)
            {
                return;
            }

            HeatmapTextureData textureData =
                _heatmapTextureData[PlayerObject.Local.Index];
            textureData._heatmapImage.texture = heatmap.HeatmapTexture;
            textureData._gazeLineImage.texture = heatmap.GazeLineTexture;
            textureData.IsHeatmapReady = true;
            textureData.IsGazeLineReady = true;
            textureData.HeatmapProgress = 1f;
            textureData.GazeLineProgress = 1f;
            textureData.IsReady = true;

            SendTextureToServer(heatmap.HeatmapTexture, false);
            SendTextureToServer(heatmap.GazeLineTexture, true);
        }

        private void SendTextureToServer(RenderTexture texture, bool isGazeLine)
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
            ReliableKey reliableKey = isGazeLine
                ? ReliableKeys.GetGazeLineKey(playerIndex, false)
                : ReliableKeys.GetHeatMapKey(playerIndex, false);

            runner.SendReliableDataToServer(reliableKey, pngBytes);
        }

        private byte[] EncodeRenderTextureToPng(RenderTexture texture)
        {
            RenderTexture previousActive = RenderTexture.active;
            Texture2D readableTexture = new Texture2D(
                texture.width,
                texture.height,
                TextureFormat.RGBA32,
                false,
                true);

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

            Texture2D texture = new Texture2D(
                2,
                2,
                TextureFormat.RGBA32,
                false,
                true);
            if (!texture.LoadImage(imageBytes))
            {
                Destroy(texture);
                return null;
            }

            RenderTexture renderTexture = new RenderTexture(
                texture.width,
                texture.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            renderTexture.wrapMode = TextureWrapMode.Clamp;
            renderTexture.filterMode = FilterMode.Bilinear;
            renderTexture.Create();

            Graphics.Blit(texture, renderTexture);
            Destroy(texture);

            return renderTexture;
        }

        private void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
        {
            key.GetInts(out int dataType, out int playerIndex, out int frameCount, out int reserved);
            bool isHeatmap = dataType == ReliableKeys.HeatmapIndex;
            bool isGazeLine = dataType == ReliableKeys.GazeLineIndex;
            if (!isHeatmap && !isGazeLine)
            {
                return;
            }

            byte[] pngBytes = new byte[data.Count];
            Buffer.BlockCopy(data.Array, data.Offset, pngBytes, 0, data.Count);

            if (runner.IsServer && reserved == 0)
            {
                key = isGazeLine
                    ? ReliableKeys.GetGazeLineKey(playerIndex, true)
                    : ReliableKeys.GetHeatMapKey(playerIndex, true);
                BroadcastTextureBytesToClients(runner, key, pngBytes, player);
                return;
            }

            RenderTexture texture = DecodeImageBytesToRenderTexture(pngBytes);
            HeatmapTextureData textureData = _heatmapTextureData[playerIndex];

            if (isGazeLine)
            {
                textureData._gazeLineImage.texture = texture;
                textureData.IsGazeLineReady = true;
                textureData.GazeLineProgress = 1f;
            }
            else
            {
                textureData._heatmapImage.texture = texture;
                textureData.IsHeatmapReady = true;
                textureData.HeatmapProgress = 1f;
            }

            textureData.IsReady =
                textureData.IsHeatmapReady && textureData.IsGazeLineReady;
            UpdateTextureTransferProgress(textureData);
            ShowHeatmapChange();

            string textureType = isGazeLine ? "gaze line" : "heatmap";
            Debug.Log($"<color=orange>[HeatMap]</color> Received {textureType} from {player} index {playerIndex}: {pngBytes.Length} bytes");
        }

        private void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
        {
            key.GetInts(out int dataType, out int playerIndex, out int frameCount, out int reserved);
            bool isHeatmap = dataType == ReliableKeys.HeatmapIndex;
            bool isGazeLine = dataType == ReliableKeys.GazeLineIndex;
            if (!isHeatmap && !isGazeLine)
            {
                return;
            }

            HeatmapTextureData textureData = _heatmapTextureData[playerIndex];
            if (isGazeLine)
                textureData.GazeLineProgress = progress;
            else
                textureData.HeatmapProgress = progress;

            UpdateTextureTransferProgress(textureData);
        }

        private void UpdateTextureTransferProgress(HeatmapTextureData textureData)
        {
            textureData._progressBar.fillAmount =
                (textureData.HeatmapProgress + textureData.GazeLineProgress) * 0.5f;
        }

        private void BroadcastTextureBytesToClients(NetworkRunner runner, ReliableKey key, byte[] pngBytes, PlayerRef excludePlayer = default)
        {
            foreach (PlayerObject playerObject in PlayerRegistry.Everyone)
            {
                if (playerObject == null || !playerObject.Ref.IsRealPlayer || playerObject.Ref == excludePlayer)
                {
                    continue;
                }

                runner.SendReliableDataToPlayer(playerObject.Ref, key, pngBytes);
            }
        }
        public void ShowHeatmapChange()
        {
            if(_resultTabManager.GetCurrentPanelName() != "Heatmap")
                return;

            foreach (var data in _heatmapTextureData)
            {
                data._heatmapPanel.SetActive(false);
            }

            int playerIndex = _heatmapPlayer_FirstIsLocal[_heatmapSelecter.CurrentIdx].Index;
            _balloonCountText.text = "× " + (PlayerContent.GetPlayer(_heatmapTextureData[playerIndex].Player.Ref)?.NetwrokedBalloonCount.ToString() ?? "0");
            _heatmapTextureData[playerIndex]._heatmapPanel.SetActive(true);
            _heatmapTextureData[playerIndex]._heatmapImage.enabled = _heatmapTextureData[playerIndex].IsReady;
            _heatmapTextureData[playerIndex]._gazeLineImage.enabled = _heatmapTextureData[playerIndex].IsReady;
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
        [SerializeField] public GameObject _heatmapPanel;
        [SerializeField] public RawImage _heatmapImage;
        [SerializeField] public RawImage _gazeLineImage;
        [SerializeField] public GameObject _noneReceivedPanel;
        [SerializeField] public Image _progressBar;
        [SerializeField] public bool IsReady;
        [NonSerialized] public bool IsHeatmapReady;
        [NonSerialized] public bool IsGazeLineReady;
        [NonSerialized] public float HeatmapProgress;
        [NonSerialized] public float GazeLineProgress;
        public PlayerObject Player;
    }
}
