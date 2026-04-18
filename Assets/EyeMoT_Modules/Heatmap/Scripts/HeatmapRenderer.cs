using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace EyeMoT.Heatmap
{
    public class HeatmapRenderer : MonoBehaviour
    {
        #region singleton
        public static HeatmapRenderer Instance{get; private set;}
        void Awake()
        {
            if(Instance != null)
            {
                Destroy(this.gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        #endregion
        
        [Header("Resoureces")]
        [SerializeField] private Material _stampMaterial;
        [SerializeField] private Material _colorizeMaterial;
        [SerializeField] private RawImage _previewImage;
        public void VisibleHeatmap(bool isVisible) => _previewImage.enabled = isVisible;

        [Header("Settings")]
        [SerializeField] private int _textureSize = 512;
        [SerializeField] private float _radius = 0.05f;
        [SerializeField] private float _intensity = 0.02f;
        [SerializeField] private string _saveDir = "/YOUR_RECORD/GazeData/";

        private RenderTexture _heatRT;
        private RenderTexture _tempRT;

        private List<string[]> _dataList = new List<string[]>();
        private float _time = 0f;

        private Vector2 _prevUV;
        private bool _hasPrev = false;
        private bool _isStart = false;
        private int _screenWidth;
        private int _screenHeight;
        private string _dirName = "";

        void Start()
        {
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;
            _heatRT = CreateRT(_textureSize);
            _tempRT = CreateRT(_textureSize);

            ClearRT(_heatRT);
            ClearRT(_tempRT);

            if (_previewImage != null)
            {
                _previewImage.texture = _heatRT;
                _previewImage.material = _colorizeMaterial;
                _previewImage.color = Color.white;
            }

            if (_colorizeMaterial != null)
            {
                _colorizeMaterial.SetTexture("_MainTex", _heatRT);
            }
        }

        public void StartHeatmap(string dirName = "")
        {
            Debug.Log($"<color=orange>[HeatMap]</color> Start Recording.");
            _isStart = true;
            _dirName = dirName;
            _hasPrev = false;
        }

        public float StopHeatmap(bool writeCsv = true)
        {
            Debug.Log($"<color=orange>[HeatMap]</color> Stop Recording.");
            _isStart = false;
            var totalDistance = GetTotalGazeDistance();

            if(!writeCsv)
            {
                _dataList.Clear();
                return totalDistance;
            }

            #if UNITY_WEBGL && !UNITY_EDITOR
            _dataList.Clear();
            return totalDistance;
            #endif

            HeatmapCsvWriter.WriteCsv(System.IO.Path.GetDirectoryName(Application.dataPath) + _saveDir + (_dirName == "" ? "" : $"{_dirName}/"), totalDistance, _dataList);
            Debug.Log($"<color=orange>[HeatMap]</color> Data saved to: {System.IO.Path.GetDirectoryName(Application.dataPath) + _saveDir + (_dirName == "" ? "" : $"/{_dirName}/")}");
            _dataList.Clear();

            return totalDistance;
        }

        public float GetTotalGazeDistance()
        {
            if (_dataList.Count < 2)
            {
                return 0f;
            }

            float totalDistance = 0f;
            Dictionary<string, Vector2> previousPointBySource = new Dictionary<string, Vector2>();

            foreach (var data in _dataList)
            {
                if (data == null || data.Length < 3)
                {
                    continue;
                }

                if (!float.TryParse(data[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                    !float.TryParse(data[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                {
                    continue;
                }

                Vector2 currentPoint = new Vector2(x, y);
                string sourceId = data.Length >= 4 ? data[3] : string.Empty;
                if (previousPointBySource.TryGetValue(sourceId, out Vector2 previousPoint))
                {
                    totalDistance += Vector2.Distance(previousPoint, currentPoint);
                }

                previousPointBySource[sourceId] = currentPoint;
            }

            return totalDistance;
        }

        RenderTexture CreateRT(int size)
        {
            var rt = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32);
            rt.wrapMode = TextureWrapMode.Clamp;
            rt.filterMode = FilterMode.Bilinear;
            rt.Create();
            return rt;
        }

        void ClearRT(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = prev;
        }

        void FixedUpdate()
        {
            if(!_isStart) return;
            if (_stampMaterial == null) return;

            UpdateScreenSize();

            Vector2 uv;
            bool inside = TryGetMouseUV(out uv);

            if (!inside)
            {
                _hasPrev = false;
                return;
            }

            StampUV(uv, 0, ref _prevUV, ref _hasPrev);
        }

        private void UpdateScreenSize()
        {
            _screenWidth = Mathf.Max(1, Screen.width);
            _screenHeight = Mathf.Max(1, Screen.height);
        }

        bool TryGetMouseUV(out Vector2 uv)
        {
            uv = Vector2.zero;

            Vector3 mouse = Input.mousePosition;

            if (mouse.x < 0 || mouse.x > _screenWidth || mouse.y < 0 || mouse.y > _screenHeight)
                return false;

            uv = new Vector2(mouse.x / _screenWidth, mouse.y / _screenHeight);
            return true;
        }

        private void StampUV(Vector2 uv, int sourceId, ref Vector2 prevUV, ref bool hasPrev)
        {
            Graphics.Blit(_heatRT, _tempRT);

            _stampMaterial.SetTexture("_MainTex", _tempRT);
            _stampMaterial.SetFloat("_Radius", _radius);
            _stampMaterial.SetFloat("_Intensity", _intensity);
            _stampMaterial.SetFloat("_Aspect", (float)_screenWidth / _screenHeight);

            if (hasPrev)
            {
                LineInterpolation(prevUV, uv);
            }
            else
            {
                _stampMaterial.SetVector("_MouseUV", new Vector4(uv.x, uv.y, 0, 0));
                Graphics.Blit(_tempRT, _heatRT, _stampMaterial);
            }

            prevUV = uv;
            hasPrev = true;

            if (_colorizeMaterial != null)
            {
                _colorizeMaterial.SetTexture("_MainTex", _heatRT);
            }

            _dataList.Add(new string[] { Time.time.ToString("F2"), (_screenWidth * uv.x).ToString("F0"), (_screenHeight * uv.y).ToString("F0"), sourceId.ToString() });
        }

        private void  LineInterpolation(Vector2 prevUV, Vector2 uv)
        {
            float aspect = (float)_screenWidth / _screenHeight;
            float dist = Vector2.Distance(new Vector2(prevUV.x * aspect, prevUV.y), new Vector2(uv.x * aspect, uv.y));

            // どれくらい細かく補間するか（重要）
            int steps = Mathf.CeilToInt(dist / (_radius * 0.5f));

            steps = Mathf.Clamp(steps, 1, 64); // 上限つけて暴走防止

            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector2 lerpUV = Vector2.Lerp(prevUV, uv, t);

                _stampMaterial.SetVector("_MouseUV", new Vector4(lerpUV.x, lerpUV.y, 0, 0));
                Graphics.Blit(_tempRT, _heatRT, _stampMaterial);

                // 次のスタンプのために更新
                Graphics.Blit(_heatRT, _tempRT);
            }
        }

        public void ClearHeatmap()
        {
            ClearRT(_heatRT);
            ClearRT(_tempRT);
        }

        void OnDestroy()
        {
            if (_heatRT != null) _heatRT.Release();
            if (_tempRT != null) _tempRT.Release();
        }

        void OnApplicationQuit()
        {
            if(_isStart)
            {
                StopHeatmap();
            }
        }
    }
}
