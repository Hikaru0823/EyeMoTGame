using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DynamicLineGraph : MonoBehaviour
{
    [Header("Resources")]
    [SerializeField] RawImage _rawImage;
    [SerializeField] RectTransform _timeAxisContent;
    [SerializeField] RectTransform _valueAxisContent;
    [SerializeField] TMP_Text _timeAxisTextPrefab;
    [SerializeField] TMP_Text _valueAxisTextPrefab;

    [Header("Setting")]
    [SerializeField][Min(2)] private int _sampleCount = 256;
    [SerializeField][Min(2)] private int _valueAxisCount = 3;
    [SerializeField] private float _minimumValue = 0f;
    [SerializeField] private float _maximumValue = 100f;
    [SerializeField][Min(1)] private int _timeAxisMax = 120;
    [SerializeField][Min(1)] private int _timeAxisInterval = 30;
    [SerializeField] private bool _test = false;

    private Coroutine _testRoutine;

    private Texture2D dataTexture;

    private float[] values;
    private Color32[] texturePixels;
    private readonly List<TimeAxisLabel> _timeAxisLabels = new();
    private float _initialTime = -1;
    private int _previousSamplePosition;
    private int _nextTimeAxisLabelTime;

    private readonly struct TimeAxisLabel
    {
        public TMP_Text Text { get; }
        public float Time { get; }

        public TimeAxisLabel(TMP_Text text, float time)
        {
            Text = text;
            Time = time;
        }
    }

    private void Awake()
    {
        var hight = _valueAxisContent.rect.height;
        var distance = (hight * 0.8f) / (_valueAxisCount-1);
        var value = (_maximumValue-_minimumValue) / (_valueAxisCount-1);
        for(int i = 0; i < _valueAxisCount; i++)
        {
            var text = Instantiate(_valueAxisTextPrefab, _valueAxisContent);
            text.transform.localPosition = new Vector3(0, -hight*0.4f + distance*i, 0);
            text.text = (_minimumValue + value*i).ToString("F1");
        }
        CreateTexture();
        ClearGraph();
    }

    private void OnDestroy()
    {
        if (dataTexture != null)
        {
            Destroy(dataTexture);
        }
    }

    private void LateUpdate()
    {
        if(_test)
        {
            if(_testRoutine == null)
            {
                _testRoutine = StartCoroutine(TestRoutine());
            }
        }
        else
        {
            if(_testRoutine != null)
            {
                StopCoroutine(_testRoutine);
                _testRoutine = null;
            }
        }
    }
    
    IEnumerator TestRoutine()
    {
        float time = 0;
        while(true)
        {
            time += 0.2f;
            AddValue(((Mathf.Sin(time) + 1)/2) * 100);
            yield return new WaitForSeconds(0.3f);
        }
    }

    private void CreateTexture()
    {
        values = new float[_sampleCount];
        texturePixels = new Color32[_sampleCount];

        dataTexture = new Texture2D(
            _sampleCount,
            1,
            TextureFormat.RGBA32,
            false,
            true
        );

        // シェーダー側で補間するためPointにする
        dataTexture.filterMode = FilterMode.Point;
        dataTexture.wrapMode = TextureWrapMode.Clamp;

        _rawImage.texture = dataTexture;
    }

    public void AddValue(float newValue, float time = -1)
    {
        var currentTime = time < 0 ? Time.time : time;
        if(_initialTime < 0)
        {
            _initialTime = currentTime;
        }
        var elapsedTime = currentTime - _initialTime;

        int moveDistance = CalculateMoveDistance(elapsedTime);
        ShiftAndInterpolateValues(moveDistance, newValue);

        UpdateTimeAxis(elapsedTime);
        UpdateTexture();
    }

    private void UpdateTimeAxis(float elapsedTime)
    {
        float visibleStartTime = elapsedTime - _timeAxisMax;

        // 時刻が大きく進んだ場合、既に画面外にあるラベルは生成しない。
        if (_nextTimeAxisLabelTime < visibleStartTime)
        {
            int skipCount = Mathf.CeilToInt(
                (visibleStartTime - _nextTimeAxisLabelTime) /
                _timeAxisInterval
            );
            _nextTimeAxisLabelTime += skipCount * _timeAxisInterval;
        }

        while (_nextTimeAxisLabelTime <= elapsedTime)
        {
            var text = Instantiate(_timeAxisTextPrefab, _timeAxisContent);
            text.text = FormatTime(_nextTimeAxisLabelTime);
            _timeAxisLabels.Add(
                new TimeAxisLabel(text, _nextTimeAxisLabelTime)
            );
            _nextTimeAxisLabelTime += _timeAxisInterval;
        }

        Rect rect = _timeAxisContent.rect;
        for (int i = _timeAxisLabels.Count - 1; i >= 0; i--)
        {
            TimeAxisLabel label = _timeAxisLabels[i];
            float normalizedPosition =
                1f - (elapsedTime - label.Time) / _timeAxisMax;

            // 左端を通過したラベルは不要なので破棄する。
            if (normalizedPosition < -0.1f)
            {
                Destroy(label.Text.gameObject);
                _timeAxisLabels.RemoveAt(i);
                continue;
            }

            float x = Mathf.LerpUnclamped(
                rect.xMin,
                rect.xMax,
                normalizedPosition
            );

            Transform labelTransform = label.Text.transform;
            Vector3 position = labelTransform.localPosition;
            position.x = x;
            labelTransform.localPosition = position;
        }
    }

    private int CalculateMoveDistance(float elapsedTime)
    {
        float secondsPerSample = (float)_timeAxisMax / _sampleCount;
        int currentSamplePosition = (int)Math.Round(
            elapsedTime / secondsPerSample,
            MidpointRounding.AwayFromZero
        );
        int moveDistance = Math.Max(
            0,
            currentSamplePosition - _previousSamplePosition
        );

        _previousSamplePosition = currentSamplePosition;
        return moveDistance;
    }

    private void ShiftAndInterpolateValues(int moveDistance, float newValue)
    {
        if (moveDistance == 0)
        {
            values[^1] = newValue;
            return;
        }

        float previousValue = values[^1];
        int retainedCount = Math.Max(0, values.Length - moveDistance);

        // 移動後も表示範囲に残る値を左へ詰める。
        for (int i = 0; i < retainedCount; i++)
        {
            values[i] = values[i + moveDistance];
        }

        // 移動で空いた部分を直前値から新しい値まで補間する。
        for (int i = retainedCount; i < values.Length; i++)
        {
            int step = moveDistance - (values.Length - 1 - i);
            float t = Mathf.Clamp01(step / (float)moveDistance);
            values[i] = Mathf.Lerp(previousValue, newValue, t);
        }
    }

    private void UpdateTexture()
    {
        for (int i = 0; i < values.Length; i++)
        {
            // minimumValue～maximumValueを0～1へ変換
            float normalizedValue = Mathf.InverseLerp(
                _minimumValue,
                _maximumValue,
                values[i]
            );

            float adjustedValue = Mathf.Lerp(
                0.1f,
                0.9f,
                normalizedValue
            );

            byte encodedValue = (byte)Mathf.RoundToInt(
                adjustedValue * 255f
            );

            // Rチャンネルへ値を保存
            texturePixels[i] = new Color32(
                encodedValue,
                0,
                0,
                255
            );
        }

        dataTexture.SetPixels32(texturePixels);
        dataTexture.Apply(false, false);
    }

    public void ClearGraph()
    {
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = _minimumValue;
        }

        foreach (TimeAxisLabel label in _timeAxisLabels)
        {
            if (label.Text != null)
            {
                Destroy(label.Text.gameObject);
            }
        }

        _timeAxisLabels.Clear();
        _initialTime = -1f;
        _previousSamplePosition = 0;
        _nextTimeAxisLabelTime = 0;

        UpdateTexture();
    }

    public void SetValueRange(float minimum, float maximum)
    {
        if (maximum <= minimum)
        {
            Debug.LogWarning(
                "maximumはminimumより大きくしてください。"
            );

            return;
        }

        _minimumValue = minimum;
        _maximumValue = maximum;

        UpdateTexture();
    }

    string FormatTime(int totalSeconds)
    {
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;

        if (hours > 0)
        {
            return $"{hours}:{minutes:00}:{seconds:00}";
        }

        if (minutes > 0)
        {
            return $"{minutes}:{seconds:00}";
        }

        return seconds.ToString("F1");
    }

    public void VisibleGraph(bool isVisible)
    {
        _rawImage.enabled = isVisible;
    }
}
