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
        [Networked, OnChangedRender(nameof(OnTeamChanged))]
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

        public void Server_Init(PlayerRef pRef, byte index, byte index_team)
        {
            Debug.Assert(Runner.IsServer);

            Ref = pRef;
            Index = Team == PlayerRegistry.TeamState.Spectator ? (byte)255 : index;
            IndexByTeam = index_team;
            //Index = index;
        }

        public override void Spawned()
        {
            if(LobbyManager.Instance.Runner.GameMode == GameMode.Single)
            {
                Team = PlayerRegistry.TeamState.Red;
                PlayerRegistry.Server_Add(Runner, Object.InputAuthority, this);
            }
            
            if (Object.HasInputAuthority)
            {
                Local = this;
                Rpc_SetPlayerData(PlayerData.Instance.Nickname, PlayerData.Instance.CharacterName);
            }

            if(Team != PlayerRegistry.TeamState.None)
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

        void NameChanged()
        {
            OnNameChanged?.Invoke();
        }

        void OnTeamChanged()
        {
            PlayerRegistry.PlayerJoined(Object.InputAuthority);
            if(Object.HasInputAuthority && Team != PlayerRegistry.TeamState.Spectator)
            {
                ReliableKey reliableKey = ReliableKeys.GetImageKey(Index, false);
                //Runner.SendReliableDataToServer(reliableKey, PlayerData.Instance.PlayerImage.texture.EncodeToPNG());
            }
        }

        void ReadyStateChanged()
        {
            OnReadyStateChanged?.Invoke();
        }

        
    }
}