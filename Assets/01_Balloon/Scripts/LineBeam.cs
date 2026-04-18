using Fusion;
using EyeMoT.Fusion;
using KanKikuchi.AudioManager;
using UnityEngine;

namespace EyeMoT.Baloon
{
    public class LineBeam : NetworkBehaviour
    {
        [Header("Resources")]
        [SerializeField] private LineRenderer _lineRenderer;
        [SerializeField] private Camera _targetCamera;
        [SerializeField] private GameObject _hitEffect;
        [SerializeField] private GameObject _targetImage;

        [Header("Settings")]
        [SerializeField] private string _targetTag = "Balloon";
        [SerializeField] private string _volumeTag = "BalloonVolume";
        [SerializeField] private float _hitEffectOffset = 0.13f;
        [SerializeField] private float _targetImageRatio = 0.15f;

        private Vector3 _startPoint;
        private Vector3 _endPoint;

        private Balloon _currentBalloon;
        private bool _hasHitTarget;
        private bool _isBeamSoundPlaying;

        [Networked, OnChangedRender(nameof(OnNetworkedHasHitTargetChanged))]
        private bool NetworkedHasHitTarget { get; set; }
        [Networked] private bool NetworkedHasLookTarget { get; set; }
        [Networked] private Vector3 NetworkedLookTarget { get; set; }
        [Networked] private Vector3 NetworkedStartPoint { get; set; }
        [Networked] private Vector3 NetworkedEndPoint { get; set; }
        [Networked] private Vector3 NetworkedHitEffectPosition { get; set; }
        [Networked] private Vector3 NetworkedTargetImagePosition { get; set; }
        [Networked] private Vector3 NetworkedTargetImageScale { get; set; }

        private bool IsNetworkSpawned => Object != null && Object.IsValid;


        public override void Spawned()
        {
            if (!Object.HasInputAuthority && !Object.HasStateAuthority) return;
            InitializeComponents();
            ApplyNetworkedVisuals();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            StopBeamSound();
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;

            if (GetInput(out EyeMoTNetworkInput input) && input.HasMouse)
                UpdateLineBeam(true, input.MouseUV);
            else
                ClearLineBeam(true);
        }

        public override void Render()
        {
            if (IsNetworkSpawned)
                ApplyNetworkedVisuals();
        }

        private void InitializeComponents()
        {
            if (_targetCamera == null)
                _targetCamera = Camera.main;

            if (_lineRenderer == null)
                _lineRenderer = GetComponent<LineRenderer>();

            if (_lineRenderer != null)
            {
                _lineRenderer.positionCount = 2;
                _lineRenderer.enabled = false;
            }

            if (_hitEffect != null)
                _hitEffect.SetActive(false);

            if (_targetImage != null)
                _targetImage.SetActive(false);
        }

        private void UpdateLineBeam(bool canNotifyBalloon, Vector2 mouseUV)
        {
            if (_lineRenderer == null || _targetCamera == null)
                return;

            Ray ray = _targetCamera.ViewportPointToRay(new Vector3(mouseUV.x, mouseUV.y, 0f));
            bool hasRaycastHit = Physics.Raycast(ray, out RaycastHit hit);

            bool hasHitTarget = hasRaycastHit && hit.collider.CompareTag(_targetTag);
            bool hasHitVolume = hasRaycastHit && hit.collider.CompareTag(_volumeTag);

            if (hasHitVolume) LookAtTarget(hit.point);

            if (_hasHitTarget != hasHitTarget)
            {
                _hasHitTarget = hasHitTarget;
                OnHitTargetChanged(_hasHitTarget);
            }

            SetLineVisible(hasHitTarget);

            Balloon hitBalloon = hasHitTarget ? hit.collider.GetComponent<Balloon>() : null;

            if (_currentBalloon != hitBalloon)
            {
                if (_currentBalloon != null)
                    OnMissTarget(canNotifyBalloon);

                _currentBalloon = hitBalloon;

                if (_currentBalloon != null)
                    OnHitTarget(canNotifyBalloon);
            }

            if (!hasHitTarget)
                return;

            LookAtTarget(_currentBalloon.transform.position);

            if (_hitEffect != null)
                _hitEffect.transform.position = GetEffectOffset();

            if (_targetImage != null)
                _targetImage.transform.position = _currentBalloon.transform.position;

            _endPoint = _currentBalloon.transform.position;
            _startPoint = transform.position;
            SetLinePositions(_startPoint, _endPoint);

            if (IsNetworkSpawned)
            {
                NetworkedHitEffectPosition = _hitEffect != null ? _hitEffect.transform.position : _endPoint;
                NetworkedTargetImagePosition = _targetImage != null ? _targetImage.transform.position : _endPoint;
                NetworkedTargetImageScale = _targetImage != null ? _targetImage.transform.localScale : Vector3.one;
            }
        }

