using System;
using Fusion;
using Fusion.Addons.Physics;
using KanKikuchi.AudioManager;
using UnityEngine;

namespace EyeMoT.Baloon
{
    public class Balloon : NetworkBehaviour
    {
        [Header("Resources")]
        [SerializeField] private GameObject _balloonVisual;
        [SerializeField] private NetworkRigidbody3D _networkRigidbody;
        [SerializeField] private MeshRenderer _collisionVisual;

        [Header("Settings")]
        [SerializeField] private float _lifeTime = 3.0f;
        [SerializeField] private bool _hitTimeResetOnMiss = false;
        [SerializeField] private bool _shakeOnHit = true;
        [SerializeField] private float _shakeStrength = 0.2f;
        [SerializeField] private float _shakeSpeed = 40f;

        private Action<Balloon> _onLifeTimeExpired;
        private bool _isHit = false;
        private float _hitTime = 0f;
        private Vector3 _moveTargetDirection;
        private float _moveSpeed;
        private Vector3 _balloonVisualDefaultLocalPosition;

        [Networked] private NetworkBool IsHit { get; set; }
        [Networked] private float HitTime { get; set; }
        [Networked] private float NetworkedLifeTime { get; set; }
        [Networked] private Vector3 NetworkedMoveTargetDirection { get; set; }
        [Networked] private float NetworkedMoveSpeed { get; set; }
        [Networked, OnChangedRender(nameof(OnColorChanged))]
        public Color NetworkedColor { get; set; }
        public bool EffectEnable = true;

        #region default paramaters
        private Vector3 _defaultVisualScale;
        private Vector3 _defaultCollisionScale;
        private float _defaultCollisionRadius;
        private float _defaultCollisionHight;
        #endregion

        private bool _hasDefaultValues;
        private bool IsNetworkSpawned => Object != null && Object.IsValid;
        private bool CurrentIsHit => IsNetworkSpawned ? IsHit : _isHit;
        private float CurrentHitTime => IsNetworkSpawned ? HitTime : _hitTime;
        private float CurrentLifeTime => IsNetworkSpawned ? NetworkedLifeTime : _lifeTime;

        public override void Spawned()
        {
            InitializeComponents();
            UpdateData();

            if (Object.HasStateAuthority)
                NetworkedLifeTime = _lifeTime;
            OnColorChanged();
            VisibleCollision(true);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if(!EffectEnable) return;
            BalloonSpawnManager.Instance.PlayDestroyEffects(transform.position);
        }

        void Update()
        {
            if (IsNetworkSpawned)
                return;

            TickBalloon(Time.deltaTime, true);
        }

        public override void FixedUpdateNetwork()
        {
            TickBalloon(Runner.DeltaTime, Object.HasStateAuthority);
        }

        public override void Render()
        {
            if (_balloonVisual == null)
                return;

            if (!CurrentIsHit)
            {
                ApplyShakeOffset(Vector3.zero);
                return;
            }

            // HitTime は Networked なので全端末でだいたい揃う
            ApplyShakeOffset(GetShakeOffset(CurrentHitTime));
        }

        private void TickBalloon(float deltaTime, bool canExpire)
        {
            if (!CurrentIsHit)
            {
                ApplyShakeOffset(Vector3.zero);
                return;
            }

            AddHitTime(deltaTime);

            if (canExpire && CurrentHitTime >= CurrentLifeTime)
            {   
                _onLifeTimeExpired?.Invoke(this);
                BalloonSpawnManager.Instance.DestroyBalloon(this);
            }
        }

        public void StartMove(Vector3 targetDirection, float moveSpeed)
        {
            _moveTargetDirection = targetDirection.normalized;
            _moveSpeed = Mathf.Max(0f, moveSpeed);
            SetMoveState(_moveTargetDirection, _moveSpeed);

            if (_networkRigidbody != null)
                _networkRigidbody.Rigidbody.velocity = _moveTargetDirection * _moveSpeed;
        }

        public void StartBalloonDestroy(float lifeTime)
        {
            EffectEnable = true;
            _lifeTime = lifeTime;
            SetLifeTime(lifeTime);
            SetHitState(true);
        }

        public void OnHitLineBeam()
        {
            SetHitState(true);
        }

        public void OnMissLineBeam()
        {
            SetHitState(false);
            ApplyShakeOffset(Vector3.zero);

            if (_hitTimeResetOnMiss)
                SetHitTime(0f);
        }

