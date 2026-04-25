using EyeMoT.Fusion;
using Fusion;
using KanKikuchi.AudioManager;
using UnityEngine;

namespace EyeMoT.Balloon
{
    public class LineBeam : NetworkBehaviour
    {
        public static LineBeam Local { get; private set; }

        [Header("Resources")]
        [SerializeField] private LineRenderer _lineRenderer;
        [SerializeField] private GameObject _hitEffect;
        [SerializeField] private GameObject _targetImage;
        [SerializeField] private Renderer _visualRenderer;
        [SerializeField] private GameObject _readyStateVisual;
        private Camera _targetCamera;

        [Header("Settings")]
        [SerializeField] private string _targetTag = "Balloon";
        [SerializeField] private string _volumeTag = "BalloonVolume";
        [SerializeField] private float _targetImageRatio = 0.15f;
        [SerializeField] private float _targetImageScaleOnMiss = 0.15f;
        [SerializeField] private float _hitNotifyInterval = 0.1f;
        [SerializeField] private float _hitEffectOffset = 0.53f;
        [SerializeField] private float _noLocalAlpha = 0.4f;

        private Vector3 _startPoint;
        private Vector3 _endPoint;
        private Vector3 _targetPosition;

        private Balloon _currentBalloon;
        private bool _hasHitTarget;
        private bool _isBeamSoundPlaying;
        private bool _componentsInitialized;
        private double _nextHitNotifyTime;
        [Networked, OnChangedRender(nameof(OnNetworkedReadyChanged))] 
        public bool NetworkedReady { get; set; } = false;

        [Networked, OnChangedRender(nameof(OnNetworkedHasHitTargetChanged))]
        private bool NetworkedHasHitTarget { get; set; }
        [Networked] private Vector3 NetworkedStartPoint { get; set; }
        [Networked] private Vector3 NetworkedEndPoint { get; set; }
        [Networked] private Vector3 NetworkedTargetPosition { get; set; }


        public override void Spawned()
        {
            if(Object.HasInputAuthority)
                Local = this;
            InitializeComponents();
            ApplyNetworkedVisuals();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            StopBeamSound();
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasInputAuthority)
                return;

            if (GetInput(out BalloonNetworkInput input) && input.HasMouse)
                UpdateLineBeam(input.MouseUV, input.ScreenAspect);
            else
                ClearLineBeam(false);

            //_targetImage.transform.position = _targetPosition;

            Rpc_SetNetworkedVisuals(
                _hasHitTarget,
                _startPoint,
                _endPoint,
                _targetPosition);
        }

        public override void Render()
        {
            ApplyNetworkedVisuals();
        }

        private void InitializeComponents()
        {
            if (_componentsInitialized)
                return;

            _targetCamera = Camera.main;
            _readyStateVisual.SetActive(false);

            if (EnsureLineRendererPositionCount())
                _lineRenderer.enabled = false;

            _targetImage.transform.localScale = Vector3.one * SettingManager.Instance.BalloonData.VisualScale * _targetImageScaleOnMiss;
            var teamColor = GameManager.TeamColor[PlayerRegistry.GetPlayer(Object.InputAuthority)?.IndexByTeam ?? 0];
            _targetImage.GetComponent<SpriteRenderer>().color = Object.HasInputAuthority ? teamColor : new Color(teamColor.r, teamColor.g, teamColor.b, _noLocalAlpha);
            _visualRenderer.material.color = teamColor;

            if(LobbyManager.Instance.Runner.GameMode == GameMode.Single)
            {    
                _targetImage.GetComponent<SpriteRenderer>().color = Object.HasInputAuthority ? Color.yellow : new Color(1f, 1f, 0, _noLocalAlpha);
                _visualRenderer.material.color = Color.black;
            }

            _componentsInitialized = true;
        }

        private bool EnsureLineRendererPositionCount()
        {
            if (_lineRenderer.positionCount < 2)
                _lineRenderer.positionCount = 2;

            return true;
        }

        private Ray GetClientViewportRay(Vector2 mouseUV, float screenAspect)
        {
            if (screenAspect <= 0f)
                return _targetCamera.ViewportPointToRay(new Vector3(mouseUV.x, mouseUV.y, 0f));

            float originalAspect = _targetCamera.aspect;
            try
            {
                _targetCamera.aspect = screenAspect;
                return _targetCamera.ViewportPointToRay(new Vector3(mouseUV.x, mouseUV.y, 0f));
            }
            finally
            {
                _targetCamera.aspect = originalAspect;
            }
        }

