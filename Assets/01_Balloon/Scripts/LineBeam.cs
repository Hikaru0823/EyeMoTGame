using KanKikuchi.AudioManager;
using UnityEngine;

namespace EyeMoT.Baloon
{
    public class LineBeam : MonoBehaviour
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

        void Start()
        {
            if (_targetCamera == null)
                _targetCamera = Camera.main;

            if (_lineRenderer != null)
            {
                _lineRenderer = GetComponent<LineRenderer>();
                _lineRenderer.positionCount = 2;
                _lineRenderer.enabled = false;
            }

            _hitEffect.SetActive(false);
            _targetImage.SetActive(false);
        }

        void Update()
        {
            UpdateLineBeam();
        }

        private void UpdateLineBeam()
        {
            if (_lineRenderer == null || _targetCamera == null)
                return;

            Ray ray = _targetCamera.ScreenPointToRay(Input.mousePosition);
            bool hasRaycastHit = Physics.Raycast(ray, out RaycastHit hit);

            bool hasHitTarget = hasRaycastHit && hit.collider.CompareTag(_targetTag);
            bool hasHitVolume = hasRaycastHit && hit.collider.CompareTag(_volumeTag);

            if (hasHitVolume) LookAtTarget(hit.point);

            if (_hasHitTarget != hasHitTarget)
            {
                _hasHitTarget = hasHitTarget;
                OnHitTargetChanged(_hasHitTarget);
            }

            _lineRenderer.enabled = hasHitTarget;

            Balloon hitBalloon = hasHitTarget ? hit.collider.GetComponent<Balloon>() : null;

            if (_currentBalloon != hitBalloon)
            {
                if (_currentBalloon != null)
                    OnMissTarget();

                _currentBalloon = hitBalloon;

                if (_currentBalloon != null)
                    OnHitTarget();
            }

            if (!hasHitTarget)
                return;

            LookAtTarget(_currentBalloon.transform.position);
            _hitEffect.transform.position = GetEffectOffset();
            _targetImage.transform.position = _currentBalloon.transform.position;
            _endPoint = _currentBalloon.transform.position;
            _startPoint = transform.position;
            _lineRenderer.SetPosition(0, _startPoint);
            _lineRenderer.SetPosition(1, _endPoint);
        }

        private void LookAtTarget(Vector3 position)
        {
            Vector3 lookDirection = position - transform.position;

            if (lookDirection.sqrMagnitude > 0f)
                transform.rotation = Quaternion.LookRotation(lookDirection);
        }

        private void OnHitTargetChanged(bool hasHitTarget)
        {
            if(hasHitTarget)
            {
                SEManager.Instance.Play(SEPath.BEAM);
            }
            else
            {
                _hitEffect.SetActive(false);
                _targetImage.SetActive(false);
                SEManager.Instance.Stop(SEPath.BEAM);
            }
        }

        private void OnHitTarget()
        {
            _hitEffect.SetActive(true);
            _targetImage.SetActive(true);
            _targetImage.transform.localScale = _currentBalloon.transform.localScale * _targetImageRatio; // Adjust target image size based on balloon size
            _currentBalloon.OnHitLineBeam();
            _endPoint = _currentBalloon.transform.position;
        }

        private void OnMissTarget()
        {
            _currentBalloon.OnMissLineBeam();
            _hitEffect.SetActive(false);
            _targetImage.SetActive(false);
        }

        private Vector3 GetEffectOffset()
        {
            Vector3 effectPosition = _currentBalloon.transform.position;
            Vector3 toBeamOrigin = transform.position - effectPosition;

            if (toBeamOrigin.sqrMagnitude > 0f)
                effectPosition += toBeamOrigin.normalized * _hitEffectOffset;

            return effectPosition;
        }
    }
}