        public void UpdateData()
        {
            _balloonVisual.transform.localScale = _defaultVisualScale * SettingManager.Instance.BalloonData.VisualScale;

            _collisionVisual.transform.localScale = _defaultCollisionScale * SettingManager.Instance.BalloonData.CollisionScale;
            var collision = GetComponent<CapsuleCollider>();
            collision.radius = _defaultCollisionRadius * SettingManager.Instance.BalloonData.CollisionScale;
            collision.height = _defaultCollisionHight * SettingManager.Instance.BalloonData.CollisionScale;

            _lifeTime = SettingManager.Instance.BalloonData.LifeTime;
            SetLifeTime(_lifeTime);
        }

        public void VisibleCollision(bool isVisible) => _collisionVisual.enabled = isVisible;

        private void SetDefault()
        {
            if (_hasDefaultValues)
                return;

            _defaultVisualScale = _balloonVisual.transform.localScale;
            _defaultCollisionScale = _collisionVisual.transform.localScale;
            var collision = GetComponent<CapsuleCollider>();
            _defaultCollisionRadius = collision.radius;
            _defaultCollisionHight = collision.height;
            _hasDefaultValues = true;
        }

        private void InitializeComponents()
        {
            if (_networkRigidbody == null)
                _networkRigidbody = GetComponent<NetworkRigidbody3D>();

            if (_balloonVisual != null)
                _balloonVisualDefaultLocalPosition = _balloonVisual.transform.localPosition;

            SetDefault();
        }

        private void AddHitTime(float deltaTime)
        {
            if (IsNetworkSpawned)
            {
                if (Object.HasStateAuthority)
                    HitTime += deltaTime;
            }
            else
            {
                _hitTime += deltaTime;
            }
        }

        private void SetHitTime(float hitTime)
        {
            _hitTime = hitTime;

            if (!IsNetworkSpawned)
                return;

            if (Object.HasStateAuthority)
                HitTime = hitTime;
            else
                Rpc_SetHitTime(hitTime);
        }

        private void SetHitState(bool isHit)
        {
            _isHit = isHit;

            if (!IsNetworkSpawned)
                return;

            if (Object.HasStateAuthority)
                IsHit = isHit;
            else
                Rpc_SetHitState(isHit);
        }

        private void SetLifeTime(float lifeTime)
        {
            _lifeTime = lifeTime;

            if (!IsNetworkSpawned)
                return;

            if (Object.HasStateAuthority)
                NetworkedLifeTime = lifeTime;
            else
                Rpc_SetLifeTime(lifeTime);
        }

        private void SetMoveState(Vector3 targetDirection, float moveSpeed)
        {
            if (!IsNetworkSpawned)
                return;

            if (Object.HasStateAuthority)
            {
                NetworkedMoveTargetDirection = targetDirection;
                NetworkedMoveSpeed = moveSpeed;
            }
            else
            {
                Rpc_SetMoveState(targetDirection, moveSpeed);
            }
        }

        private void OnColorChanged()
        {
            if (_balloonVisual != null)
                _balloonVisual.GetComponent<Renderer>().material.color = NetworkedColor;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void Rpc_SetHitState(bool isHit)
        {
            IsHit = isHit;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void Rpc_SetHitTime(float hitTime)
        {
            HitTime = hitTime;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void Rpc_SetLifeTime(float lifeTime)
        {
            NetworkedLifeTime = lifeTime;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void Rpc_SetMoveState(Vector3 targetDirection, float moveSpeed)
        {
            NetworkedMoveTargetDirection = targetDirection;
            NetworkedMoveSpeed = moveSpeed;

            if (_networkRigidbody != null)
                _networkRigidbody.Rigidbody.velocity = targetDirection * moveSpeed;
        }

        #region Shake Effect
        private void ApplyShakeOffset(Vector3 offset)
        {
            if (_balloonVisual == null)
                return;

            _balloonVisual.transform.localPosition = _balloonVisualDefaultLocalPosition + offset;
        }

        private Vector3 GetShakeOffset(float t)
        {
            float time = t * _shakeSpeed;
            return new Vector3(
                Mathf.PerlinNoise(time, 0f) - 0.5f,
                Mathf.PerlinNoise(0f, time) - 0.5f,
                Mathf.PerlinNoise(time, time) - 0.5f
            ) * (_shakeStrength * 2f);
        }
        #endregion
    }
}
