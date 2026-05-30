using UnityEngine;
using UnityEngine.UI;

namespace EyeMoT
{
    public class CursorManager : Singleton<CursorManager>
    {
        [Header("Resources")]
        [SerializeField] private Image _cursorImage;
        [Header("Settings")]
        [SerializeField] private Vector2 _offset;

        private RectTransform _cursorRect;
        private RectTransform _cursorParentRect;
        private Canvas _canvas;

        public bool IsCursorVisible => _cursorImage.enabled;

        protected override void OnAwake()
        {
            if(_cursorImage == null) return;

            _cursorRect = _cursorImage.GetComponent<RectTransform>();
            _cursorParentRect = _cursorRect.parent as RectTransform;
            _canvas = _cursorImage.GetComponentInParent<Canvas>();
        }

        void Start()
        {
            Cursor.visible = false;
            SetCursorVisible(true);
        }

        public void SetCursorVisible(bool isVisible)
        {
            _cursorImage.enabled = isVisible;
        }

        void LateUpdate()
        {
            if(_cursorRect == null || _cursorParentRect == null || _canvas == null) return;

            var camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(_cursorParentRect, Input.mousePosition, camera, out var cursorPos))
                _cursorRect.anchoredPosition = cursorPos + _offset;
        }
    }
}
