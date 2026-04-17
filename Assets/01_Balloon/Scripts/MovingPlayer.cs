using Fusion;
using UnityEngine;

namespace EyeMoT.Baloon
{
    public class MovingPlayer : NetworkBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _moveTime = 40f;
        [SerializeField] private float _moveDistance = 1f;

        private Vector3 _initPosition;
        private float _elapsedTime;

        [Networked] private Vector3 NetworkedInitPosition { get; set; }
        [Networked] private float NetworkedElapsedTime { get; set; }

        private bool IsNetworkSpawned => Object != null && Object.IsValid;

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
