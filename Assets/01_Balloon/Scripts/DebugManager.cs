using System.Collections;
using System.Collections.Generic;
using EyeMoT;
using EyeMoT.Balloon;
using EyeMoT.Heatmap;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DebugManager : Singleton<DebugManager>
{
    [SerializeField] private string _debugTag = "Debug";
    [SerializeField] private Canvas _debugCanvas;
    [SerializeField] private TMP_Text _debugText;
    private bool _isDebug = false;
    private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();
    private EventSystem _eventSystem;
    private PointerEventData _pointerEventData;

    void Start()
    {
        DebugOff();
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.F1))
        {
            if(!GameManager.Instance.IsStart) return;
            _isDebug = !_isDebug;
            if(_isDebug)
            {
                DebugOn();
            }
            else
            {
                DebugOff();
            }
        }
    }

    void LateUpdate()
    {
        if(!_isDebug) return;

        var tag = GetUIUnderPointerTag();
        UpdateDebugText(tag);

        if(CursorManager.Instance.IsCursorVisible) return;
        Cursor.visible = tag == _debugTag;
    }

    public void DebugOn()
    {
        _isDebug = true;
        HeatmapRenderer.Instance.VisibleHeatmap(true);
        _debugCanvas.enabled = true;
    }

    public void DebugOff()
    {
        _isDebug = false;
        HeatmapRenderer.Instance.VisibleHeatmap(false);
        _debugCanvas.enabled = false;

        Cursor.visible = false;
    }

    private void UpdateDebugText(string text = "")
    {
        _debugText.text = $"<color=orange>[UI Tag]</color> {text}";
    }

    private string GetUIUnderPointerTag()
    {
        if (EventSystem.current == null)
        {
            return string.Empty;
        }

        if (_eventSystem != EventSystem.current)
        {
            _eventSystem = EventSystem.current;
            _pointerEventData = new PointerEventData(_eventSystem);
        }

        _pointerEventData.position = Input.mousePosition;
        _raycastResults.Clear();
        _eventSystem.RaycastAll(_pointerEventData, _raycastResults);

        foreach (RaycastResult result in _raycastResults)
        {
            if (!(result.module is GraphicRaycaster))
            {
                continue;
            }

            Transform target = result.gameObject.transform;
            if (target.IsChildOf(_debugText.transform))
            {
                continue;
            }

            Transform canvasChild = GetDirectChildUnderCanvas(target);
            if (canvasChild != null)
            {
                return canvasChild.tag;
            }
        }

        return string.Empty;
    }

    private Transform GetDirectChildUnderCanvas(Transform target)
    {
        Canvas canvas = target.GetComponentInParent<Canvas>();
        if (canvas == null || target == canvas.transform)
        {
            return null;
        }

        while (target.parent != canvas.transform)
        {
            target = target.parent;
            if (target == null)
            {
                return null;
            }
        }

        return target;
    }

    #region HeatmapSettings

    public void SetHeatmapRadius(string value)
    {
        if(float.TryParse(value, out float radius))
        {
            HeatmapRenderer.Instance?.SetHeatmapRadius(radius);
        }
    }

    public void SetHeatmapIntensity(string value)
    {
        if(float.TryParse(value, out float intensity))
        {
            HeatmapRenderer.Instance?.SetHeatmapIntensity(intensity);
        }
    }

    public void SetHeatmapSoftness(string value)
    {
        if(float.TryParse(value, out float softness))
        {
            HeatmapRenderer.Instance?.SetHeatmapSoftness(softness);
        }
    }

    public void SetHeatmapOpacity(string value)
    {
        if(float.TryParse(value, out float opacity))
        {
            HeatmapRenderer.Instance?.SetHeatmapOpacity(opacity);
        }
    }

    public void SetHeatmapDecay(bool value)
    {
        HeatmapRenderer.Instance?.SetHeatmapDecay(value);
    }
    #endregion
}
