using Fusion;
using UnityEngine;
using System.Threading.Tasks;
using System;
using System.Collections;
using Fusion.Sockets;
using System.Collections.Generic;
using TMPro;

namespace EyeMoT.Fusion
{
    public class LobbyManager : SimulationBehaviour, INetworkRunnerCallbacks
    {
        public static LobbyManager Instance;
        [Header("Resources")]
        [SerializeField] private RunnerLifecycleManager _runnerLifecycleManager;
        [SerializeField] private SessionListView _sessionListView;
        [SerializeField] private InputProvider _inputProvider;
        [SerializeField] private TMP_Text _lobbyStateText;
        [SerializeField] public SessionHolder SessionHolder;
        public TabManager _networkTabManager;
        public TabManager _mainTabManager;
        public static event System.Action OnInitAll; //Host or Client　がセッションに入ったときに呼ばれるイベント
        public static event System.Action OnReleaseAll;
        public static event System.Action OnGameStart;
        public static event System.Action<NetworkRunner, PlayerRef, ReliableKey, ArraySegment<byte>> OnReliableDataReceivedEvent;

        public new NetworkRunner Runner => _runnerLifecycleManager != null ? _runnerLifecycleManager.Runner : null;

        bool _isTransitioning;

        async void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            SessionHolder.init();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void GameStart()
        {
            _mainTabManager.OpenPanel("Game");
            OnGameStart?.Invoke();
            if(Runner.IsServer)
            {
                Runner.SessionInfo.IsOpen = false;
            }
        }

        public void TrySingleSession(Action successCallback = null)
        {
            StartCoroutine(SingleSessionRoutine(null, successCallback));
        }

        public IEnumerator SingleSessionRoutine(string sessionCode = null, Action successCallback = null)
        {
            if (_isTransitioning)
            {
                yield break;
            }

            _isTransitioning = true;
            yield return _runnerLifecycleManager.ShutdownRunnerRoutine();
            NetworkRunner runner = _runnerLifecycleManager.CreateRunner(this, attachSpawnListener: true);

            Task<StartGameResult> task = runner.StartGame(new StartGameArgs()
            {
                GameMode = GameMode.Single,
                SessionName = "1",
                SceneManager = runner.GetComponent<INetworkSceneManager>(),
            });

            while (!task.IsCompleted)
            {
                yield return null;
            }

            StartGameResult result = task.Result;
            if (result.Ok)
            {
                successCallback?.Invoke();
            }
            else
            {
                DisconnectUI.OnShutdown(result.ShutdownReason);
            }

            _isTransitioning = false;
        }

        public void TryJoinLobby(Action successCallback = null)
        {
            StartCoroutine(JoinLobbyRoutine(successCallback));
        }

        IEnumerator JoinLobbyRoutine(Action successCallback = null)
        {
            _networkTabManager.OpenPanel("Network_Lobby");

            if (_isTransitioning)
            {
                yield break;
            }

            Debug.Log("<color=orange>[Fusion]</color> Joining Lobby...");
            _isTransitioning = true;
            Loading.Instance.SetVisible(true);

            yield return _runnerLifecycleManager.ShutdownRunnerRoutine();
            NetworkRunner runner = _runnerLifecycleManager.CreateRunner(this);

            Task<StartGameResult> task = runner.JoinSessionLobby(SessionLobby.ClientServer);
            while (!task.IsCompleted)
            {
                yield return null;
            }

            StartGameResult result = task.Result;
            Loading.Instance.SetVisible(false);
            if (!result.Ok)
            {
                DisconnectUI.OnShutdown(result.ShutdownReason);
            }
            else
            {
                _lobbyStateText.text = "ルーム一覧";
                    Debug.Log("<color=orange>[Fusion]</color> Joined Lobby!");
                successCallback?.Invoke();
            }

            _isTransitioning = false;
        }

