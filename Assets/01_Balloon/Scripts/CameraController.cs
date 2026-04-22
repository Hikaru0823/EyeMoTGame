using System;
using UnityEngine;

namespace EyeMoT.Balloon
{
    public class CameraController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _sensitivity = 3f;
        [SerializeField] private Vector2 _xRotateLimit = new Vector2(-10f, 10f);
        [SerializeField] private Vector2 _yRotateLimit = new Vector2(-10f, 10f);

        private Camera _thisCamera;
        private Vector3 _initRotate;
        private Vector2 _currentRotate;

        private void Awake()
        {
            _thisCamera = GetComponent<Camera>();
            Camera.SetupCurrent(_thisCamera);
            InitializeRotation();
            ApplyRotation(_currentRotate);
        }

        void Start()
        {
            UpdateBackGround();
        }

        private void Update()
        {
            GetNormalizedMousePosition(out Vector2 mouseUV);
            UpdateRotation(Time.deltaTime, mouseUV);
        }

        private void InitializeRotation()
        {
            _initRotate = transform.rotation.eulerAngles;
            _currentRotate = _initRotate;
        }

        private void UpdateRotation(float deltaTime, Vector2 normalizedMousePosition)
        {
            _currentRotate = CalculateRotation(_initRotate, _currentRotate, deltaTime, normalizedMousePosition);
            ApplyRotation(_currentRotate);
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

        private void GetNormalizedMousePosition(out Vector2 normalizedMousePosition)
        {
            int width = Mathf.Max(1, Screen.width);
            int height = Mathf.Max(1, Screen.height);
            Vector3 mouse = Input.mousePosition;

            normalizedMousePosition = new Vector2(
                Mathf.Clamp01(mouse.x / width),
                Mathf.Clamp01(mouse.y / height));
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
                case PreviewManager.BGColor.Default:
                    _thisCamera.clearFlags = CameraClearFlags.Skybox;
                    _thisCamera.cullingMask |= (1 << stageLayer);
                    break;
                case PreviewManager.BGColor.White:
                    _thisCamera.clearFlags = CameraClearFlags.SolidColor;
                    _thisCamera.backgroundColor = Color.white;
                    _thisCamera.cullingMask &= ~(1 << stageLayer);
                    break;
                case PreviewManager.BGColor.Black:
                    _thisCamera.clearFlags = CameraClearFlags.SolidColor;
                    _thisCamera.backgroundColor = Color.black;
                    _thisCamera.cullingMask &= ~(1 << stageLayer);
                    break;
            }
        }
    }
}
