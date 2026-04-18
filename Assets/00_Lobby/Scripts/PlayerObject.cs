using UnityEngine;
using Fusion;
using Fusion.Sockets;

namespace EyeMoT.Fusion
{
    public class PlayerObject : NetworkBehaviour
    {
        public static PlayerObject Local { get; private set; }
        
        // Metadata
        [Networked]
        public PlayerRef Ref { get; set; }
        [Networked]
        public PlayerController Controller { get; set; }
        [Networked]
        public PlayerRegistry.TeamState Team { get; set; } = PlayerRegistry.TeamState.None;
        [Networked]
        public byte Index { get; set; } = 255;
        [Networked, OnChangedRender(nameof(OnIndexByTeamChanged))]
        public byte IndexByTeam { get; set; } = 255;

        // User Settings
        [Networked, OnChangedRender(nameof(NameChanged))]
        public string Nickname { get; set; }
        [Networked, Capacity(30)]
        public string CharacterName { get; set; }
        // State & Gameplay Info
        [Networked, OnChangedRender(nameof(ReadyStateChanged))]
        public bool IsReady { get; set; }


        public event System.Action OnNameChanged;
        public event System.Action OnReadyStateChanged;

        public void Server_Init(PlayerRef pRef, byte index)
        {
            Debug.Assert(Runner.IsServer);

            Ref = pRef;
            IndexByTeam = index;
            //Index = index;
        }

        public override void Spawned()
        {
            if(LobbyManager.Instance.Runner.GameMode == GameMode.Single)
            {
                Team = PlayerRegistry.TeamState.TeamA;
                PlayerRegistry.Server_Add(Runner, Object.InputAuthority, this);
            }
            
            if (Object.HasInputAuthority)
            {
                Local = this;
                Rpc_SetPlayerData(PlayerData.Instance.Nickname + $"_{Index}", PlayerData.Instance.CharacterName + $"_{Index}");
            }

            if(Team != PlayerRegistry.TeamState.None && IndexByTeam != 255)
                PlayerRegistry.PlayerJoined(Object.InputAuthority);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Local == this) Local = null;

            if (!runner.IsShutdown)
            {
                if (Controller)
                {
                    runner.Despawn(Controller.Object);
                }
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void Rpc_OnInput()
        {
            if (Controller == null) return;
            Controller.OnInput();
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void Rpc_SetPlayerData(string nick, string characterName)
        {
            Nickname = nick;
            CharacterName = characterName;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void Rpc_SetReadyState(bool isReady)
        {
            IsReady = isReady;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void Rpc_SetTeam(PlayerRegistry.TeamState team)
        {
            Team = team;
            PlayerRegistry.Server_Add(Runner, Object.InputAuthority, this);
        }

        [Rpc(RpcSources.All, RpcTargets.InputAuthority)]
        public void Rpc_SendImage()
        {
            ReliableKey reliableKey = ReliableKey.FromInts(0, Index, 0, 0);
            Runner.SendReliableDataToServer(reliableKey, PlayerData.Instance.ImageBytes);
        }

        void NameChanged()
        {
            OnNameChanged?.Invoke();
        }

        void OnIndexByTeamChanged()
        {
            PlayerRegistry.PlayerJoined(Object.InputAuthority);
        }

        void ReadyStateChanged()
        {
            OnReadyStateChanged?.Invoke();
        }

        
    }
}