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
        public bool IsStart = false;
        private float _time = 0f;
        private int _balloonCount = 0;
        public bool IsAnalyze = false;
        public BalloonSpawnManager.GenerationPatern CurrentMode => AnalyzeModePopupUI.Instance._currentMode;

        void Start()
        {
            #if !UNITY_WEBGL && !UNITY_EDITOR
            Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
            #endif

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

        void Update()
        {
            if(Input.GetKeyDown(KeyCode.T) && PlayerData.Instance.CanUseShortCut)
            {
                ReturnTitle(LobbyManager.Instance.Runner.GameMode != GameMode.Single);
            }

            if(Input.GetKeyDown(KeyCode.R) && PlayerData.Instance.CanUseShortCut)
            {
                if(LobbyManager.Instance.Runner.GameMode != GameMode.Single) return;
                #if !UNITY_WEBGL || UNITY_EDITOR
                EyeMoT.GameRecoder.GameRecoder.Instance.RecordEnd();
                #endif
                GameStart();
            }
        }

        void Init()
        {
            _mainTabManager.OpenPanel("Title");
            BGMManager.Instance.Play(BGMPath.BALLOON_TITLE, volumeRate: 0.5f);
            PreviewManager.Instance.ResetBalloon();
        }

        public void OnClickStartButton()
        {
            GameStart();
        }

        public void OnClickAnalyzeButton()
        {
            IsAnalyze = true;
            AnalyzeModePopupUI.OnVisible("評価モードで開始しますか？", "評価モードでは、バルーンは1つしか出現せず、プレイヤーの視線情報を評価することができます。");
        }

        public void GameStart()
        {
            CursorManager.Instance.SetCursorVisible(false);
            _mainTabManager.OpenPanel("Game");
            #if !UNITY_WEBGL || UNITY_EDITOR
            if(SettingManager.Instance.GameData.ActiveRecord == 0)
                EyeMoT.GameRecoder.GameRecoder.Instance.RecordStart();
            #endif
            BGMManager.Instance.Play(BalloonBGMEditor.Instance.CurrentBGM, volumeRate: 0.5f);

            var players = PlayerRegistry.Players
                    .Where(kvp => kvp.Team != PlayerRegistry.TeamState.Spectator && kvp.Team != PlayerRegistry.TeamState.None)
                    .OrderBy(kvp => kvp.Index)
                    .ToArray();

            ResultManager.Instance.StartRecordHeatmap(players);

            _gameTimeText.text = SettingManager.Instance.GameData.GameTime.ToString("F1") + "s";
            _balloonCountText.text = "×" + 0;
            _balloonCount = 0;
            _time = 0f;
            IsStart = true;


            if(IsAnalyze)
            {
                Debug.Log("GazeAnalyzeManager: AnalyzeStart");
                GazeAnalyseManager.AnalyzeStart(
                    sessionId: "BalloonGame",
                    targetCamera: Camera.main,
                    sampleHz: 30f
                );
            }
            

            if(!LobbyManager.Instance.Runner.IsServer) return;

            Timer.Instance.StartTimer(LobbyManager.Instance.Runner.Tick, SettingManager.Instance.GameData.GameTime);

            var patern = /*IsAnalyze ? CurrentMode : */SettingManager.Instance.GameData.BalloonGeneratePatern;
            var maxBalloons = IsAnalyze ? 1 : SettingManager.Instance.GameData.BalloonAmount;
            BalloonSpawnManager.Instance.SpawnInitialBalloons(patern, maxBalloons);

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
            CursorManager.Instance.SetCursorVisible(true);
            #if !UNITY_WEBGL || UNITY_EDITOR
            EyeMoT.GameRecoder.GameRecoder.Instance.RecordEnd();
            #endif
            if(IsAnalyze)
                ResultManager.Instance.SetAnalyze(GazeAnalyseManager.AnalyzeEnd());
            DebugManager.Instance.DebugOff();
            ResultManager.Instance.ShowResult();
            _gameTimeText.text = "0.0s";
            BalloonSpawnManager.Instance.ResetBalloons();
        }

        public void GameRestart()
        {
            _mainTabManager.OpenPanel("Game");
        }

        // UI hook
        public void ReturnTitle(bool isConfirm = false)
        {
            if(isConfirm && LobbyManager.Instance.Runner.GameMode != GameMode.Single)
            {
                PopupUI.OnVisible("タイトルへ戻りますか？", "再度同じルームには入れませんが、よろしいですか？", PopupUI.Type.Alert, () =>
                {
                    _mainTabManager.OpenPanel("Title");
                    LobbyManager.Instance.Quit();
                }, true);
            }
            else
            {
                PopupUI.OnVisible("タイトルへ戻りますか？", "", PopupUI.Type.Alert, () =>
                {
                    _mainTabManager.OpenPanel("Title");
                    LobbyManager.Instance.Quit();
                }, true);
            }
        }

        public void GameExit()
        {
            _mainTabManager.OpenPanel("Title");
            ResultManager.Instance.StopRecordHeatmap();
            //GazeSessionResult result = GazeAnalyseManager.AnalyzeEnd();
            #if !UNITY_WEBGL || UNITY_EDITOR
            EyeMoT.GameRecoder.GameRecoder.Instance.RecordEnd();
            #endif
            CursorManager.Instance.SetCursorVisible(true);
            DebugManager.Instance.DebugOff();
            BGMManager.Instance.Play(BGMPath.BALLOON_TITLE, volumeRate: 0.5f);
            IsStart = false;
            BalloonSpawnManager.Instance.ResetBalloons();

            Init();
        }

        public int UpdateBalloonCount()
        {
            if(!IsStart) return 0;
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
            if(!IsStart) return;

            _gameTimeText.text = time.ToString("F1") + "s";
        }

        private void OnTimeUp()
        {
            IsStart = false;
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
