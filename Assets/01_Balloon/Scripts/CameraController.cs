using Fusion;
using UnityEngine;
using EyeMoT.Fusion;
using EyeMoT.Heatmap;
using static PreviewManager;

namespace EyeMoT.Baloon
{
    public class CameraController : NetworkBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _sensitivity = 3f;
        [SerializeField] private Vector2 _xRotateLimit = new Vector2(-10f, 10f);
        [SerializeField] private Vector2 _yRotateLimit = new Vector2(-10f, 10f);

        private Camera _thisCamera;
        private Vector3 _initRotate;
        private Vector2 _currentRotate;

        [Networked] private Vector3 NetworkedInitRotate { get; set; }
        [Networked] private Vector2 NetworkedCurrentRotate { get; set; }

        private bool IsNetworkSpawned => Object != null && Object.IsValid;

        public override void Spawned()
        {
            if(!gameObject.activeSelf) return;
            if(!Object.HasInputAuthority && !Object.HasStateAuthority) return;

            Debug.Log("Camera Spawned");
            _thisCamera = GetComponent<Camera>();
            InitializeRotation();

            if (Object.HasStateAuthority)
            {
                NetworkedInitRotate = _initRotate;
                NetworkedCurrentRotate = _currentRotate;
            }

            ApplyRotation(CurrentRotate);
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority && !Object.HasInputAuthority) return;
            
            if (GetInput(out EyeMoTNetworkInput input) && input.HasMouse)
            {
                UpdateRotation(Runner.DeltaTime, Object.HasStateAuthority, input.MouseUV);
            }
            else
            {
                ApplyRotation(CurrentRotate);
            }
        }

        private Vector3 CurrentInitRotate => IsNetworkSpawned && Object.HasStateAuthority ? NetworkedInitRotate : _initRotate;
        private Vector2 CurrentRotate => IsNetworkSpawned && Object.HasStateAuthority ? NetworkedCurrentRotate : _currentRotate;

        private void InitializeRotation()
        {
            _initRotate = transform.rotation.eulerAngles;
            _currentRotate = _initRotate;
        }

        private void UpdateRotation(float deltaTime, bool hasStateAuthority, Vector2 normalizedMousePosition)
        {
            Vector2 nextRotate = CalculateRotation(CurrentInitRotate, CurrentRotate, deltaTime, normalizedMousePosition);

            if (hasStateAuthority)
            {
                if (IsNetworkSpawned)
                    NetworkedCurrentRotate = nextRotate;
                else
                    _currentRotate = nextRotate;
            }
            else
            {
                _currentRotate = nextRotate;
            }

            ApplyRotation(nextRotate);
        }

        private Vector2 CalculateRotation(Vector3 initRotate, Vector2 currentRotate, float deltaTime, Vector2 normalizedMousePosition)
        {
            float targetYaw = initRotate.y + Mathf.Lerp(_yRotateLimit.x, _yRotateLimit.y, normalizedMousePosition.x);
            float targetPitch = initRotate.x + Mathf.Lerp(_xRotateLimit.y, _xRotateLimit.x, normalizedMousePosition.y);
            float lerpFactor = 1f - Mathf.Exp(-Mathf.Max(0f, _sensitivity) * deltaTime);

            currentRotate.x = Mathf.LerpAngle(currentRotate.x, targetPitch, lerpFactor);
            currentRotate.y = Mathf.LerpAngle(currentRotate.y, targetYaw, lerpFactor);

            return currentRotate;
        }

        private void ApplyRotation(Vector2 rotate)
        {
            transform.rotation = Quaternion.Euler(rotate.x, rotate.y, 0f);
        }

        public void UpdateBackGround()
        {
            int stageLayer = LayerMask.NameToLayer("Stage");

            switch(SettingManager.Instance.GameData.BGColor)
            {
                case BGColor.Default:
                    _thisCamera.clearFlags = CameraClearFlags.Skybox;
                    _thisCamera.cullingMask |= (1 << stageLayer);
                    break;
                case BGColor.White:
                    _thisCamera.clearFlags = CameraClearFlags.SolidColor;
                    _thisCamera.backgroundColor = Color.white;
                    _thisCamera.cullingMask &= ~(1 << stageLayer);
                    break;
                case BGColor.Black:
                    _thisCamera.clearFlags = CameraClearFlags.SolidColor;
                    _thisCamera.backgroundColor = Color.black;
                    _thisCamera.cullingMask &= ~(1 << stageLayer);
                    break;
            }
        }
    }
}
