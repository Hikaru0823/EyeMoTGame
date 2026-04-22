using System;
using System.Collections.Generic;
using EyeMoT.Fusion;
using Fusion;
using Fusion.Addons.Physics;
using KanKikuchi.AudioManager;
using UnityEngine;

namespace EyeMoT.Balloon
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
        private Vector3 _moveTargetDirection;
        private float _moveSpeed;
        private Vector3 _balloonVisualDefaultLocalPosition;
        private readonly HashSet<PlayerRef> _hitSources = new HashSet<PlayerRef>();

        [Networked] private NetworkBool IsHit { get; set; }
        [Networked] private float HitTime { get; set; }
        [Networked] private float NetworkedLifeTime { get; set; }

        [Networked, OnChangedRender(nameof(OnColorChanged))]
        public Color NetworkedColor { get; set; }
        [Networked] private bool _effectEnable { get; set; } = false;

        #region default paramaters
        private Vector3 _defaultVisualScale;
        private Vector3 _defaultCollisionScale;
        private float _defaultCollisionRadius;
        private float _defaultCollisionHight;
        #endregion
        private bool _hasDefaultValues;

        public override void Spawned()
        {
            _hitSources.Clear();
            InitializeComponents();
            UpdateData();

            if (Object.HasStateAuthority)
            {
                _lifeTime = SettingManager.Instance.BalloonData.LifeTime;
                NetworkedLifeTime = _lifeTime;
            }
            OnColorChanged();
            VisibleCollision(false);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if(!_effectEnable) return;
            BalloonSpawnManager.Instance.PlayDestroyEffects(transform.position);
        }


        public override void FixedUpdateNetwork()
        {
            TickBalloon(Runner.DeltaTime, Object.HasStateAuthority);
        }

        public override void Render()
        {
            if (_balloonVisual == null)
                return;

            
            if (!IsHit)
            {
                ApplyShakeOffset(Vector3.zero);
                return;
            }

            // HitTime は Networked なので全端末でだいたい揃う
            ApplyShakeOffset(GetShakeOffset(HitTime));
        }

        private void TickBalloon(float deltaTime, bool canExpire)
        {
            if (!IsHit)
            {
                ApplyShakeOffset(Vector3.zero);
                return;
            }

            HitTime += deltaTime * Mathf.Max(1, _hitSources.Count);

            if (canExpire && HitTime >= NetworkedLifeTime)
            {   
                _onLifeTimeExpired?.Invoke(this);
                BalloonSpawnManager.Instance.DestroyBalloon(this);
            }
        }

        public void StartMove(Vector3 targetDirection, float moveSpeed)
        {
            _moveTargetDirection = targetDirection.normalized;
            _moveSpeed = Mathf.Max(0f, moveSpeed);
            Rpc_SetMoveState(_moveTargetDirection, _moveSpeed);

            if (_networkRigidbody != null)
                _networkRigidbody.Rigidbody.velocity = _moveTargetDirection * _moveSpeed;
        }

        public void StartBalloonDestroy(float lifeTime)
        {
            SetEffectEnable(true);
            Rpc_SetLifeTime(lifeTime);
            Rpc_SetHitState(true);
        }

        public void OnHitLineBeam(PlayerRef source)
        {
            if (!Object || !Object.IsValid)
                return;
            Rpc_SetHitSourceState(source, true);
        }

        public void OnMissLineBeam(PlayerRef source)
        {
            if (!Object || !Object.IsValid)
                return;

            Rpc_SetHitSourceState(source, false);
        }

        public void SetEffectEnable(bool enable)
        {
            _effectEnable = enable;
        }

        public void UpdateData()
        {
            _balloonVisual.transform.localScale = _defaultVisualScale * SettingManager.Instance.BalloonData.VisualScale;

            _collisionVisual.transform.localScale = _defaultCollisionScale * SettingManager.Instance.BalloonData.CollisionScale;
            var collision = GetComponent<CapsuleCollider>();
            collision.radius = _defaultCollisionRadius * SettingManager.Instance.BalloonData.CollisionScale;
            collision.height = _defaultCollisionHight * SettingManager.Instance.BalloonData.CollisionScale;
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

        private void ResetMissStateIfNeeded()
        {
            ApplyShakeOffset(Vector3.zero);

            if (_hitTimeResetOnMiss)
                Rpc_SetHitTime(0f);
        }

        private void OnColorChanged()
        {
            if (_balloonVisual != null)
                _balloonVisual.GetComponent<Renderer>().material.color = NetworkedColor;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void Rpc_SetHitState(bool isHit)
        {
            if (!isHit)
                _hitSources.Clear();

            IsHit = isHit;
            _effectEnable = isHit;

            if (!isHit)
            {    
                ResetMissStateIfNeeded();
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void Rpc_SetHitSourceState(PlayerRef source, bool isHit, RpcInfo info = default)
        {
            if (!source.IsRealPlayer && info.Source.IsRealPlayer)
                source = info.Source;

            if (!source.IsRealPlayer)
            {
                Rpc_SetHitState(isHit);
                if (!isHit)
                    ResetMissStateIfNeeded();
                return;
            }

            if (isHit)
                _hitSources.Add(source);
            else
                _hitSources.Remove(source);

            Rpc_SetHitState(_hitSources.Count > 0);

            if (_hitSources.Count == 0)
                ResetMissStateIfNeeded();
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
