using System;
using System.Collections.Generic;
using System.Linq;
using EyeMoT.Fusion;
using Fusion;
using Fusion.Sockets;
using Helpers.Linq;

namespace EyeMoT.Balloon
{
    public class PlayerContent : NetworkBehaviour, INetworkRunnerCallbacks
    {
        public const byte CAPACITY = 20;
        public static PlayerContent Instance { get; private set; }
        public static int CountAll => Instance != null && Instance.Object.IsValid ? Instance.ObjectByRef.Count : 0;
        public static IEnumerable<LineBeam> Everyone => Instance?.Object?.IsValid == true ? Instance.ObjectByRef.Select(kvp => kvp.Value) : Enumerable.Empty<LineBeam>();
        [Networked, Capacity(CAPACITY)]
        NetworkDictionary<PlayerRef, LineBeam> ObjectByRef { get; }

        public static event System.Action OnAllReady;

        bool _allReady = false;

        public override void Spawned()
        {
            PlayerRegistry.OnPlayerLeft -= OnPlayerLeft;
            PlayerRegistry.OnPlayerLeft += OnPlayerLeft;

            Instance = this;

            if (Runner != null)
            {
                Runner.SetIsSimulated(Object, true);
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            Instance = null;
        }

        public override void FixedUpdateNetwork()
        {
            if(!Runner.IsServer) return;

            if(!_allReady)
            {
                var allReady = Instance.ObjectByRef.Count() > 0 && Instance.ObjectByRef.All(kvp => kvp.Value.NetworkedReady);
                if (allReady)
                {
                    _allReady = true;
                    Instance.ObjectByRef.ForEach(kvp => kvp.Value.NetworkedReady = false);
                    Rpc_OnAllReady();
                }
            }
        }

        public void Server_Add(PlayerRef pRef, LineBeam obj)
        {
            if(Instance.ObjectByRef.ContainsKey(pRef)) return;
            
            Instance.ObjectByRef.Add(pRef, obj);
        }

        private void OnPlayerLeft(NetworkRunner runner, PlayerRef playerRef)
        {
            if (!runner.IsServer) return;

            if (Instance.ObjectByRef.ContainsKey(playerRef))
            {
                Runner.Despawn(GetPlayer(playerRef)?.Object);
                Instance.ObjectByRef.Remove(playerRef);
            }
        }

        public static bool HasPlayer(PlayerRef pRef)
        {
            return Instance.ObjectByRef.ContainsKey(pRef);
        }

        public static LineBeam GetPlayer(PlayerRef pRef)
        {
            if (HasPlayer(pRef))
                return Instance.ObjectByRef.Get(pRef);
            return null;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void Rpc_OnAllReady()
        {
            _allReady = false;
            OnAllReady?.Invoke();
        }

        #region INetworkRunnerCallbacks
        void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
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