        public void CreateHostSession()
        {
            SessionDef.Name? availableSession = GetAvailableSessionName();
            if (!availableSession.HasValue)
            {
                PopupUI.OnVisible("空きルームがありません", "利用可能なルーム名がすべて使用中です。", PopupUI.Type.Alert);
                return;
            }

            SessionHolder.TryGet(availableSession.Value, out SessionData data);
            var sessionCode = SessionCodeUtility.BuildSessionCode(data.Name);
            var description = $"ホストとしてルーム<color=green>「{data.UIName}」</color>を作成します。\nホストがゲーム終了するとルームも閉じます。";
            PopupUI.OnVisible($"ルームを作成しますか？", description, data.Sprite, () =>
            {
                TryHostSession(sessionCode);
            });
        }

        public void TryHostSession(string sessionCode = null, Action successCallback = null)
        {
            StartCoroutine(HostSessionRoutine(sessionCode, successCallback));
        }

        IEnumerator HostSessionRoutine(string sessionCode = null, Action successCallback = null)
        {
            if (_isTransitioning)
            {
                yield break;
            }

            Debug.Log("<color=orange>[Fusion]</color> Hosting Session...");
            _isTransitioning = true;
            yield return _runnerLifecycleManager.ShutdownRunnerRoutine();
            NetworkRunner runner = _runnerLifecycleManager.CreateRunner(this, attachSpawnListener: true);

            Dictionary<string, SessionProperty> customProperties = new Dictionary<string, SessionProperty>
            {
                { "WhitePlayers", 0 },
                { "RedPlayers", 0 },
                { "SpectatorPlayers", 0 },
            };

            Loading.Instance.SetVisible(true);
            Task<StartGameResult> task = runner.StartGame(new StartGameArgs()
            {
                GameMode = GameMode.Host,
                SessionName = sessionCode,
                SceneManager = runner.GetComponent<INetworkSceneManager>(),
                PlayerCount = 20,
                SessionProperties = customProperties,
            });

            while (!task.IsCompleted)
            {
                yield return null;
            }

            StartGameResult result = task.Result;
            Loading.Instance.SetVisible(false);
            if (result.Ok)
            {
                Debug.Log("<color=orange>[Fusion]</color> Session Hosted!");
                OnInitAll?.Invoke();
                SessionDef.Name sessionDefName = SessionCodeUtility.ParseSessionName(sessionCode);
                SessionHolder.TryGet(sessionDefName, out SessionData data);
                _lobbyStateText.text = $"ルーム : <color=green>{data.UIName}</color>";
                _networkTabManager.OpenPanel("Network_TeamSelect");
                successCallback?.Invoke();
            }
            else
            {
                DisconnectUI.OnShutdown(result.ShutdownReason);
            }

            _isTransitioning = false;
        }

        public void TryJoinSession(string sessionCode, Action successCallback = null)
        {
            StartCoroutine(JoinSessionRoutine(sessionCode, successCallback));
        }

        IEnumerator JoinSessionRoutine(string sessionCode, Action successCallback)
        {
            if (_isTransitioning)
            {
                yield break;
            }

            Debug.Log($"<color=orange>[Fusion]</color> Joining Session {sessionCode}...");
            _isTransitioning = true;
            yield return _runnerLifecycleManager.ShutdownRunnerRoutine();
            NetworkRunner runner = _runnerLifecycleManager.CreateRunner(this);

            Loading.Instance.SetVisible(true);
            Task<StartGameResult> task = runner.StartGame(new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = sessionCode,
                SceneManager = runner.GetComponent<INetworkSceneManager>(),
                ConnectionToken = SessionCodeUtility.BuildJoinToken(sessionCode),
            });

            while (!task.IsCompleted)
            {
                yield return null;
            }

            StartGameResult result = task.Result;
            Loading.Instance.SetVisible(false);
            if (result.Ok)
            {
                Debug.Log("<color=orange>[Fusion]</color> Joined Session!");
                OnInitAll?.Invoke();
                SessionDef.Name sessionDefName = SessionCodeUtility.ParseSessionName(sessionCode);
                SessionHolder.TryGet(sessionDefName, out SessionData data);
                _lobbyStateText.text = $"ルーム : <color=green>{data.UIName}</color>";
                _networkTabManager.OpenPanel("Network_TeamSelect");
                successCallback?.Invoke();
            }
            else
            {
                DisconnectUI.OnShutdown(result.ShutdownReason);
            }

            _isTransitioning = false;
        }

