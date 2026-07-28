using System.Collections.Generic;
using System.Globalization;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Concurrent;

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
        [SerializeField] private Material _decayMaterial;
        [SerializeField] private Material _colorizeMaterial;
        [SerializeField] private Material _gazeLineMaterial;
        [SerializeField] private RawImage _previewImage;
        [SerializeField] private RawImage _gazeLineImage;
        public void VisibleHeatmap(bool isVisible, RenderTexture heatmapTexture = null)
        {
            if(isVisible)
                _previewImage.texture = heatmapTexture != null ? heatmapTexture : _heatRT;
            _previewImage.enabled = isVisible;
            if (_gazeLineImage != null)
                _gazeLineImage.enabled = isVisible;
        }

        [Header("Settings")]
        [SerializeField] private int _textureSize = 512;
        [SerializeField] private float _radius = 0.05f;
        public void SetHeatmapRadius(float radius) => _radius = Mathf.Clamp(radius, 0.001f, 1f);

        [SerializeField] private float _intensity = 0.02f;
        public void SetHeatmapIntensity(float intensity) => _intensity = Mathf.Clamp(intensity, 0.001f, 1f);
        [SerializeField, Min(0f)] private float _decayPerSecond = 0.05f;
        public void SetHeatmapDecayPerSecond(float decayPerSecond) => _decayPerSecond = Mathf.Max(0f, decayPerSecond);
        [SerializeField, Range(0f, 3f)] private float _softness = 1.5f;
        public void SetHeatmapSoftness(float softness) => _softness = Mathf.Clamp(softness, 0f, 3f);
        [SerializeField, Range(0f, 1f)] private float _opacity = 1f;
        public void SetHeatmapOpacity(float opacity)
        {
            _opacity = Mathf.Clamp01(opacity);
            ApplyHeatmapOpacity();
        }
        [SerializeField] public bool _isDecay = false;
        public void SetHeatmapDecay(bool isDecay) => _isDecay = isDecay;
        [SerializeField, Min(0.0001f)] private float _gazeLineWidth = 0.003f;
        [SerializeField] private string _saveDir = "/YOUR_RECORD/GazeData/";

        private RenderTexture _heatRT;
        private RenderTexture _tempRT;
        private RenderTexture _gazeLineRT;
        private RenderTexture _gazeLineTempRT;
        private float _pendingDecay;

        private List<string[]> _dataList = new List<string[]>();
        private float _startTime = 0f;

        private Vector2 _prevUV;
        private bool _hasPrev = false;
        private Vector2 _previousGazeLineUV;
        private bool _hasPreviousGazeLineUV;
        private bool _isStart = false;
        private bool _isDynamicDraw = false;
        private int _screenWidth;
        private int _screenHeight;
        private string _dirName = "";

        //テスト用
        ConcurrentQueue<Dictionary<FocusCalmManager.EData, string>> _receivedData = new();
        public void Enqueue(Dictionary<FocusCalmManager.EData, string> data)
        {
            if (data == null) return;
            if(!_isStart) return;
            _receivedData.Enqueue(data);
        }

        void Start()
        {
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;
            _heatRT = CreateRT(_textureSize);
            _tempRT = CreateRT(_textureSize);
            _gazeLineRT = CreateColorRT(_textureSize);
            _gazeLineTempRT = CreateColorRT(_textureSize);

            ClearHeatmap();

            if (_previewImage != null)
            {
                _previewImage.texture = _heatRT;
                _previewImage.material = _colorizeMaterial;
                _previewImage.color = Color.white;
            }

            if (_colorizeMaterial != null)
            {
                ApplyHeatmapOpacity();
            }

            if (_gazeLineImage != null)
            {
                _gazeLineImage.texture = _gazeLineRT;
                _gazeLineImage.material = null;
                _gazeLineImage.color = Color.white;
            }
        }

        private void ApplyHeatmapOpacity()
        {
            if (_colorizeMaterial != null)
            {
                _colorizeMaterial.SetFloat("_Opacity", _opacity);
            }
        }


        private void OnValidate()
        {
            _opacity = Mathf.Clamp01(_opacity);

            if (Application.isPlaying)
            {
                ApplyHeatmapOpacity();
            }
        }

        public void StartHeatmap(string dirName = "", bool isDynamicDraw = false)
        {
            Debug.Log($"<color=orange>[HeatMap]</color> Start Recording.");
            _isStart = true;
            _startTime = Time.time;
            _dirName = dirName;
            _hasPrev = false;
            _hasPreviousGazeLineUV = false;
            _isDynamicDraw = isDynamicDraw;
            _previewImage.texture = _heatRT;
        }

        public void StopHeatmapRecording()
        {
            _isStart = false;
        }

        public HeatmapResult StopHeatmap(bool writeCsv = true, Action<HeatmapResult> onComplete = null)
        {
            Debug.Log($"<color=orange>[HeatMap]</color> Stop Recording.");
            _isStart = false;
            var totalDistance = GetTotalGazeDistance();

            var result = new HeatmapResult
            {
                TotalDistance = totalDistance,
                DataList = new List<string[]>(_dataList),
                HeatmapTexture = _heatRT,
                GazeLineTexture = _gazeLineRT
            };

            if(!_isDynamicDraw || _isDecay)
            {
                if (onComplete != null)
                {
                    CreateHeatmapFromDataListAsync(result.DataList, onComplete);
                }
            }
            else
            {
                onComplete?.Invoke(result);
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

        public Coroutine CreateHeatmapFromDataListAsync(List<string[]> dataList, Action<HeatmapResult> onComplete, int pointsPerFrame = 64)
        {
            return StartCoroutine(CreateHeatmapFromDataListRoutine(dataList, onComplete, pointsPerFrame));
        }

        private IEnumerator CreateHeatmapFromDataListRoutine(List<string[]> dataList, Action<HeatmapResult> onComplete, int pointsPerFrame)
        {
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture heatRT = CreateRT(_textureSize);
            RenderTexture tempRT = CreateRT(_textureSize);
            RenderTexture gazeLineRT =
                _gazeLineMaterial != null ? CreateColorRT(_textureSize) : null;
            RenderTexture gazeLineTempRT =
                _gazeLineMaterial != null ? CreateColorRT(_textureSize) : null;
            bool keepGazeLineRT = false;
            pointsPerFrame = Mathf.Max(1, pointsPerFrame);

            try
            {
                ClearRT(heatRT);
                ClearRT(tempRT);
                if (gazeLineRT != null) ClearRT(gazeLineRT);
                if (gazeLineTempRT != null) ClearRT(gazeLineTempRT);

                if (dataList != null &&
                    (_stampMaterial != null || _gazeLineMaterial != null))
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

                        if (_gazeLineMaterial != null)
                        {
                            DrawGazeLineSegment(
                                gazeLineRT,
                                gazeLineTempRT,
                                hasPrev ? prevUV : uv,
                                uv);
                        }

                        if (_stampMaterial != null)
                        {
                            StampUVToHeatmap(
                                heatRT,
                                tempRT,
                                uv,
                                ref prevUV,
                                ref hasPrev);
                        }
                        else
                        {
                            prevUV = uv;
                            hasPrev = true;
                        }

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

                if (gazeLineRT != null)
                {
                    ReplaceGazeLineTexture(gazeLineRT);
                    keepGazeLineRT = true;
                }
            }
            finally
            {
                RenderTexture.active = previousActive;
                tempRT.Release();
                if (gazeLineTempRT != null) gazeLineTempRT.Release();
                if (gazeLineRT != null && !keepGazeLineRT) gazeLineRT.Release();
            }

            onComplete?.Invoke(new HeatmapResult
            {
                DataList = dataList != null
                    ? new List<string[]>(dataList)
                    : new List<string[]>(),
                HeatmapTexture = heatRT,
                GazeLineTexture = gazeLineRT,
            });
        }

        RenderTexture CreateRT(int size)
        {
            RenderTextureFormat format =
                SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RHalf)
                    ? RenderTextureFormat.RHalf
                    : SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
                        ? RenderTextureFormat.ARGBHalf
                        : RenderTextureFormat.ARGB32;

            var rt = new RenderTexture(size, size, 0, format);
            rt.wrapMode = TextureWrapMode.Clamp;
            rt.filterMode = FilterMode.Bilinear;
            rt.Create();
            return rt;
        }

        RenderTexture CreateColorRT(int size)
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

            //テスト用
            string[] dataSet = new string[6]{ 
                (Time.time-_startTime).ToString("F2"), (_screenWidth * uv.x).ToString("F0"), (_screenHeight * uv.y).ToString("F0"), 
                "", "", "",
                };

            while (_receivedData.TryDequeue(out Dictionary<FocusCalmManager.EData, string> data))
            {
                foreach (KeyValuePair<FocusCalmManager.EData, string> pair in data)
                {
                    dataSet[(int)pair.Key+3] = pair.Value;
                }
            }
            _dataList.Add(dataSet);
            /////////

            //_dataList.Add(new string[] { (Time.time-_startTime).ToString("F2"), (_screenWidth * uv.x).ToString("F0"), (_screenHeight * uv.y).ToString("F0")});

            if (inside)
                DrawGazeLine(uv);
            else
                _hasPreviousGazeLineUV = false;

            if(!_isDynamicDraw) return;

            DecayHeatmap(Time.fixedDeltaTime);

            if (!inside)
            {
                _hasPrev = false;
                return;
            }

            StampUV(uv, ref _prevUV, ref _hasPrev);
        }

        private void DrawGazeLine(Vector2 currentUV)
        {
            if (_gazeLineMaterial == null ||
                _gazeLineRT == null ||
                _gazeLineTempRT == null)
            {
                return;
            }

            if (!_hasPreviousGazeLineUV)
            {
                _previousGazeLineUV = currentUV;
                _hasPreviousGazeLineUV = true;
            }

            DrawGazeLineSegment(
                _gazeLineRT,
                _gazeLineTempRT,
                _previousGazeLineUV,
                currentUV);

            _previousGazeLineUV = currentUV;
        }

        private void DrawGazeLineSegment(
            RenderTexture gazeLineRT,
            RenderTexture gazeLineTempRT,
            Vector2 startUV,
            Vector2 endUV)
        {
            if (_gazeLineMaterial == null ||
                gazeLineRT == null ||
                gazeLineTempRT == null)
            {
                return;
            }

            Graphics.Blit(gazeLineRT, gazeLineTempRT);
            _gazeLineMaterial.SetVector(
                "_StartUV",
                new Vector4(startUV.x, startUV.y, 0f, 0f));
            _gazeLineMaterial.SetVector(
                "_EndUV",
                new Vector4(endUV.x, endUV.y, 0f, 0f));
            _gazeLineMaterial.SetFloat("_LineWidth", _gazeLineWidth);
            _gazeLineMaterial.SetFloat(
                "_Aspect",
                (float)_screenWidth / _screenHeight);
            Graphics.Blit(gazeLineTempRT, gazeLineRT, _gazeLineMaterial);
        }

        private void ReplaceGazeLineTexture(RenderTexture gazeLineTexture)
        {
            if (gazeLineTexture == null || gazeLineTexture == _gazeLineRT)
                return;

            if (_gazeLineRT != null)
                _gazeLineRT.Release();

            _gazeLineRT = gazeLineTexture;
            if (_gazeLineImage != null)
                _gazeLineImage.texture = _gazeLineRT;
        }

        private void DecayHeatmap(float deltaTime)
        {
            if (!_isDecay || _decayMaterial == null || _heatRT == null || _tempRT == null) return;

            _pendingDecay += _decayPerSecond * Mathf.Max(0f, deltaTime);

            float decayAmount = _pendingDecay;
            if (_heatRT.format == RenderTextureFormat.ARGB32)
            {
                const float argb32Step = 1f / 255f;
                if (_pendingDecay < argb32Step) return;

                decayAmount = Mathf.Floor(_pendingDecay / argb32Step) * argb32Step;
            }

            if (decayAmount <= 0f) return;

            _pendingDecay -= decayAmount;
            _decayMaterial.SetFloat("_DecayAmount", decayAmount);
            Graphics.Blit(_heatRT, _tempRT, _decayMaterial);
            Graphics.Blit(_tempRT, _heatRT);
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
            //_dataList.Add(new string[] { Time.time.ToString("F2"), (_screenWidth * uv.x).ToString("F0"), (_screenHeight * uv.y).ToString("F0")});
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
            _stampMaterial.SetFloat("_Softness", _softness);
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
            _pendingDecay = 0f;
            _hasPreviousGazeLineUV = false;
            ClearRT(_heatRT);
            ClearRT(_tempRT);
            if (_gazeLineRT != null) ClearRT(_gazeLineRT);
            if (_gazeLineTempRT != null) ClearRT(_gazeLineTempRT);
        }

        void OnDestroy()
        {
            if (_heatRT != null) _heatRT.Release();
            if (_tempRT != null) _tempRT.Release();
            if (_gazeLineRT != null) _gazeLineRT.Release();
            if (_gazeLineTempRT != null) _gazeLineTempRT.Release();
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
        public RenderTexture GazeLineTexture;
    }
}
