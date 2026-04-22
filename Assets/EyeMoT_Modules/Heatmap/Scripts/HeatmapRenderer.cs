using System.Collections.Generic;
using System.Globalization;
using System;
using System.Collections;
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
        public void VisibleHeatmap(bool isVisible, RenderTexture heatmapTexture = null)
        {
            if(isVisible)
                _previewImage.texture = heatmapTexture != null ? heatmapTexture : _heatRT;
            _previewImage.enabled = isVisible;
        }

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
        private bool _isDynamicDraw = false;
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

        public void StartHeatmap(string dirName = "", bool isDynamicDraw = false)
        {
            Debug.Log($"<color=orange>[HeatMap]</color> Start Recording.");
            _isStart = true;
            _dirName = dirName;
            _hasPrev = false;
            _isDynamicDraw = isDynamicDraw;
            _previewImage.texture = _heatRT;
        }

        public HeatmapResult StopHeatmap(bool writeCsv = true, Action<RenderTexture> onComplete = null)
        {
            Debug.Log($"<color=orange>[HeatMap]</color> Stop Recording.");
            _isStart = false;
            var totalDistance = GetTotalGazeDistance();

            var result = new HeatmapResult
            {
                TotalDistance = totalDistance,
                DataList = new List<string[]>(_dataList),
                HeatmapTexture = _heatRT,
            };

            if(!_isDynamicDraw)
            {
                if (onComplete != null)
                {
                    CreateHeatmapFromDataListAsync(result.DataList, onComplete);
                }
            }
            else
            {
                onComplete?.Invoke(result.HeatmapTexture);
            }

            if(!writeCsv)
            {
                _dataList.Clear();
                return result;
            }

            #if UNITY_WEBGL && !UNITY_EDITOR
            _dataList.Clear();
            return result;
            #endif

            HeatmapCsvWriter.WriteCsv(System.IO.Path.GetDirectoryName(Application.dataPath) + _saveDir + (_dirName == "" ? "" : $"{_dirName}/"), totalDistance, new List<string[]>(_dataList));
            Debug.Log($"<color=orange>[HeatMap]</color> Data saved to: {System.IO.Path.GetDirectoryName(Application.dataPath) + _saveDir + (_dirName == "" ? "" : $"/{_dirName}/")}");
            _dataList.Clear();

            return result;
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

        public Coroutine CreateHeatmapFromDataListAsync(List<string[]> dataList, Action<RenderTexture> onComplete, int pointsPerFrame = 64)
        {
            return StartCoroutine(CreateHeatmapFromDataListRoutine(dataList, onComplete, pointsPerFrame));
        }

        private IEnumerator CreateHeatmapFromDataListRoutine(List<string[]> dataList, Action<RenderTexture> onComplete, int pointsPerFrame)
        {
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture heatRT = CreateRT(_textureSize);
            RenderTexture tempRT = CreateRT(_textureSize);
            pointsPerFrame = Mathf.Max(1, pointsPerFrame);

            try
            {
                ClearRT(heatRT);
                ClearRT(tempRT);

                if (dataList != null && _stampMaterial != null)
                {
                    UpdateScreenSize();

                    int processedThisFrame = 0;
                    Dictionary<string, Vector2> previousUVBySource = new Dictionary<string, Vector2>();
                    HashSet<string> hasPreviousBySource = new HashSet<string>();

                    foreach (string[] data in dataList)
                    {
                        if (!TryParseHeatmapData(data, out Vector2 uv, out string sourceId))
                        {
                            continue;
                        }

                        bool hasPrev = hasPreviousBySource.Contains(sourceId);
                        Vector2 prevUV = hasPrev ? previousUVBySource[sourceId] : Vector2.zero;

                        StampUVToHeatmap(heatRT, tempRT, uv, ref prevUV, ref hasPrev);

                        previousUVBySource[sourceId] = prevUV;
                        if (hasPrev)
                        {
                            hasPreviousBySource.Add(sourceId);
                        }

                        processedThisFrame++;
                        if (processedThisFrame >= pointsPerFrame)
                        {
                            processedThisFrame = 0;
                            RenderTexture.active = previousActive;
                            yield return null;
                        }
                    }
                }
            }
            finally
            {
                RenderTexture.active = previousActive;
                tempRT.Release();
            }

            onComplete?.Invoke(heatRT);
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
            _dataList.Add(new string[] { Time.time.ToString("F2"), (_screenWidth * uv.x).ToString("F0"), (_screenHeight * uv.y).ToString("F0")});
            if(!_isDynamicDraw) return;

            if (!inside)
            {
                _hasPrev = false;
                return;
            }

            StampUV(uv, ref _prevUV, ref _hasPrev);
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

        private void StampUV(Vector2 uv, ref Vector2 prevUV, ref bool hasPrev)
        {
            StampUVToHeatmap(uv, ref prevUV, ref hasPrev);
            _dataList.Add(new string[] { Time.time.ToString("F2"), (_screenWidth * uv.x).ToString("F0"), (_screenHeight * uv.y).ToString("F0")});
        }

        private void StampUVToHeatmap(Vector2 uv, ref Vector2 prevUV, ref bool hasPrev)
        {
            StampUVToHeatmap(_heatRT, _tempRT, uv, ref prevUV, ref hasPrev);
        }

        private void StampUVToHeatmap(RenderTexture heatRT, RenderTexture tempRT, Vector2 uv, ref Vector2 prevUV, ref bool hasPrev)
        {
            Graphics.Blit(heatRT, tempRT);

            _stampMaterial.SetTexture("_MainTex", tempRT);
            _stampMaterial.SetFloat("_Radius", _radius);
            _stampMaterial.SetFloat("_Intensity", _intensity);
            _stampMaterial.SetFloat("_Aspect", (float)_screenWidth / _screenHeight);

            if (hasPrev)
            {
                LineInterpolation(heatRT, tempRT, prevUV, uv);
            }
            else
            {
                _stampMaterial.SetVector("_MouseUV", new Vector4(uv.x, uv.y, 0, 0));
                Graphics.Blit(tempRT, heatRT, _stampMaterial);
            }

            prevUV = uv;
            hasPrev = true;

            if (_colorizeMaterial != null && heatRT == _heatRT)
            {
                _colorizeMaterial.SetTexture("_MainTex", heatRT);
            }
        }

        private bool TryParseHeatmapData(string[] data, out Vector2 uv, out string sourceId)
        {
            uv = Vector2.zero;
            sourceId = string.Empty;

            if (data == null || data.Length < 3)
            {
                return false;
            }

            if (!float.TryParse(data[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(data[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            {
                return false;
            }

            uv = new Vector2(
                Mathf.Clamp01(x / _screenWidth),
                Mathf.Clamp01(y / _screenHeight));
            sourceId = data.Length >= 4 ? data[3] : string.Empty;
            return true;
        }

        private void  LineInterpolation(Vector2 prevUV, Vector2 uv)
        {
            LineInterpolation(_heatRT, _tempRT, prevUV, uv);
        }

        private void  LineInterpolation(RenderTexture heatRT, RenderTexture tempRT, Vector2 prevUV, Vector2 uv)
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
                Graphics.Blit(tempRT, heatRT, _stampMaterial);

                // 次のスタンプのために更新
                Graphics.Blit(heatRT, tempRT);
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

    public class HeatmapResult
    {
        public float TotalDistance;
        public List<string[]> DataList;
        public RenderTexture HeatmapTexture;
    }
}