        private void ClearLineBeam(bool canNotifyBalloon)
        {
            if (_currentBalloon != null)
            {
                OnMissTarget(canNotifyBalloon);
                _currentBalloon = null;
            }

            if (_hasHitTarget)
            {
                _hasHitTarget = false;
                OnHitTargetChanged(false);
            }

            SetLineVisible(false);
        }

        private void LookAtTarget(Vector3 position)
        {
            Vector3 lookDirection = position - transform.position;

            if (lookDirection.sqrMagnitude > 0f)
                transform.rotation = Quaternion.LookRotation(lookDirection);

            if (!IsNetworkSpawned)
                return;

            NetworkedHasLookTarget = true;
            NetworkedLookTarget = position;
        }

        private void OnHitTargetChanged(bool hasHitTarget)
        {
            if (IsNetworkSpawned)
                NetworkedHasHitTarget = hasHitTarget;

            if(hasHitTarget)
            {
                PlayBeamSound();
            }
            else
            {
                SetHitVisualsActive(false);
                StopBeamSound();
            }
        }

        private void OnHitTarget(bool canNotifyBalloon)
        {
            SetHitVisualsActive(true);

            if (_targetImage != null)
                _targetImage.transform.localScale = _currentBalloon.transform.localScale * _targetImageRatio;

            if (canNotifyBalloon)
                _currentBalloon.OnHitLineBeam();
            _endPoint = _currentBalloon.transform.position;
        }

        private void OnMissTarget(bool canNotifyBalloon)
        {
            if (canNotifyBalloon)
                _currentBalloon.OnMissLineBeam();

            SetHitVisualsActive(false);
        }

        private Vector3 GetEffectOffset()
        {
            Vector3 effectPosition = _currentBalloon.transform.position;
            Vector3 toBeamOrigin = transform.position - effectPosition;

            if (toBeamOrigin.sqrMagnitude > 0f)
                effectPosition += toBeamOrigin.normalized * _hitEffectOffset;

            return effectPosition;
        }

        private void SetLineVisible(bool isVisible)
        {
            if (_lineRenderer != null)
                _lineRenderer.enabled = isVisible;

            if (IsNetworkSpawned)
                NetworkedHasHitTarget = isVisible;
        }

        private void SetLinePositions(Vector3 startPoint, Vector3 endPoint)
        {
            if (_lineRenderer != null)
            {
                _lineRenderer.SetPosition(0, startPoint);
                _lineRenderer.SetPosition(1, endPoint);
            }

            if (!IsNetworkSpawned)
                return;

            NetworkedStartPoint = startPoint;
            NetworkedEndPoint = endPoint;
        }

        private void SetHitVisualsActive(bool isActive)
        {
            if (_hitEffect != null)
                _hitEffect.SetActive(isActive);

            if (_targetImage != null)
                _targetImage.SetActive(isActive);
        }

        private void ApplyNetworkedVisuals()
        {
            if (NetworkedHasLookTarget)
                ApplyLookRotation(NetworkedLookTarget);

            if (_lineRenderer != null)
            {
                _lineRenderer.enabled = NetworkedHasHitTarget;
                _lineRenderer.SetPosition(0, NetworkedStartPoint);
                _lineRenderer.SetPosition(1, NetworkedEndPoint);
            }

            SetHitVisualsActive(NetworkedHasHitTarget);

            if (_hitEffect != null)
                _hitEffect.transform.position = NetworkedHitEffectPosition;

            if (_targetImage != null)
            {
                _targetImage.transform.position = NetworkedTargetImagePosition;
                _targetImage.transform.localScale = NetworkedTargetImageScale;
            }
        }

        private void OnNetworkedHasHitTargetChanged()
        {
            if (NetworkedHasHitTarget)
                PlayBeamSound();
            else
                StopBeamSound();
        }

        private void ApplyLookRotation(Vector3 position)
        {
            Vector3 lookDirection = position - transform.position;

            if (lookDirection.sqrMagnitude > 0f)
                transform.rotation = Quaternion.LookRotation(lookDirection);
        }

        private void PlayBeamSound()
        {
            if (_isBeamSoundPlaying)
                return;

            _isBeamSoundPlaying = true;
            SEManager.Instance.Play(SEPath.BEAM);
        }

        private void StopBeamSound()
        {
            if (!_isBeamSoundPlaying)
                return;

            _isBeamSoundPlaying = false;
            SEManager.Instance.Stop(SEPath.BEAM);
        }
    }
}
