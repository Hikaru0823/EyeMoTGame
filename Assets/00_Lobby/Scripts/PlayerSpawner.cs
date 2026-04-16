using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EyeMoT.Fusion
{
    public class PlayerSpawner : SimulationBehaviour, IPlayerJoined, IPlayerLeft
    {
        public NetworkObject playerObject;

        public void PlayerJoined(PlayerRef player)
        {
            if (Runner.CanSpawn)
            {
                var obj =Runner.Spawn(playerObject, inputAuthority: player, onBeforeSpawned: (runner, obj) =>
                {
                    // チーム選択後にRegistしたいからコメントアウト
                    // PlayerObject pObj = obj.GetComponent<PlayerObject>();
                    // PlayerRegistry.Server_Add(runner, player, pObj);
                });
                var plobj = obj.GetComponent<PlayerObject>();
            }
        }

        public void PlayerLeft(PlayerRef player)
        {
            bool canDespawn = (Runner.Topology == Topologies.ClientServer && Runner.IsServer) || 
                (Runner.Topology == Topologies.Shared && Runner.IsSharedModeMasterClient);

            if (canDespawn)
            {
                PlayerObject leavingPlayer = PlayerRegistry.GetPlayer(player);
                Runner.Despawn(leavingPlayer.Object);
            }
        }
    }
}