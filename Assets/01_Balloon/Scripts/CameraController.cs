using UnityEngine;

namespace EyeMoT.Baloon
{
    public class CameraController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _sensitivity = 3f;
        [SerializeField] private Vector2 _xRotateLimit = new Vector2(-10f, 10f);
        [SerializeField] private Vector2 _yRotateLimit = new Vector2(-10f, 10f);

        private Vector3 _initRotate;
        private Vector2 _currentRotate;

        void Start()
        {
            _initRotate = transform.rotation.eulerAngles;
            _currentRotate = _initRotate;
        }

        void FixedUpdate()
        {
            UpdateRotation();
        }

        private void UpdateRotation()
        {
            float normalizedMouseX = Screen.width > 0
                ? Mathf.Clamp01(Input.mousePosition.x / Screen.width)
                : 0.5f;
            float normalizedMouseY = Screen.height > 0
                ? Mathf.Clamp01(Input.mousePosition.y / Screen.height)
                : 0.5f;

            float targetYaw = _initRotate.y + Mathf.Lerp(_yRotateLimit.x, _yRotateLimit.y, normalizedMouseX);
            float targetPitch = _initRotate.x + Mathf.Lerp(_xRotateLimit.y, _xRotateLimit.x, normalizedMouseY);
            float lerpFactor = 1f - Mathf.Exp(-Mathf.Max(0f, _sensitivity) * Time.fixedDeltaTime);

            _currentRotate.x = Mathf.LerpAngle(_currentRotate.x, targetPitch, lerpFactor);
            _currentRotate.y = Mathf.LerpAngle(_currentRotate.y, targetYaw, lerpFactor);

            transform.rotation = Quaternion.Euler(_currentRotate.x, _currentRotate.y, 0f);
        }
    }
}
