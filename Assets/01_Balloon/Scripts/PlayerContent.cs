using System.Linq;
using EyeMoT.Fusion;
using Fusion;
using UnityEngine;

namespace EyeMoT.Baloon
{
    public class PlayerContent : NetworkBehaviour
    {
        [Header("Resources")]
        [SerializeField] private GameObject[] _players;
        [SerializeField] private BalloonVolume _balloonVolume;
        
        [Header("Settings")]
        [SerializeField] private float _moveTime = 40f;
        [SerializeField] private float _moveDistance = 1f;

        private Vector3 _initPosition;
        private float _elapsedTime;

        [Networked] private Vector3 NetworkedInitPosition { get; set; }
        [Networked] private float NetworkedElapsedTime { get; set; }

        private bool IsNetworkSpawned => Object != null && Object.IsValid;

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_SetPlayer(int[] playersIdx)
        {
            var offset = playersIdx.Length == 1 ? 0 : (playersIdx.Length - 1) * 2 - 1;
            var idx = 0;
            foreach (var player in playersIdx)
            {    
                _players[player].SetActive(true);
                if(Object.HasStateAuthority)
                {
                    _players[player].transform.localPosition = new Vector3(-offset + idx * 2, _players[player].transform.localPosition.y, _players[player].transform.localPosition.z);
                    var playerEntry = PlayerRegistry.Where(p => p.IndexByTeam == player && p.Team == PlayerRegistry.TeamState.TeamA).FirstOrDefault()?.Ref;
                    if (playerEntry.HasValue)
                    {
                        foreach(var networkObject in _players[player].GetComponentsInChildren<NetworkObject>())
                        {
                            networkObject.AssignInputAuthority(playerEntry.Value);
                        }
                    }
                }
                idx++;
            }
            
            if(Object.HasStateAuthority)
                _balloonVolume.transform.localScale = new Vector3((playersIdx.Length-1) * 2 + _balloonVolume.transform.localScale.x, _balloonVolume.transform.localScale.y, _balloonVolume.transform.localScale.z);
        }

        public override void Spawned()
        {
            _initPosition = transform.position;

            if (Object.HasStateAuthority)
            {
                NetworkedInitPosition = _initPosition;
                NetworkedElapsedTime = 0f;
            }

            UpdatePosition(CurrentInitPosition, CurrentElapsedTime);
        }

        public override void FixedUpdateNetwork()
        {
            if (Object.HasStateAuthority)
                NetworkedElapsedTime += Runner.DeltaTime;

            UpdatePosition(CurrentInitPosition, CurrentElapsedTime);
        }

        private Vector3 CurrentInitPosition => IsNetworkSpawned ? NetworkedInitPosition : _initPosition;
        private float CurrentElapsedTime => IsNetworkSpawned ? NetworkedElapsedTime : _elapsedTime;

        private void UpdatePosition(Vector3 initPosition, float elapsedTime)
        {
            float xOffset = 0f;

            if (_moveTime > 0f)
            {
                float phase = elapsedTime / _moveTime * Mathf.PI * 2f;
                xOffset = Mathf.Sin(phase) * _moveDistance;
            }

            transform.position = initPosition + new Vector3(xOffset, 0f, 0f);
        }
    }
}
