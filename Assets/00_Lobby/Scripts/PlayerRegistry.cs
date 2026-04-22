using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using System.Linq;
using Helpers.Linq;
using Fusion.Sockets;
using System;

namespace EyeMoT.Fusion
{

    public class PlayerRegistry : NetworkBehaviour, INetworkRunnerCallbacks
    {
        public enum TeamState
        {
            None = -1,
            TeamA = 0,
            TeamB = 1,
            Spectator = 255
        }
        public const byte CAPACITY = 20;
        public static PlayerRegistry Instance { get; private set; }
        public static int CountAll => Instance.Object.IsValid ? Instance.ObjectByRef.Count : 0;
        public static int CountPlayers => Instance.Object.IsValid ? CountWhere(p => p.Team != TeamState.Spectator) : 0;
        public static int CountSpectators => Instance.Object.IsValid ? CountWhere(p => p.Team == TeamState.Spectator) : 0;
        public static event System.Action<NetworkRunner, PlayerRef> OnPlayerRegistered;
        public static event System.Action<NetworkRunner, PlayerRef> OnPlayerLeft;

        public static IEnumerable<PlayerObject> Everyone => Instance?.Object?.IsValid == true ? Instance.ObjectByRef.Select(kvp => kvp.Value) : Enumerable.Empty<PlayerObject>();
        public static IEnumerable<PlayerObject> Players => Instance?.Object?.IsValid == true ? Instance.ObjectByRef.Where(kvp => kvp.Value && kvp.Value.Team != TeamState.Spectator).Select(kvp => kvp.Value) : Enumerable.Empty<PlayerObject>();

        [Networked, Capacity(CAPACITY)]
        NetworkDictionary<PlayerRef, PlayerObject> ObjectByRef { get; }

        bool _allReady = false;

        void Awake()
        {
            Instance = this;
        }

        public override void Spawned()
        {
            Instance = this;
            Runner.AddCallbacks(this);

            if (Runner != null)
            {
                Runner.SetIsSimulated(Object, true);
            }
        }

