using System;
using KanKikuchi.AudioManager;
using UnityEngine;

namespace EyeMoT.Baloon
{
    public class Balloon : MonoBehaviour
    {
        [Header("Resources")]
        [SerializeField] private GameObject _balloonVisual;
        [SerializeField] private Rigidbody _rigidbody;
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

        #region default paramaters
        private Vector3 _defaultVisualScale;
        private Vector3 _defaultCollisionScale;
        private float _defaultCollisionRadius;
        private float _defaultCollisionHight;
        #endregion

        void Start()
        {
            if (_rigidbody == null)
                _rigidbody = GetComponent<Rigidbody>();

            if (_balloonVisual != null)
                _balloonVisualDefaultLocalPosition = _balloonVisual.transform.localPosition;

            SetDefault();
            UpdateData();
        }

        void Update()
        {
            if (!_isHit)
            {
                ApplyShakeOffset(Vector3.zero);
                return;
            }

            _hitTime += Time.deltaTime;

            if (_shakeOnHit)
                ApplyShakeOffset(GetShakeOffset());
            else
                ApplyShakeOffset(Vector3.zero);

            if (_hitTime >= _lifeTime)
            {   
                _onLifeTimeExpired?.Invoke(this);
            }
        }

        public void Initialize(Color color, Action<Balloon> onLifeTimeExpired)
        {
            _onLifeTimeExpired += onLifeTimeExpired;

            if (_balloonVisual != null)
                _balloonVisual.GetComponent<Renderer>().material.color = color;
        }

        public void StartMove(Vector3 targetDirection, float moveSpeed)
        {
            _moveTargetDirection = targetDirection.normalized;
            _moveSpeed = Mathf.Max(0f, moveSpeed);

            if (_rigidbody != null)
                _rigidbody.velocity = _moveTargetDirection * _moveSpeed;
        }

        public void StartBalloonDestroy(float lifeTime)
        {
            _isHit = true;
            _lifeTime = lifeTime;
        }

        public void OnHitLineBeam()
        {
            _isHit = true;
        }

        public void OnMissLineBeam()
        {
            _isHit = false;
            ApplyShakeOffset(Vector3.zero);

            if (_hitTimeResetOnMiss)
                _hitTime = 0f;
        }

        public void UpdateData()
        {
            _balloonVisual.transform.localScale = _defaultVisualScale * SettingManager.Instance.BalloonData.VisualScale;

            _collisionVisual.transform.localScale = _defaultCollisionScale * SettingManager.Instance.BalloonData.CollisionScale;
            var collision = GetComponent<CapsuleCollider>();
            collision.radius = _defaultCollisionRadius * SettingManager.Instance.BalloonData.CollisionScale;
            collision.height = _defaultCollisionHight * SettingManager.Instance.BalloonData.CollisionScale;

            _lifeTime = SettingManager.Instance.BalloonData.LifeTime;
        }

        public void VisibleCollision(bool isVisible) => _collisionVisual.enabled = isVisible;

        private void SetDefault()
        {
            _defaultVisualScale = _balloonVisual.transform.localScale;
            _defaultCollisionScale = _collisionVisual.transform.localScale;
            var collision = GetComponent<CapsuleCollider>();
            _defaultCollisionRadius = collision.radius;
            _defaultCollisionHight = collision.height;
        }

        #region Shake Effect
        private void ApplyShakeOffset(Vector3 offset)
        {
            if (_balloonVisual == null)
                return;

            _balloonVisual.transform.localPosition = _balloonVisualDefaultLocalPosition + offset;
        }

        private Vector3 GetShakeOffset()
        {
            float time = Time.time * _shakeSpeed;
            return new Vector3(
                Mathf.PerlinNoise(time, 0f) - 0.5f,
                Mathf.PerlinNoise(0f, time) - 0.5f,
                Mathf.PerlinNoise(time, time) - 0.5f
            ) * (_shakeStrength * 2f);
        }
        #endregion
    }
}
