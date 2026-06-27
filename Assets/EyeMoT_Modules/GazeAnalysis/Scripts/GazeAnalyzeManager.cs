using System;
using System.Collections.Generic;
using UnityEngine;

public class GazeAnalyseManager : MonoBehaviour
{
    private static GazeAnalyseManager _instance;

    public static GazeAnalyseManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<GazeAnalyseManager>();

                if (_instance == null)
                {
                    GameObject obj = new GameObject("GazeAnalyseManager");
                    _instance = obj.AddComponent<GazeAnalyseManager>();
                }
            }

            return _instance;
        }
    }

    public static bool HasInstance
    {
        get { return _instance != null; }
    }

    public static bool IsAnalyzing
    {
        get { return _instance != null && _instance._isAnalyzing; }
    }

    [Header("Sampling")]
    public GazeSamplingLoop samplingLoop = GazeSamplingLoop.FixedUpdate;

    [Tooltip("1秒あたりの分析回数")]
    public float sampleHz = 30f;

    [Tooltip("Time.timeScaleの影響を受けない時間を使う")]
    public bool useUnscaledTime = true;

    [Header("Score Settings")]
    [Tooltip("正確性評価で、対象半径の何倍まで許容するか")]
    public float accuracyToleranceMultiplier = 1.5f;

    [Tooltip("安定性評価で、対象半径の何倍まで散らばりを許容するか")]
    public float stabilityToleranceMultiplier = 1.0f;

    [Header("Score Weight")]
    [Range(0f, 1f)] public float accuracyWeight = 0.4f;
    [Range(0f, 1f)] public float stabilityWeight = 0.3f;
    [Range(0f, 1f)] public float attentionWeight = 0.3f;

    private bool _isAnalyzing;

    private string _sessionId;
    private string _memo;

    private string _startedAt;
    private string _endedAt;

    private float _sessionStartTime;
    private float _nextSampleTime;

    private Camera _targetCamera;
    private Func<Vector2> _gazeProvider;

    private readonly Dictionary<string, ActiveGazeTarget> _activeTargets =
        new Dictionary<string, ActiveGazeTarget>();

    private readonly List<GazeTargetResult> _completedResults =
        new List<GazeTargetResult>();

    private GazeSessionResult _latestResult;

    private float Now
    {
        get { return useUnscaledTime ? Time.unscaledTime : Time.time; }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (samplingLoop == GazeSamplingLoop.Update)
        {
            TrySample();
        }
    }

    private void FixedUpdate()
    {
        if (samplingLoop == GazeSamplingLoop.FixedUpdate)
        {
            TrySample();
        }
    }

    // ============================================================
    // Static API
    // ============================================================

    public static void AnalyzeStart(
        string sessionId = "",
        Camera targetCamera = null,
        Func<Vector2> gazeProvider = null,
        float sampleHz = 30f,
        GazeSamplingLoop samplingLoop = GazeSamplingLoop.FixedUpdate,
        bool useUnscaledTime = true
    )
    {
        Instance.StartAnalysis(
            sessionId,
            targetCamera,
            gazeProvider,
            sampleHz,
            samplingLoop,
            useUnscaledTime
        );
    }

    public static string Add(
        GameObject targetObject,
        string targetId = "",
        string label = "",
        string group = "",
        GazeAnalysisStartMode startMode = GazeAnalysisStartMode.OnFirstEnter,
        float radiusPx = -1f,
        GazeTargetShape targetShape = GazeTargetShape.Auto,
        bool keepRawSamples = false
    )
    {
        return Instance.AddTarget(
            targetObject,
            targetId,
            label,
            group,
            startMode,
            radiusPx,
            targetShape,
            keepRawSamples
        );
    }

    public static GazeTargetResult Remove(string targetId)
    {
        if (_instance == null)
        {
            return null;
        }

        return _instance.CompleteTarget(targetId, GazeTargetEndReason.Removed);
    }

    public static GazeSessionResult AnalyzeEnd(string memo = "")
    {
        if (_instance == null)
        {
            return null;
        }

        return _instance.EndAnalysis(memo);
    }

    public static GazeSessionResult GetSnapshot()
    {
        if (_instance == null)
        {
            return null;
        }

        return _instance.CreateSnapshot();
    }

    public static void SetGazeProvider(Func<Vector2> gazeProvider)
    {
        Instance._gazeProvider = gazeProvider;
    }

    // ============================================================
    // Analysis Core
    // ============================================================

    private void StartAnalysis(
        string sessionId,
        Camera targetCamera,
        Func<Vector2> gazeProvider,
        float sampleHz,
        GazeSamplingLoop samplingLoop,
        bool useUnscaledTime
    )
    {
        this.useUnscaledTime = useUnscaledTime;
        this.sampleHz = Mathf.Max(1f, sampleHz);
        this.samplingLoop = samplingLoop;

        _sessionId = string.IsNullOrEmpty(sessionId)
            ? DateTime.Now.ToString("yyyyMMdd_HHmmss")
            : sessionId;

        _targetCamera = targetCamera != null ? targetCamera : Camera.main;

        // デフォルトはマウス座標。
        // 実際の視線入力にする場合は gazeProvider を差し替える。
        _gazeProvider = gazeProvider ?? (() => Input.mousePosition);

        _sessionStartTime = Now;
        _nextSampleTime = _sessionStartTime;

        _startedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _endedAt = "";

        _memo = "";

        _activeTargets.Clear();
        _completedResults.Clear();

        _latestResult = null;
        _isAnalyzing = true;
    }

    private string AddTarget(
        GameObject targetObject,
        string targetId,
        string label,
        string group,
        GazeAnalysisStartMode startMode,
        float radiusPx,
        GazeTargetShape targetShape,
        bool keepRawSamples
    )
    {
        if (!_isAnalyzing)
        {
            Debug.LogWarning("GazeAnalyseManager: AnalyzeStart() が呼ばれていません。");
            return null;
        }

        if (targetObject == null)
        {
            Debug.LogWarning("GazeAnalyseManager: targetObject が null です。");
            return null;
        }

        if (string.IsNullOrEmpty(targetId))
        {
            targetId = targetObject.name + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        if (_activeTargets.ContainsKey(targetId))
        {
            targetId = targetId + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        if (string.IsNullOrEmpty(label))
        {
            label = targetObject.name;
        }

        ActiveGazeTarget target = new ActiveGazeTarget(
            targetId,
            label,
            group,
            targetObject,
            Now,
            startMode,
            radiusPx,
            targetShape,
            keepRawSamples
        );

        _activeTargets.Add(targetId, target);

        return targetId;
    }

    private GazeTargetResult CompleteTarget(
        string targetId,
        GazeTargetEndReason endReason
    )
    {
        if (string.IsNullOrEmpty(targetId))
        {
            return null;
        }

        ActiveGazeTarget target;

        if (!_activeTargets.TryGetValue(targetId, out target))
        {
            return null;
        }

        GazeTargetResult result = target.BuildResult(
            Now,
            _sessionStartTime,
            _targetCamera,
            this,
            endReason
        );

        _activeTargets.Remove(targetId);
        _completedResults.Add(result);

        return result;
    }

    private GazeSessionResult EndAnalysis(string memo)
    {
        if (!_isAnalyzing)
        {
            return _latestResult;
        }

        _memo = memo;
        _endedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        List<string> activeIds = new List<string>(_activeTargets.Keys);

        foreach (string id in activeIds)
        {
            CompleteTarget(id, GazeTargetEndReason.AnalyzeEnd);
        }

        _isAnalyzing = false;

        _latestResult = new GazeSessionResult
        {
            sessionId = _sessionId,
            memo = _memo,
            startedAt = _startedAt,
            endedAt = _endedAt,
            durationSeconds = Mathf.Max(0f, Now - _sessionStartTime),
            targetResults = new List<GazeTargetResult>(_completedResults)
        };

        _latestResult.RecalculateSummary();

        return _latestResult;
    }

    private GazeSessionResult CreateSnapshot()
    {
        GazeSessionResult snapshot = new GazeSessionResult
        {
            sessionId = _sessionId,
            memo = _memo,
            startedAt = _startedAt,
            endedAt = "",
            durationSeconds = _isAnalyzing ? Mathf.Max(0f, Now - _sessionStartTime) : 0f,
            targetResults = new List<GazeTargetResult>(_completedResults)
        };

        foreach (ActiveGazeTarget target in _activeTargets.Values)
        {
            snapshot.targetResults.Add(
                target.BuildResult(
                    Now,
                    _sessionStartTime,
                    _targetCamera,
                    this,
                    GazeTargetEndReason.Snapshot
                )
            );
        }

        snapshot.RecalculateSummary();

        return snapshot;
    }

    private void TrySample()
    {
        if (!_isAnalyzing)
        {
            return;
        }

        float now = Now;
        float interval = 1f / Mathf.Max(1f, sampleHz);

        if (now < _nextSampleTime)
        {
            return;
        }

        _nextSampleTime = now + interval;

        SampleNow(now);
    }

    private void SampleNow(float now)
    {
        Vector2 gazePosition = _gazeProvider != null
            ? _gazeProvider.Invoke()
            : (Vector2)Input.mousePosition;

        List<string> destroyedTargetIds = null;

        foreach (KeyValuePair<string, ActiveGazeTarget> pair in _activeTargets)
        {
            ActiveGazeTarget target = pair.Value;

            if (!target.IsAlive)
            {
                if (destroyedTargetIds == null)
                {
                    destroyedTargetIds = new List<string>();
                }

                destroyedTargetIds.Add(pair.Key);
                continue;
            }

            target.Sample(gazePosition, now, _targetCamera);
        }

        if (destroyedTargetIds != null)
        {
            foreach (string id in destroyedTargetIds)
            {
                CompleteTarget(id, GazeTargetEndReason.Destroyed);
            }
        }
    }

    // ============================================================
    // Active Target
    // ============================================================

    private class ActiveGazeTarget
    {
        private readonly string _targetId;
        private readonly string _label;
        private readonly string _group;

        private readonly GameObject _targetObject;
        private readonly RectTransform _rectTransform;

        private readonly Collider[] _colliders;
        private readonly Collider2D[] _colliders2D;
        private readonly Renderer[] _renderers;

        private readonly float _appearTime;
        private readonly GazeAnalysisStartMode _startMode;
        private readonly float _manualRadiusPx;
        private readonly GazeTargetShape _targetShape;
        private readonly bool _keepRawSamples;

        private readonly List<GazeSampleRecord> _rawSamples;

        private int _sampleCountFromAppear;
        private int _insideSampleCountFromAppear;

        private float _insideTimeFromAppear;
        private bool _lastInsideFromAppear;
        private float _lastSampleTime = -1f;

        private bool _wasLooked;
        private float _firstLookTime = -1f;

        private bool _evaluationStarted;
        private float _evaluationStartTime = -1f;
        private float _lastEvaluationSampleTime = -1f;
        private bool _lastEvaluationInside;

        private int _evaluationSampleCount;
        private int _evaluationInsideSampleCount;

        private float _evaluationInsideTime;
        private float _evaluationCurrentInsideDuration;
        private float _evaluationMaxContinuousInsideTime;

        private double _meanErrorX;
        private double _meanErrorY;
        private double _m2ErrorX;
        private double _m2ErrorY;

        private double _distanceFromCenterSum;
        private double _radiusSum;

        private Vector2 _latestTargetCenter;
        private float _latestTargetRadiusPx = 50f;

        public bool IsAlive
        {
            get { return _targetObject != null; }
        }

        public ActiveGazeTarget(
            string targetId,
            string label,
            string group,
            GameObject targetObject,
            float appearTime,
            GazeAnalysisStartMode startMode,
            float manualRadiusPx,
            GazeTargetShape targetShape,
            bool keepRawSamples
        )
        {
            _targetId = targetId;
            _label = label;
            _group = group;

            _targetObject = targetObject;
            _appearTime = appearTime;
            _startMode = startMode;
            _manualRadiusPx = manualRadiusPx;
            _targetShape = targetShape;
            _keepRawSamples = keepRawSamples;

            _rectTransform = targetObject.GetComponent<RectTransform>();

            _colliders = targetObject.GetComponentsInChildren<Collider>(true);
            _colliders2D = targetObject.GetComponentsInChildren<Collider2D>(true);
            _renderers = targetObject.GetComponentsInChildren<Renderer>(true);

            if (_keepRawSamples)
            {
                _rawSamples = new List<GazeSampleRecord>();
            }

            if (_startMode == GazeAnalysisStartMode.OnAdd)
            {
                StartEvaluation(_appearTime);
            }
        }

        public void Sample(Vector2 gazePosition, float now, Camera camera)
        {
            float deltaTime = _lastSampleTime < 0f
                ? 0f
                : Mathf.Max(0f, now - _lastSampleTime);

            _lastSampleTime = now;

            TargetMetrics metrics = GetTargetMetrics(camera);

            _latestTargetCenter = metrics.center;
            _latestTargetRadiusPx = metrics.radiusPx;

            bool isInside = IsInsideTarget(gazePosition, metrics, camera);
            float distanceFromCenter = Vector2.Distance(gazePosition, metrics.center);

            // 出現時点からの注視時間を計算。
            // 前回サンプルの状態を使うことで、外→内に入った瞬間の移動時間を過剰に加算しにくくする。
            if (_lastInsideFromAppear)
            {
                _insideTimeFromAppear += deltaTime;
            }

            _sampleCountFromAppear++;

            if (isInside)
            {
                _insideSampleCountFromAppear++;
            }

            if (!_wasLooked && isInside)
            {
                _wasLooked = true;
                _firstLookTime = Mathf.Max(0f, now - _appearTime);

                if (_startMode == GazeAnalysisStartMode.OnFirstEnter)
                {
                    StartEvaluation(now);
                }
            }

            if (_evaluationStarted)
            {
                AddEvaluationSample(gazePosition, metrics);

                float evaluationDelta = _lastEvaluationSampleTime < 0f
                    ? 0f
                    : Mathf.Max(0f, now - _lastEvaluationSampleTime);

                if (_lastEvaluationInside)
                {
                    _evaluationInsideTime += evaluationDelta;
                    _evaluationCurrentInsideDuration += evaluationDelta;

                    if (_evaluationCurrentInsideDuration > _evaluationMaxContinuousInsideTime)
                    {
                        _evaluationMaxContinuousInsideTime = _evaluationCurrentInsideDuration;
                    }
                }
                else
                {
                    _evaluationCurrentInsideDuration = 0f;
                }

                if (isInside)
                {
                    _evaluationInsideSampleCount++;
                }

                _lastEvaluationInside = isInside;
                _lastEvaluationSampleTime = now;
            }

            if (_keepRawSamples)
            {
                _rawSamples.Add(new GazeSampleRecord
                {
                    timeFromTargetAppear = Mathf.Max(0f, now - _appearTime),
                    timeFromEvaluationStart = _evaluationStarted
                        ? Mathf.Max(0f, now - _evaluationStartTime)
                        : -1f,

                    gazeScreenPosition = gazePosition,
                    targetCenterScreenPosition = metrics.center,

                    targetRadiusPx = metrics.radiusPx,
                    distanceFromCenterPx = distanceFromCenter,

                    isInside = isInside,
                    isEvaluated = _evaluationStarted
                });
            }

            _lastInsideFromAppear = isInside;
        }

        private void StartEvaluation(float now)
        {
            if (_evaluationStarted)
            {
                return;
            }

            _evaluationStarted = true;
            _evaluationStartTime = now;
            _lastEvaluationSampleTime = -1f;
            _lastEvaluationInside = false;
            _evaluationCurrentInsideDuration = 0f;
        }

        private void AddEvaluationSample(Vector2 gazePosition, TargetMetrics metrics)
        {
            Vector2 error = gazePosition - metrics.center;
            float distance = error.magnitude;

            _evaluationSampleCount++;
            _distanceFromCenterSum += distance;
            _radiusSum += metrics.radiusPx;

            double dx = error.x - _meanErrorX;
            _meanErrorX += dx / _evaluationSampleCount;
            _m2ErrorX += dx * (error.x - _meanErrorX);

            double dy = error.y - _meanErrorY;
            _meanErrorY += dy / _evaluationSampleCount;
            _m2ErrorY += dy * (error.y - _meanErrorY);
        }

        public GazeTargetResult BuildResult(
            float endTime,
            float sessionStartTime,
            Camera camera,
            GazeAnalyseManager settings,
            GazeTargetEndReason endReason
        )
        {
            float lifeTime = Mathf.Max(0f, endTime - _appearTime);

            float finalInsideTimeFromAppear = _insideTimeFromAppear;

            if (_lastInsideFromAppear && _lastSampleTime >= 0f)
            {
                finalInsideTimeFromAppear += Mathf.Max(0f, endTime - _lastSampleTime);
            }

            float evaluationDuration = _evaluationStarted
                ? Mathf.Max(0f, endTime - _evaluationStartTime)
                : 0f;

            float finalEvaluationInsideTime = _evaluationInsideTime;
            float finalMaxContinuousInsideTime = _evaluationMaxContinuousInsideTime;

            if (_evaluationStarted && _lastEvaluationInside && _lastEvaluationSampleTime >= 0f)
            {
                float trailingTime = Mathf.Max(0f, endTime - _lastEvaluationSampleTime);
                finalEvaluationInsideTime += trailingTime;

                float currentDuration = _evaluationCurrentInsideDuration + trailingTime;

                if (currentDuration > finalMaxContinuousInsideTime)
                {
                    finalMaxContinuousInsideTime = currentDuration;
                }
            }

            TargetMetrics latestMetrics = IsAlive
                ? GetTargetMetrics(camera)
                : new TargetMetrics
                {
                    center = _latestTargetCenter,
                    radiusPx = _latestTargetRadiusPx,
                    screenRect = new Rect(
                        _latestTargetCenter.x - _latestTargetRadiusPx,
                        _latestTargetCenter.y - _latestTargetRadiusPx,
                        _latestTargetRadiusPx * 2f,
                        _latestTargetRadiusPx * 2f
                    ),
                    hasScreenRect = true
                };

            float averageTargetRadiusPx = _evaluationSampleCount > 0
                ? Mathf.Max(1f, (float)(_radiusSum / _evaluationSampleCount))
                : Mathf.Max(1f, latestMetrics.radiusPx);

            Vector2 meanRelativeError = _evaluationSampleCount > 0
                ? new Vector2((float)_meanErrorX, (float)_meanErrorY)
                : Vector2.zero;

            float averageDistanceFromCenter = _evaluationSampleCount > 0
                ? (float)(_distanceFromCenterSum / _evaluationSampleCount)
                : 0f;

            float relativeStdDev = CalculateRelativeErrorStdDev();

            float normalizedAverageDistance = averageTargetRadiusPx > 0f
                ? averageDistanceFromCenter / averageTargetRadiusPx
                : 0f;

            float accuracyScore = 0f;
            float stabilityScore = 0f;
            float attentionScore = 0f;

            if (_evaluationSampleCount > 0 && averageTargetRadiusPx > 0f)
            {
                float accuracyDenominator =
                    averageTargetRadiusPx * Mathf.Max(0.01f, settings.accuracyToleranceMultiplier);

                float stabilityDenominator =
                    averageTargetRadiusPx * Mathf.Max(0.01f, settings.stabilityToleranceMultiplier);

                accuracyScore = 1f - Mathf.Clamp01(averageDistanceFromCenter / accuracyDenominator);
                stabilityScore = 1f - Mathf.Clamp01(relativeStdDev / stabilityDenominator);
            }

            if (evaluationDuration > 0f)
            {
                attentionScore = Mathf.Clamp01(finalEvaluationInsideTime / evaluationDuration);
            }

            float weightSum =
                settings.accuracyWeight +
                settings.stabilityWeight +
                settings.attentionWeight;

            if (weightSum <= 0f)
            {
                weightSum = 1f;
            }

            float totalScore =
                accuracyScore * settings.accuracyWeight +
                stabilityScore * settings.stabilityWeight +
                attentionScore * settings.attentionWeight;

            totalScore /= weightSum;

            return new GazeTargetResult
            {
                targetId = _targetId,
                label = _label,
                group = _group,
                endReason = endReason,

                targetAppearTimeFromSession = Mathf.Max(0f, _appearTime - sessionStartTime),
                lifeTime = lifeTime,

                firstLookTime = _firstLookTime,
                wasLooked = _wasLooked,
                evaluationStarted = _evaluationStarted,

                evaluationStartTimeFromAppear = _evaluationStarted
                    ? Mathf.Max(0f, _evaluationStartTime - _appearTime)
                    : -1f,

                evaluationDuration = evaluationDuration,

                sampleCountFromAppear = _sampleCountFromAppear,
                insideSampleCountFromAppear = _insideSampleCountFromAppear,

                insideTimeFromAppear = finalInsideTimeFromAppear,
                insideRateFromAppear = lifeTime > 0f
                    ? Mathf.Clamp01(finalInsideTimeFromAppear / lifeTime)
                    : 0f,

                evaluationSampleCount = _evaluationSampleCount,
                evaluationInsideSampleCount = _evaluationInsideSampleCount,

                evaluationInsideTime = finalEvaluationInsideTime,
                evaluationInsideRate = evaluationDuration > 0f
                    ? Mathf.Clamp01(finalEvaluationInsideTime / evaluationDuration)
                    : 0f,

                maxContinuousInsideTime = finalMaxContinuousInsideTime,

                latestTargetCenterScreenPosition = latestMetrics.center,
                latestTargetRadiusPx = latestMetrics.radiusPx,
                averageTargetRadiusPx = averageTargetRadiusPx,

                meanRelativeErrorPx = meanRelativeError,
                averageDistanceFromCenterPx = averageDistanceFromCenter,
                relativeErrorStdDevPx = relativeStdDev,
                normalizedAverageDistance = normalizedAverageDistance,

                accuracyScore = accuracyScore,
                stabilityScore = stabilityScore,
                attentionScore = attentionScore,
                totalScore = totalScore,

                rawSamples = _keepRawSamples
                    ? new List<GazeSampleRecord>(_rawSamples)
                    : null
            };
        }

        private float CalculateRelativeErrorStdDev()
        {
            if (_evaluationSampleCount <= 1)
            {
                return 0f;
            }

            double varianceX = _m2ErrorX / (_evaluationSampleCount - 1);
            double varianceY = _m2ErrorY / (_evaluationSampleCount - 1);

            return Mathf.Sqrt((float)(varianceX + varianceY));
        }

        // ========================================================
        // Target Area
        // ========================================================

        private struct TargetMetrics
        {
            public Vector2 center;
            public float radiusPx;

            public Rect screenRect;
            public bool hasScreenRect;
        }

        private TargetMetrics GetTargetMetrics(Camera camera)
        {
            TargetMetrics metrics = new TargetMetrics
            {
                center = _latestTargetCenter,
                radiusPx = _latestTargetRadiusPx,
                screenRect = new Rect(
                    _latestTargetCenter.x - _latestTargetRadiusPx,
                    _latestTargetCenter.y - _latestTargetRadiusPx,
                    _latestTargetRadiusPx * 2f,
                    _latestTargetRadiusPx * 2f
                ),
                hasScreenRect = true
            };

            if (_targetObject == null)
            {
                return metrics;
            }

            if (_rectTransform != null)
            {
                Rect rect = GetRectTransformScreenRect(camera);
                Vector2 center = rect.center;

                metrics.center = center;
                metrics.screenRect = rect;
                metrics.hasScreenRect = true;
                metrics.radiusPx = _manualRadiusPx > 0f
                    ? _manualRadiusPx
                    : Mathf.Max(rect.width, rect.height) * 0.5f;

                return metrics;
            }

            Bounds bounds;

            if (TryGetWorldBounds(out bounds))
            {
                Rect rect;

                if (TryGetWorldBoundsScreenRect(bounds, camera, out rect))
                {
                    Vector2 center = GetWorldPointScreenPosition(bounds.center, camera);

                    if (center == Vector2.zero)
                    {
                        center = rect.center;
                    }

                    metrics.center = center;
                    metrics.screenRect = rect;
                    metrics.hasScreenRect = true;
                    metrics.radiusPx = _manualRadiusPx > 0f
                        ? _manualRadiusPx
                        : Mathf.Max(rect.width, rect.height) * 0.5f;

                    metrics.radiusPx = Mathf.Max(1f, metrics.radiusPx);
                    return metrics;
                }
            }

            Vector2 fallbackCenter = GetWorldPointScreenPosition(_targetObject.transform.position, camera);

            metrics.center = fallbackCenter;
            metrics.radiusPx = _manualRadiusPx > 0f ? _manualRadiusPx : 50f;
            metrics.screenRect = new Rect(
                fallbackCenter.x - metrics.radiusPx,
                fallbackCenter.y - metrics.radiusPx,
                metrics.radiusPx * 2f,
                metrics.radiusPx * 2f
            );
            metrics.hasScreenRect = true;

            return metrics;
        }

        private bool IsInsideTarget(
            Vector2 gazePosition,
            TargetMetrics metrics,
            Camera camera
        )
        {
            GazeTargetShape shape = _targetShape;

            if (shape == GazeTargetShape.Auto)
            {
                shape = _rectTransform != null
                    ? GazeTargetShape.Rect
                    : GazeTargetShape.Circle;
            }

            if (shape == GazeTargetShape.Rect)
            {
                if (_rectTransform != null)
                {
                    Camera uiCamera = GetUICamera(camera);

                    return RectTransformUtility.RectangleContainsScreenPoint(
                        _rectTransform,
                        gazePosition,
                        uiCamera
                    );
                }

                return metrics.hasScreenRect && metrics.screenRect.Contains(gazePosition);
            }

            return Vector2.Distance(gazePosition, metrics.center) <= metrics.radiusPx;
        }

        private Camera GetUICamera(Camera fallback)
        {
            if (_rectTransform == null)
            {
                return fallback;
            }

            Canvas canvas = _rectTransform.GetComponentInParent<Canvas>();

            if (canvas == null)
            {
                return fallback;
            }

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            if (canvas.worldCamera != null)
            {
                return canvas.worldCamera;
            }

            return fallback;
        }

        private Rect GetRectTransformScreenRect(Camera camera)
        {
            Camera uiCamera = GetUICamera(camera);

            Vector3[] corners = new Vector3[4];
            _rectTransform.GetWorldCorners(corners);

            Vector2 min = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
            Vector2 max = min;

            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 p = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[i]);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private bool TryGetWorldBounds(out Bounds bounds)
        {
            bool hasBounds = false;
            bounds = new Bounds();

            // Colliderを最優先。
            // 視線評価の対象範囲は、見た目より当たり判定範囲に合わせたいことが多いため。
            if (_colliders != null)
            {
                foreach (Collider col in _colliders)
                {
                    if (col == null || !col.enabled)
                    {
                        continue;
                    }

                    if (!hasBounds)
                    {
                        bounds = col.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(col.bounds);
                    }
                }

                if (hasBounds)
                {
                    return true;
                }
            }

            if (_colliders2D != null)
            {
                foreach (Collider2D col in _colliders2D)
                {
                    if (col == null || !col.enabled)
                    {
                        continue;
                    }

                    if (!hasBounds)
                    {
                        bounds = col.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(col.bounds);
                    }
                }

                if (hasBounds)
                {
                    return true;
                }
            }

            if (_renderers != null)
            {
                foreach (Renderer renderer in _renderers)
                {
                    if (renderer == null || !renderer.enabled)
                    {
                        continue;
                    }

                    if (!hasBounds)
                    {
                        bounds = renderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }
            }

            return hasBounds;
        }

        private bool TryGetWorldBoundsScreenRect(
            Bounds bounds,
            Camera camera,
            out Rect rect
        )
        {
            rect = new Rect();

            Camera cam = camera != null ? camera : Camera.main;

            if (cam == null)
            {
                return false;
            }

            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;

            Vector3[] corners =
            {
                center + new Vector3( extents.x,  extents.y,  extents.z),
                center + new Vector3( extents.x,  extents.y, -extents.z),
                center + new Vector3( extents.x, -extents.y,  extents.z),
                center + new Vector3( extents.x, -extents.y, -extents.z),
                center + new Vector3(-extents.x,  extents.y,  extents.z),
                center + new Vector3(-extents.x,  extents.y, -extents.z),
                center + new Vector3(-extents.x, -extents.y,  extents.z),
                center + new Vector3(-extents.x, -extents.y, -extents.z)
            };

            bool hasPoint = false;

            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 screen = cam.WorldToScreenPoint(corners[i]);

                if (screen.z < 0f)
                {
                    continue;
                }

                Vector2 p = new Vector2(screen.x, screen.y);

                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);

                hasPoint = true;
            }

            if (!hasPoint)
            {
                return false;
            }

            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        private Vector2 GetWorldPointScreenPosition(Vector3 worldPosition, Camera camera)
        {
            Camera cam = camera != null ? camera : Camera.main;

            if (cam == null)
            {
                return Vector2.zero;
            }

            Vector3 screen = cam.WorldToScreenPoint(worldPosition);

            if (screen.z < 0f)
            {
                return Vector2.zero;
            }

            return new Vector2(screen.x, screen.y);
        }
    }
}