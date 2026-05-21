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
using UnityEngine;
using UnityEngine.InputSystem.Interactions;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EyeMoT.Balloon
{
    public class GameManager : SceneSingleton<GameManager>
    {
        [Header("Resources")]
        [SerializeField] private PlayerContent _playerContentPrefab;
        [SerializeField] private LineBeam _playerPrefab;
        [SerializeField] private TMP_Text _gameTimeText;
        [SerializeField] private TMP_Text _balloonCountText;
        [SerializeField] private TabManager _mainTabManager;
        private bool _isStart = false;
        private float _time = 0f;
        private int _balloonCount = 0;

        void Start()
        {
            LobbyManager.OnReleaseAll -= GameExit;
            LobbyManager.OnReleaseAll += GameExit;
            Timer.OnTimeUpdated -= UpdateGameTime;
            Timer.OnTimeUpdated += UpdateGameTime;
            Timer.onTimeUp -= OnTimeUp;
            Timer.onTimeUp += OnTimeUp;
            PlayerContent.OnAllReady -= GameStart;
            PlayerContent.OnAllReady += GameStart;

            //BalloonSpawnManager.Instance.OnBalloonDestroyed += UpdateBalloonCount;

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
            #if !UNITY_WEBGL || UNITY_EDITOR
            EyeMoT.GameRecoder.GameRecoder.Instance.RecordStart();
            #endif
            BGMManager.Instance.Play(BGMPath.BALLOON_GAME, volumeRate: 0.5f);

            var players = PlayerRegistry.Players
                    .Where(kvp => kvp.Team != PlayerRegistry.TeamState.Spectator && kvp.Team != PlayerRegistry.TeamState.None)
                    .OrderBy(kvp => kvp.Index)
                    .ToArray();

            ResultManager.Instance.StartRecordHeatmap(players);

            _gameTimeText.text = SettingManager.Instance.GameData.GameTime.ToString("F1") + "s";
            _balloonCountText.text = "×" + 0;
            _balloonCount = 0;
            _time = 0f;
            _isStart = true;

            if(!LobbyManager.Instance.Runner.IsServer) return;

            Timer.Instance.StartTimer(LobbyManager.Instance.Runner.Tick, SettingManager.Instance.GameData.GameTime);

            BalloonSpawnManager.Instance.SpawnInitialBalloons(SettingManager.Instance.GameData.BalloonGeneratePatern);

            if(PlayerContent.Instance == null)
                LobbyManager.Instance.Runner.Spawn(_playerContentPrefab, Vector3.zero, Quaternion.identity);

            if(PlayerContent.CountAll > 0) return;

            //var offset = players.Length == 1 ? 0 : (players.Length - 1) * 2 - 1;
            var offset = 1- players.Length;

            foreach(var player in players)
            {
                var obj = LobbyManager.Instance.Runner.Spawn(_playerPrefab, new Vector3(offset + player.Index * 2, 0, 4.3f), Quaternion.identity, player.Ref);
                LobbyManager.Instance.Runner.SetPlayerObject(player.Ref, obj.GetComponent<NetworkObject>());
                PlayerContent.Instance.Server_Add(player.Ref, obj.GetComponent<LineBeam>());    
            }
        }

        public void GameEnd()
        {
            _mainTabManager.OpenPanel("Result");
        
            #if !UNITY_WEBGL || UNITY_EDITOR
            EyeMoT.GameRecoder.GameRecoder.Instance.RecordEnd();
            #endif
            ResultManager.Instance.ShowResult();
            _gameTimeText.text = "0.0s";
            BalloonSpawnManager.Instance.ResetBalloons();
        }

        public void GameRestart()
        {
            _mainTabManager.OpenPanel("Game");
        }

        public void ReturnTitle()
        {
            if(LobbyManager.Instance.Runner.GameMode != GameMode.Single)
            {
                PopupUI.OnVisible("タイトルへ戻りますか？", "再度同じルームには入れませんが、よろしいですか？", PopupUI.Type.Alert, () =>
                {
                    _mainTabManager.OpenPanel("Title");
                    LobbyManager.Instance.Quit();
                }, true);
                return;
            }
            _mainTabManager.OpenPanel("Title");
            LobbyManager.Instance.Quit();
        }

        public void GameExit()
        {
            _mainTabManager.OpenPanel("Title");
            ResultManager.Instance.StopRecordHeatmap();
            #if !UNITY_WEBGL || UNITY_EDITOR
            EyeMoT.GameRecoder.GameRecoder.Instance.RecordEnd();
            #endif
            BGMManager.Instance.Play(BGMPath.BALLOON_TITLE, volumeRate: 0.5f);
            _isStart = false;
            BalloonSpawnManager.Instance.ResetBalloons();

            Init();
        }

        public int UpdateBalloonCount()
        {
            if(!_isStart) return 0;
            //var count = 0;
            // foreach(var player in PlayerContent.Everyone)
            //     count ++= player.NetwrokedBalloonCount;
            //_balloonCount = count;
            _balloonCount ++;
            _balloonCountText.text = "×" + _balloonCount;
            return _balloonCount;
        }

        private void UpdateGameTime(float time)
        {
            if(!_isStart) return;

            _gameTimeText.text = time.ToString("F1") + "s";
        }

        private void OnTimeUp()
        {
            _isStart = false;
            GameEnd();
        }

        //UIhook
        public void LoadLobbyScene()
        {
            LobbyManager.OnGameStart -= GameStart;
            LobbyManager.OnGameStart += GameStart;
            LobbyManager.Instance._mainTabManager.OpenPanel("Network");
            LobbyManager.Instance.TryJoinLobby();
        }
    }
}