        public override void FixedUpdateNetwork()
        {
            //if(!Runner.IsServer) return;

            if(!_allReady)
            {
                if(CountAll != Runner.ActivePlayers.Count()) return;

                var allReady = Players.Count() > 0 && Players.All(p => p.IsReady);
                if (allReady)
                {
                    _allReady = true;
                    LobbyManager.Instance.GameStart();
                }
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            Instance = null;
            runner.RemoveCallbacks(this);
            OnPlayerRegistered = OnPlayerLeft = null;
        }
        
        public bool GetAvailableOfTeam(PlayerRegistry.TeamState team, out byte index)
        {
            if (ObjectByRef.Where(kvp => kvp.Value.Team == team).Count() == 0)
            {
                index = 0;
                return true;
            }
            else if (ObjectByRef.Count == CAPACITY)
            {
                index = default;
                return false;
            }

            byte[] indices = ObjectByRef.Where(kvp => kvp.Value.Team == team && kvp.Value.IndexByTeam != 255).OrderBy(kvp => kvp.Value.IndexByTeam).Select(kvp => kvp.Value.IndexByTeam).ToArray();

            if(indices.Length == 1 && indices[0] > 0 || indices.Length == 0)
            {
                index = 0;
                return true;
            }

            for (int i = 0; i < indices.Length - 1; i++)
            {
                if (indices[i + 1] > indices[i] + 1)
                {
                    index = (byte)(indices[i] + 1);
                    return true;
                }
            }


            index = (byte)(indices[indices.Length - 1] + 1);
            return true;
        }

        public static void Server_Add(NetworkRunner runner, PlayerRef pRef, PlayerObject pObj)
        {
            Debug.Assert(runner.IsServer);

            //if (Instance.GetAvailable(out byte index))
            if(Instance.GetAvailableOfTeam(pObj.Team, out byte index))
            {
                Instance.ObjectByRef.Add(pRef, pObj);
                pObj.Server_Init(pRef, index);
            }
            else
            {
                Debug.LogWarning($"<color=orange>[Fusion]</color> Unable to register player {pRef}", pObj);
            }
        }
        
        public static void PlayerJoined(PlayerRef player)
        {
            OnPlayerRegistered?.Invoke(Instance.Runner, player);
        }

        public static void Server_Remove(NetworkRunner runner, PlayerRef pRef)
        {
            Debug.Assert(runner.IsServer);
            Debug.Assert(pRef.IsRealPlayer);

            Debug.Log($"<color=orange>[Fusion]</color> Removing player {pRef}");

            if (!Instance.ObjectByRef.Remove(pRef))
            {
                Debug.LogWarning("<color=orange>[Fusion]</color> Could not remove player from registry");
            }
        }

        public static bool HasPlayer(PlayerRef pRef)
        {
            return Instance.ObjectByRef.ContainsKey(pRef);
        }

        public static PlayerObject GetPlayer(PlayerRef pRef)
        {
            if (HasPlayer(pRef))
                return Instance.ObjectByRef.Get(pRef);
            return null;
        }

        #region Utility Methods

        /// <summary>
        /// 指定した条件に一致するプレイヤーを取得
        /// </summary>
        /// <param name="match">条件を指定するPredicate</param>
        /// <param name="includeSpectators">観戦者を含めるかどうか</param>
        /// <returns>条件に一致するプレイヤーのコレクション</returns>
        public static IEnumerable<PlayerObject> Where(System.Predicate<PlayerObject> match, bool includeSpectators = false)
        {
            return (includeSpectators ? Everyone : Players).Where(p => match.Invoke(p));

            //return Instance.ObjectByRef.Where(kvp => match.Invoke(kvp.Value)).Select(kvp => kvp.Value);
        }

        /// <summary>
        /// 指定した条件に一致する最初のプレイヤーを取得
        /// </summary>
        /// <param name="match">条件を指定するPredicate</param>
        /// <param name="includeSpectators">観戦者を含めるかどうか</param>
        /// <returns>条件に一致する最初のプレイヤー</returns>
        public static PlayerObject First(System.Predicate<PlayerObject> match, bool includeSpectators = false)
        {
            return (includeSpectators ? Everyone : Players).First(p => match.Invoke(p));
        }

        /// <summary>
        /// 全プレイヤーに対して指定したアクションを実行
        /// </summary>
        /// <param name="action">実行するアクション</param>
        /// <param name="includeSpectators">観戦者を含めるかどうか</param>
        public static void ForEach(System.Action<PlayerObject> action, bool includeSpectators = false)
        {
            (includeSpectators ? Everyone : Players).ForEach(p => action.Invoke(p));
        }

        /// <summary>
        /// 全プレイヤーに対してインデックス付きでアクションを実行
        /// </summary>
        /// <param name="action">実行するアクション（プレイヤーとインデックスを受け取る）</param>
        /// <param name="includeSpectators">観戦者を含めるかどうか</param>
        public static void ForEach(System.Action<PlayerObject, int> action, bool includeSpectators = false)
        {
            int i = 0;
            (includeSpectators ? Everyone : Players).ForEach(p => action.Invoke(p, i++));
        }

        /// <summary>
        /// 指定した条件に一致するプレイヤーに対してアクションを実行
        /// </summary>
        /// <param name="match">条件を指定するPredicate</param>
        /// <param name="action">実行するアクション</param>
        /// <param name="includeSpectators">観戦者を含めるかどうか</param>
        public static void ForEachWhere(System.Predicate<PlayerObject> match, System.Action<PlayerObject> action, bool includeSpectators = false)
        {
            (includeSpectators ? Everyone : Players).Where(p => match.Invoke(p)).ForEach(p => action.Invoke(p));
            //foreach (PlayerObject p in (includeSpectators ? Everyone : Players).Where(p => match.Invoke(p)))
            //{
            //	action.Invoke(p);
            //}
        }

        /// <summary>
        /// 指定した条件に一致するプレイヤーの数を取得
        /// </summary>
        /// <param name="match">条件を指定するPredicate</param>
        /// <param name="includeSpectators">観戦者を含めるかどうか</param>
        /// <returns>条件に一致するプレイヤーの数</returns>
        public static int CountWhere(System.Predicate<PlayerObject> match, bool includeSpectators = false)
        {
            return (includeSpectators ? Everyone : Players).Where(p => match.Invoke(p)).Count();

            //int count = 0;
            //foreach (var kvp in Instance.ObjectByRef)
            //{
            //	if (match.Invoke(kvp.Value))
            //		count++;
            //}
            //return count;
        }

        /// <summary>
        /// 指定した条件に一致するプレイヤーが1人以上いるかチェック
        /// </summary>
        /// <param name="match">条件を指定するPredicate</param>
        /// <param name="includeSpectators">観戦者を含めるかどうか</param>
        /// <returns>条件に一致するプレイヤーが存在するかどうか</returns>
        public static bool Any(System.Predicate<PlayerObject> match, bool includeSpectators = false)
        {
            if (Instance == null) return false;
            return (includeSpectators ? Everyone : Players).Where(p => match.Invoke(p)).Count() > 0;

            //foreach (var kvp in Instance.ObjectByRef)
            //{
            //	if (match.Invoke(kvp.Value)) return true;
            //}
            //return false;
        }

        /// <summary>
        /// 全プレイヤーが指定した条件を満たすかチェック
        /// </summary>
        /// <param name="match">条件を指定するPredicate</param>
        /// <param name="includeSpectators">観戦者を含めるかどうか</param>
        /// <returns>全プレイヤーが条件を満たすかどうか</returns>
        public static bool All(System.Predicate<PlayerObject> match, bool includeSpectators = false)
        {
            return (includeSpectators ? Everyone : Players).Where(p => !match.Invoke(p)).Count() == 0;

            //foreach (var kvp in Instance.ObjectByRef)
            //{
            //	if (match.Invoke(kvp.Value) == false) return false;
            //}
            //return true;
        }

        /// <summary>
        /// 指定したセレクターで昇順ソートされたプレイヤーを取得
        /// </summary>
        /// <typeparam name="T">ソートキーの型</typeparam>
        /// <param name="selector">ソートキーを選択するFunc</param>
        /// <param name="match">条件を指定するPredicate（オプション）</param>
        /// <param name="includeSpectators">観戦者を含めるかどうか</param>
        /// <returns>昇順ソートされたプレイヤーのコレクション</returns>
        public static IOrderedEnumerable<PlayerObject> OrderAsc<T>(
            System.Func<PlayerObject, T> selector,
            System.Predicate<PlayerObject> match = null,
            bool includeSpectators = false) where T : System.IComparable<T>
        {
            if (match != null) return (includeSpectators ? Everyone : Players).Where(p => match.Invoke(p)).OrderBy(selector);
            return (includeSpectators ? Everyone : Players).OrderBy(selector);
        }

        /// <summary>
        /// 指定したセレクターで降順ソートされたプレイヤーを取得
        /// </summary>
        /// <typeparam name="T">ソートキーの型</typeparam>
        /// <param name="selector">ソートキーを選択するFunc</param>
        /// <param name="match">条件を指定するPredicate（オプション）</param>
        /// <param name="includeSpectators">観戦者を含めるかどうか</param>
        /// <returns>降順ソートされたプレイヤーのコレクション</returns>
        public static IOrderedEnumerable<PlayerObject> OrderDesc<T>(
            System.Func<PlayerObject, T> selector, 
            System.Predicate<PlayerObject> match = null, 
            bool includeSpectators = false) where T : System.IComparable<T>
        {
            if (match != null) return (includeSpectators ? Everyone : Players).Where(p => match.Invoke(p)).OrderByDescending(selector);
            return (includeSpectators ? Everyone : Players).OrderByDescending(selector);
        }

        /// <summary>
        /// 現在のプレイヤーの次のプレイヤーを取得（循環リスト形式）
        /// </summary>
        /// <param name="current">現在のプレイヤー</param>
        /// <param name="includeSpectators">観戦者を含めるかどうか</param>
        /// <returns>次のプレイヤー</returns>
        public static PlayerObject Next(PlayerObject current, bool includeSpectators = false)
        {
            IEnumerable<PlayerObject> collection = (includeSpectators ? Everyone : Players);
            int index = collection.FirstIndex(current);
            if (index == -1) return current;
            int length = collection.Count();
            return collection.ElementAt((int)Mathf.Repeat(index + 1, length));
        }

        /// <summary>
        /// 現在のプレイヤーの次の条件に一致するプレイヤーを取得（循環リスト形式）
        /// </summary>
        /// <param name="current">現在のプレイヤー</param>
        /// <param name="match">条件を指定するPredicate</param>
        /// <param name="includeSpectators">観戦者を含めるかどうか</param>
        /// <returns>次の条件に一致するプレイヤー</returns>
        public static PlayerObject NextWhere(PlayerObject current, System.Predicate<PlayerObject> match, bool includeSpectators = false)
        {
            IEnumerable<PlayerObject> collection = (includeSpectators ? Everyone : Players).Where(p => match.Invoke(p));
            int index = collection.FirstIndex(current);
            if (index == -1) return current;
            int length = collection.Count();
            return collection.ElementAt((int)Mathf.Repeat(index + 1, length));
        }

        #endregion

        #region INetworkRunnerCallbacks
        void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (runner.IsServer) Server_Remove(runner, player);
            OnPlayerLeft?.Invoke(Runner, player);
        }
        void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input) { }
        void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { }
        void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner) { }
        void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) { }
        void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
        }
        #endregion
    }
}
