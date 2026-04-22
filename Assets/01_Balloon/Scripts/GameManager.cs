using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EyeMoT.Fusion;
using EyeMoT.Heatmap;
using Fusion;
using Fusion.Sockets;
using KanKikuchi.AudioManager;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem.Interactions;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EyeMoT.Balloon
{
    public class GameManager : SceneSingleton<GameManager>
    {
        public static Color[] TeamColor = new Color[4]
        {
            Color.red,
            Color.blue,
            Color.yellow,
            Color.green,
        };
        [Header("Resources")]
        [SerializeField] private PlayerContent _playerContentPrefab;
        [SerializeField] private LineBeam _playerPrefab;
        [SerializeField] private TMP_Text _gameTimeText;
        [SerializeField] private TMP_Text _balloonCountText;
        [SerializeField] private Animator _resultPanel;
        [SerializeField] private GameObject _retryButton;
        [SerializeField] private TabManager _mainTabManager;
        [SerializeField] private SingleSelecterUI _heatmapSelecter;
        [SerializeField] private GameObject _notReceivedHeatmapPanel;
        private bool _isStart = false;
        private float _time = 0f;
        private int _balloonCount = 0;
        private RenderTexture[] _heatmapTextures = new RenderTexture[4];
        private bool[] _heatmapTextureReady = new bool[4]{false, false, false, false};

        void Start()
        {
            LobbyManager.OnReleaseAll -= GameExit;
            LobbyManager.OnReleaseAll += GameExit;
            Timer.OnTimeUpdated -= UpdateGameTime;
            Timer.OnTimeUpdated += UpdateGameTime;
            PlayerContent.OnAllReady -= GameStart;
            PlayerContent.OnAllReady += GameStart;
            LobbyManager.OnReliableDataReceivedEvent -= OnReliableDataReceived;
            LobbyManager.OnReliableDataReceivedEvent += OnReliableDataReceived;

            BalloonSpawnManager.Instance.OnBalloonDestroyed += UpdateBalloonCount;

            LobbyManager.Instance.TrySingleSession(Init);
        }

        void Init()
        {
            _mainTabManager.OpenPanel("Title");
            BGMManager.Instance.Play(BGMPath.BALLOON_TITLE, volumeRate: 0.5f);
            PreviewManager.Instance.ResetBalloon();
        }

        public void GameStart()
        {
            _mainTabManager.OpenPanel("Game");
            if(PlayerObject.Local.Team != PlayerRegistry.TeamState.Spectator)
                HeatmapRenderer.Instance.StartHeatmap("01_Balloon", true);
            BGMManager.Instance.Play(BGMPath.BALLOON_GAME, volumeRate: 0.5f);

            var players = PlayerRegistry.Players
                    .Where(kvp => kvp.Team == PlayerRegistry.TeamState.TeamA && kvp.IndexByTeam != 255)
                    .OrderBy(kvp => kvp.IndexByTeam)
                    .ToArray();

            _gameTimeText.text = SettingManager.Instance.GameData.GameTime.ToString("F1") + "s";
            _balloonCountText.text = "×" + 0;
            _balloonCount = 0;
            _time = 0f;
            _isStart = true;
            _heatmapTextures = new RenderTexture[4];
            _heatmapTextureReady = new bool[4]{false, false, false, false};
            _heatmapSelecter.SetItems(players.Select(kvp => kvp.Nickname).ToArray(), 0);

            if(!LobbyManager.Instance.Runner.IsServer) return;

            Timer.Instance.TickStarted = LobbyManager.Instance.Runner.Tick;

            BalloonSpawnManager.Instance.SpawnInitialBalloons(SettingManager.Instance.GameData.BalloonGeneratePatern);

            if(PlayerContent.Instance == null)
                LobbyManager.Instance.Runner.Spawn(_playerContentPrefab, Vector3.zero, Quaternion.identity);

            if(PlayerContent.CountAll > 0) return;

            //var offset = players.Length == 1 ? 0 : (players.Length - 1) * 2 - 1;
            var offset = 1- players.Length;

            foreach(var player in players)
            {
                var obj = LobbyManager.Instance.Runner.Spawn(_playerPrefab, new Vector3(offset + player.IndexByTeam * 2, 0, 4.3f), Quaternion.identity, player.Ref);
                LobbyManager.Instance.Runner.SetPlayerObject(player.Ref, obj.GetComponent<NetworkObject>());
                PlayerContent.Instance.Server_Add(player.Ref, obj.GetComponent<LineBeam>());    
            }
        }

        public void GameEnd()
        {
            _resultPanel.Play(StaticData.PANEL_FADE_IN);
            _retryButton.SetActive(PlayerObject.Local.Team != PlayerRegistry.TeamState.Spectator);
            if(PlayerObject.Local.Team != PlayerRegistry.TeamState.Spectator)
            {
                var heatmapResult = HeatmapRenderer.Instance.StopHeatmap();
                _heatmapTextures[PlayerObject.Local.IndexByTeam] = heatmapResult.HeatmapTexture;
                _heatmapTextureReady[PlayerObject.Local.IndexByTeam] = true;
                SendHeatmapTextureToServer(heatmapResult.HeatmapTexture);
            }
            _gameTimeText.text = "0.0s";
            BalloonSpawnManager.Instance.ResetBalloons();
        }

        public void GameRestart()
        {
            _resultPanel.Play(StaticData.PANEL_FADE_OUT);
            HeatmapRenderer.Instance.VisibleHeatmap(false);

            LineBeam.Local?.Rpc_SetReadyState(true);
        }

        public void ReturnTitle()
        {
            LobbyManager.Instance.Quit();
        }

        public void GameExit()
        {
            if(_resultPanel.GetCurrentAnimatorStateInfo(0).IsName(StaticData.PANEL_FADE_IN))
                _resultPanel.Play(StaticData.PANEL_FADE_OUT);
            var heatmapResult = HeatmapRenderer.Instance.StopHeatmap(false);
            HeatmapRenderer.Instance.VisibleHeatmap(false);
            BGMManager.Instance.Play(BGMPath.BALLOON_TITLE, volumeRate: 0.5f);
            _isStart = false;
            BalloonSpawnManager.Instance.ResetBalloons();

            Init();
        }

        private void UpdateBalloonCount()
        {
            if(!_isStart) return;
            _balloonCount++;
            _balloonCountText.text = "×" + _balloonCount;
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

        private void UpdateGameTime(float time)
        {
            if(!_isStart) return;
            _time = SettingManager.Instance.GameData.GameTime - time;
            //_time = 5 - time;
            _gameTimeText.text = _time.ToString("F1") + "s";

            if(_time <= 0)
            {
                _isStart = false;
                GameEnd();
            }
        }

        public void LoadLobbyScene()
        {
            //SceneManager.LoadScene("00_Lobby", LoadSceneMode.Additive);
            LobbyManager.OnGameStart -= GameStart;
            LobbyManager.OnGameStart += GameStart;
            LobbyManager.Instance._mainTabManager.OpenPanel("Network");
            LobbyManager.Instance.TryJoinLobby();
        }

        public void ShowHeatmapChange()
        {
            if(_resultPanel.GetCurrentAnimatorStateInfo(0).IsName(StaticData.PANEL_FADE_OUT)) return;

            if(!_heatmapTextureReady[_heatmapSelecter.CurrentIdx])
            {
                _notReceivedHeatmapPanel.SetActive(true);
                HeatmapRenderer.Instance.VisibleHeatmap(false);
                return;
            }
            _notReceivedHeatmapPanel.SetActive(false);
            HeatmapRenderer.Instance.VisibleHeatmap(true, _heatmapTextures[_heatmapSelecter.CurrentIdx]);
        }
    }
}