        SessionDef.Name? GetAvailableSessionName()
        {
            return _sessionListView != null ? _sessionListView.GetAvailableSessionName() : null;
        }
        static void RefuseConnection(NetworkRunnerCallbackArgs.ConnectRequest request)
        {
            object requestObject = request;
            Type requestType = requestObject.GetType();

            var resultProperty = requestType.GetProperty("Result");
            if (resultProperty != null && resultProperty.CanWrite)
            {
                Type enumType = Nullable.GetUnderlyingType(resultProperty.PropertyType) ?? resultProperty.PropertyType;
                resultProperty.SetValue(requestObject, Enum.Parse(enumType, "Refuse"));
                return;
            }

            var resultField = requestType.GetField("Result");
            if (resultField != null)
            {
                Type enumType = Nullable.GetUnderlyingType(resultField.FieldType) ?? resultField.FieldType;
                resultField.SetValue(requestObject, Enum.Parse(enumType, "Refuse"));
            }
        }

        public void Quit()
        {
            StartCoroutine(QuitRoutine());
        }

        public IEnumerator QuitRoutine()
        {
            Debug.Log("<color=orange>[Fusion]</color> Quitting ...");

            Loading.Instance.SetVisible(true);

            switch (_mainTabManager.GetCurrentPanelName())
            {
                case "Game":
                    yield return ReleaseAll(() =>
                    {             
                        Debug.Log("<color=orange>[Fusion]</color> Quit and Released Resources.");   
                        Loading.Instance.SetVisible(false);
                    });
                    yield return SingleSessionRoutine(successCallback: () =>
                    {
                        OnReleaseAll?.Invoke();
                    });
                    yield break;
            }

            switch (_networkTabManager.GetCurrentPanelName())
            {
                case "Network_Lobby":
                    yield return ReleaseAll(() =>
                    {             
                        Debug.Log("<color=orange>[Fusion]</color> Quit and Released Resources.");   
                        Loading.Instance.SetVisible(false);
                        _mainTabManager.OpenPanel("Offline");
                    });
                    yield return SingleSessionRoutine(successCallback: () =>
                    {
                        OnReleaseAll?.Invoke();
                    });
                    yield break;
                case "Network_TeamSelect": case "Network_Session":
                    yield return ReleaseAll();
                    yield return JoinLobbyRoutine();
                    yield break;
            }
        }

        //コールバック内で LobbyManager.Instance を触る前提にはしない方がいい
        public IEnumerator ReleaseAll(Action onComplete = null)
        {
            yield return StartCoroutine(ReleaseAllRoutine(onComplete));
        }

        IEnumerator ReleaseAllRoutine(Action onComplete)
        {
            if (_isTransitioning)
            {
                yield break;
            }

            _isTransitioning = true;

            yield return _runnerLifecycleManager.ReleaseAllRoutine();

            _isTransitioning = false;
            onComplete?.Invoke();
        }

        #region NetworkCallBacks

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            _sessionListView.UpdateSessions(sessionList);
        }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            _runnerLifecycleManager.HandleRunnerShutdown(runner);

            _isTransitioning = false;
            DisconnectUI.OnShutdown(shutdownReason);
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            _isTransitioning = false;
            DisconnectUI.OnDisconnectedFromServer(reason);
        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            if (!runner.IsServer || runner.SessionInfo == null)
            {
                return;
            }

            if (!SessionCodeUtility.IsValidJoinToken(token, runner.SessionInfo.Name))
            {
                RefuseConnection(request);
            }
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            _isTransitioning = false;
            DisconnectUI.OnConnectFailed(reason);
        }

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        { }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        { }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        { }

        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
        { }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            _inputProvider.ApplyInput(input);
        }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
        {
            _inputProvider.ApplyMissingInput(input);
        }

        public void OnConnectedToServer(NetworkRunner runner)
        { }

        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
        { }

        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
        { }

        public void OnSceneLoadDone(NetworkRunner runner)
        { }

        public void OnSceneLoadStart(NetworkRunner runner)
        { }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        { }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
        {
            OnReliableDataReceivedEvent?.Invoke(runner, player, key, data);
        }

        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
        { }

        #endregion
    }
}