        private void UpdateLineBeam(Vector2 mouseUV, float screenAspect)
        {
            if (_targetCamera == null)
                return;

            Ray ray = GetClientViewportRay(mouseUV, screenAspect);
            bool hasRaycastHit = Physics.Raycast(ray, out RaycastHit hit);

            _hasHitTarget = hasRaycastHit && hit.collider.CompareTag(_targetTag);
            bool hasHitVolume = hasRaycastHit && hit.collider.CompareTag(_volumeTag);

            if(hasHitVolume)
                _targetPosition = hit.point;

            Balloon hitBalloon = _hasHitTarget ? hit.collider.GetComponent<Balloon>() : null;

            if (_currentBalloon != hitBalloon)
            {
                if (_currentBalloon != null)
                    OnMissTarget(true);

                _currentBalloon = hitBalloon;

                if (_currentBalloon != null)
                    OnHitTarget(true);
            }

            if(_hasHitTarget)
            {
                _targetPosition = _currentBalloon.transform.position;
                _startPoint = transform.position;
                _endPoint = _currentBalloon.transform.position;
                NotifyCurrentBalloonHit(true);
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
            }
        }

        private void OnHitTarget(bool canNotifyBalloon)
        {
            _nextHitNotifyTime = 0f;
            NotifyCurrentBalloonHit(canNotifyBalloon);
            _targetPosition = _currentBalloon.transform.position;
        }

        private void OnMissTarget(bool canNotifyBalloon)
        {
            if (canNotifyBalloon)
                _currentBalloon.OnMissLineBeam(Object.InputAuthority);
        }

        //一回だけHitEventを発火してもホストに届かないこともあるため、一定時間ごとにHitEventを発火する
        private void NotifyCurrentBalloonHit(bool canNotifyBalloon)
        {
            if (!canNotifyBalloon || _currentBalloon == null)
                return;

            if (Runner != null && Runner.SimulationTime < _nextHitNotifyTime)
                return;

            _currentBalloon.OnHitLineBeam(Object.InputAuthority);

            if (Runner != null)
                _nextHitNotifyTime = Runner.SimulationTime + Mathf.Max(0.02f, _hitNotifyInterval);
        }


        private void ApplyNetworkedVisuals()
        {
            ApplyNetworkedVisuals(
                NetworkedHasHitTarget,
                NetworkedStartPoint,
                NetworkedEndPoint,
                NetworkedTargetPosition);
        }

        private void ApplyNetworkedVisuals(
            bool hasHitTarget,
            Vector3 startPoint,
            Vector3 endPoint,
            Vector3 targetPosition)
        {
            ApplyLookRotation(targetPosition);

            if (EnsureLineRendererPositionCount())
            {
                _lineRenderer.SetPosition(0, startPoint);
                _lineRenderer.SetPosition(1, endPoint);
            }

            //ローカルで処理する
            if (Object.HasInputAuthority)
                _targetImage.transform.position = _targetPosition;
            else
                _targetImage.transform.position = targetPosition;

            if(hasHitTarget)
                _hitEffect.transform.position = GetEffectOffset(targetPosition);
        }

        private Vector3 GetEffectOffset(Vector3 balloonPosition)
        {
            Vector3 effectPosition = balloonPosition;
            Vector3 toBeamOrigin = transform.position - effectPosition;

            if (toBeamOrigin.sqrMagnitude > 0f)
                effectPosition += toBeamOrigin.normalized * _hitEffectOffset;

            return effectPosition;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, Channel = RpcChannel.Unreliable)]
        private void Rpc_SetNetworkedVisuals(
            bool hasHitTarget,
            Vector3 startPoint,
            Vector3 endPoint,
            Vector3 targetPosition)
        {
            NetworkedHasHitTarget = hasHitTarget;
            NetworkedStartPoint = startPoint;
            NetworkedEndPoint = endPoint;
            NetworkedTargetPosition = targetPosition;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void Rpc_SetReadyState(bool isReady)
        {
            NetworkedReady = isReady;
        }

        private void OnNetworkedHasHitTargetChanged()
        {
            _lineRenderer.enabled = NetworkedHasHitTarget;
            _hitEffect.SetActive(NetworkedHasHitTarget);

            if(!Object.HasInputAuthority) return;

            if (NetworkedHasHitTarget)
            {    
                _targetImage.transform.localScale = Vector3.one * SettingManager.Instance.BalloonData.VisualScale * _targetImageRatio;
                PlayBeamSound();
            }
            else
            {    
                _targetImage.transform.localScale = Vector3.one * SettingManager.Instance.BalloonData.VisualScale * _targetImageScaleOnMiss;
                StopBeamSound();
            }
        }

        private void OnNetworkedReadyChanged()
        {
            _readyStateVisual.SetActive(NetworkedReady);
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
