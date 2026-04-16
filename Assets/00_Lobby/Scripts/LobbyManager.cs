using Fusion;
using UnityEngine;
using System.Threading.Tasks;
using System;
using System.Collections;
using Fusion.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine.SceneManagement;
using TMPro;

namespace EyeMoT.Fusion
{
    public class LobbyManager : SimulationBehaviour, INetworkRunnerCallbacks
    {
        const string JoinTokenPrefix = "JOIN";

        public static LobbyManager Instance;
        [Header("Resources")]
        [SerializeField] NetworkRunner _runnerPrefab;
        [SerializeField] NetworkObject _registryPrefab;
        [SerializeField] private SessionItemUI _sessionItemPrefab;
        [SerializeField] private Transform _sessionItemHolder;
        [SerializeField] private TMP_Text _noSessionText;
        [SerializeField] private TMP_Text _lobbyStateText;
        [SerializeField] public SessionHolder SessionHolder;
        public TabManager _networkTabManager;
        public TabManager _mainTabManager;
        private List<SessionItemUI> _sessionItems = new List<SessionItemUI>();
        public static event System.Action OnInitAll;
        public static event System.Action OnResetAll;
        public event System.Action OnGameStart;

        public new NetworkRunner Runner { get; private set; }

        bool _isTransitioning;
        NetworkRunner _spawnListenerRunner;
        bool _managerSpawnedForCurrentRunner;

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
        }

