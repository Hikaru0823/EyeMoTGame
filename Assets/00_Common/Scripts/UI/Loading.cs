using UnityEngine;
using UnityEngine.UIElements;

namespace EyeMoT
{
    public class Loading : Singleton<Loading>
    {
        [Header("Resources")]
        [SerializeField] private Canvas _loadingCanvas;

        public void SetVisible(bool isVisible)
        {
            _loadingCanvas.enabled = isVisible;
        }
    }
}