        public IEnumerator SingleSessionRoutine(string sessionCode = null, Action successCallback = null)
        {
            if (_isTransitioning)
            {
                yield break;
            }

            _isTransitioning = true;
            yield return ShutdownRunnerRoutine();
            Runner = CreateRunner(attachSpawnListener: true);

            Task<StartGameResult> task = Runner.StartGame(new StartGameArgs()
            {
                GameMode = GameMode.Single,
                SessionName = "1",
                SceneManager = Runner.GetComponent<INetworkSceneManager>(),
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

        public void TryJoinLobby()
        {
            _networkTabManager.OpenPanel("Network_Lobby");
            StartCoroutine(JoinLobbyRoutine());
        }

        IEnumerator JoinLobbyRoutine()
        {
            if (_isTransitioning)
            {
                yield break;
            }

            Debug.Log("<color=orange>[Fusion]</color> Joining Lobby...");
            _isTransitioning = true;
            Loading.Instance.SetVisible(true);

            yield return ShutdownRunnerRoutine();
            Runner = CreateRunner();

            Task<StartGameResult> task = Runner.JoinSessionLobby(SessionLobby.ClientServer);
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
            var sessionCode = $"{SceneManager.GetActiveScene().name}_{data.Name}";
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
            yield return ShutdownRunnerRoutine();
            Runner = CreateRunner(attachSpawnListener: true);

            Dictionary<string, SessionProperty> customProperties = new Dictionary<string, SessionProperty>
            {
                { "WhitePlayers", 0 },
                { "RedPlayers", 0 },
                { "SpectatorPlayers", 0 },
            };

            Loading.Instance.SetVisible(true);
            Task<StartGameResult> task = Runner.StartGame(new StartGameArgs()
            {
                GameMode = GameMode.Host,
                SessionName = sessionCode,
                SceneManager = Runner.GetComponent<INetworkSceneManager>(),
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
                SessionDef.Name sessionDefName = (SessionDef.Name)System.Enum.Parse(typeof(SessionDef.Name), sessionCode.Replace(SceneManager.GetActiveScene().name + "_", ""));
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
            yield return ShutdownRunnerRoutine();
            Runner = CreateRunner();

            Loading.Instance.SetVisible(true);
            Task<StartGameResult> task = Runner.StartGame(new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = sessionCode,
                SceneManager = Runner.GetComponent<INetworkSceneManager>(),
                ConnectionToken = BuildJoinToken(sessionCode),
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
                SessionDef.Name sessionDefName = (SessionDef.Name)System.Enum.Parse(typeof(SessionDef.Name), sessionCode.Replace(SceneManager.GetActiveScene().name + "_", ""));
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

        SessionItemUI TrackItem(SessionItemUI item)
        {
            _sessionItems.Add(item);
            return item;
        }

        SessionItemUI GetSessionItem(string sessionname)
        {
            return _sessionItems.FirstOrDefault(item => item.SessionName == sessionname) ?? TrackItem(Instantiate(_sessionItemPrefab, _sessionItemHolder));
        }

        SessionDef.Name? GetAvailableSessionName()
        {
            HashSet<SessionDef.Name> usedNames = new HashSet<SessionDef.Name>();

            foreach (SessionItemUI item in _sessionItems)
            {
                if (item == null || string.IsNullOrEmpty(item.SessionName))
                {
                    continue;
                }

                string[] parts = item.SessionName.Split('_');
                string rawName = parts[parts.Length - 1];
                if (Enum.TryParse(rawName, out SessionDef.Name sessionName))
                {
                    usedNames.Add(sessionName);
                }
            }

            foreach (SessionDef.Name sessionName in Enum.GetValues(typeof(SessionDef.Name)))
            {
                if (!usedNames.Contains(sessionName))
                {
                    return sessionName;
                }
            }

            return null;
        }

        NetworkRunner CreateRunner(bool attachSpawnListener = false)
        {
            NetworkRunner runner = Instantiate(_runnerPrefab);
            runner.AddCallbacks(this);

            if (attachSpawnListener)
            {
                NetworkEvents eventsComponent = runner.GetComponent<NetworkEvents>();
                if (eventsComponent != null)
                {
                    eventsComponent.PlayerJoined.AddListener(HandlePlayerJoinedSpawn);
                    _spawnListenerRunner = runner;
                    _managerSpawnedForCurrentRunner = false;
                }
            }

            return runner;
        }

        IEnumerator ShutdownRunnerRoutine()
        {
            if (Runner == null)
            {
                yield break;
            }

            NetworkRunner oldRunner = Runner;
            Runner = null;

            Task shutdownTask = oldRunner.Shutdown();
            while (!shutdownTask.IsCompleted)
            {
                yield return null;
            }

            if (_spawnListenerRunner == oldRunner)
            {
                _spawnListenerRunner = null;
                _managerSpawnedForCurrentRunner = false;
            }
        }

        void HandlePlayerJoinedSpawn(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer || runner.LocalPlayer != player || _managerSpawnedForCurrentRunner)
            {
                return;
            }

            runner.Spawn(_registryPrefab);
            _managerSpawnedForCurrentRunner = true;
        }

        static byte[] BuildJoinToken(string sessionCode)
        {
            return Encoding.UTF8.GetBytes($"{JoinTokenPrefix}:{sessionCode}");
        }

        static bool IsValidJoinToken(byte[] token, string expectedSessionCode)
        {
            if (token == null || token.Length == 0)
            {
                return false;
            }

            string payload = Encoding.UTF8.GetString(token);
            string[] parts = payload.Split(':');

            return parts.Length == 2 &&
                   parts[0] == JoinTokenPrefix &&
                   parts[1] == expectedSessionCode;
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
                    yield return SingleSessionRoutine();
                    yield break;
                case "Network_TeamSelect":
                    yield return ReleaseAll(() => {
                        TryJoinLobby();
                    });
                    yield break;
                case "Network_Session":
                    yield return ReleaseAll(() => {
                        TryJoinLobby();
                    });
                    yield break;
            }
        }

        //コールバック内で LobbyManager.Instance を触る前提にはしない方がいい
        public IEnumerator ReleaseAll(Action onComplete = null)
        {
            OnResetAll?.Invoke();
            yield return StartCoroutine(ReleaseAllRoutine(onComplete));
        }

        IEnumerator ReleaseAllRoutine(Action onComplete)
        {
            if (_isTransitioning)
            {
                yield break;
            }

            _isTransitioning = true;

            List<NetworkRunner> runners = NetworkRunner.Instances.ToList();
            foreach (NetworkRunner runner in runners)
            {
                if (runner == null)
                {
                    continue;
                }

                Task shutdownTask = runner.Shutdown(shutdownReason: ShutdownReason.Ok);
                while (!shutdownTask.IsCompleted)
                {
                    yield return null;
                }
            }

            Runner = null;
            _spawnListenerRunner = null;
            _managerSpawnedForCurrentRunner = false;

            foreach (NetworkRunner runner in FindObjectsByType<NetworkRunner>(FindObjectsSortMode.None))
            {
                if (runner != null)
                {
                    Destroy(runner.gameObject);
                }
            }

            foreach (NetworkObject networkObject in FindObjectsByType<NetworkObject>(FindObjectsSortMode.None))
            {
                if (networkObject != null)
                {
                    Destroy(networkObject.gameObject);
                }
            }

            yield return null;

            _isTransitioning = false;
            onComplete?.Invoke();
        }

        #region NetworkCallBacks

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            _noSessionText.enabled = sessionList.Count == 0;

            for (int i = _sessionItems.Count - 1; i >= 0; i--)
            {
                var item = _sessionItems[i];
                if (item == null || !sessionList.Any(info => info.Name == item.SessionName))
                {
                    if (item != null)
                    {
                        Destroy(item.gameObject);
                    }

                    _sessionItems.RemoveAt(i);
                }
            }

            foreach (var info in sessionList)
            {
                SessionItemUI sessionInfo = _sessionItems.FirstOrDefault(item => item.SessionName == info.Name) ?? null;
                bool isFull = info.PlayerCount >= info.MaxPlayers;
                bool canJoin = info.IsOpen && !isFull;
                GetSessionItem(info.Name).Init(info.Name, info.PlayerCount, canJoin);
            }
        }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            if (Runner == runner)
            {
                Runner = null;
            }

            if (_spawnListenerRunner == runner)
            {
                _spawnListenerRunner = null;
                _managerSpawnedForCurrentRunner = false;
            }

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

            if (!IsValidJoinToken(token, runner.SessionInfo.Name))
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
        { }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
        { }

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
        { }

        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
        { }

        #endregion
    }
}
