using UnityEngine;

public class GazeAnalyseTarget : MonoBehaviour
{
    [Header("Target Info")]
    public string targetId = "01";
    public string label = "風船";
    public string group = "balloon";

    [Header("Analysis")]
    public GazeAnalysisStartMode startMode = GazeAnalysisStartMode.OnFirstEnter;
    public GazeTargetShape targetShape = GazeTargetShape.Circle;

    [Tooltip("マイナスならCollider / RectTransform / Rendererから自動推定")]
    public float radiusPx = -1f;

    [Tooltip("生データを保存する。通常はfalse推奨")]
    public bool keepRawSamples = false;

    [Header("Auto Register")]
    public bool registerOnEnable = true;
    public bool unregisterOnDisable = true;

    [Header("Debug")]
    [SerializeField] private string registeredId;
    [SerializeField] private bool isRegistered;

    public string RegisteredId
    {
        get { return registeredId; }
    }

    public bool IsRegistered
    {
        get { return isRegistered; }
    }

    private void OnEnable()
    {
        if (registerOnEnable)
        {
            Register();
        }
    }

    private void OnDisable()
    {
        if (unregisterOnDisable)
        {
            Unregister();
        }
    }

    private void OnDestroy()
    {
        Unregister();
    }

    public void Register()
    {
        if (isRegistered)
        {
            return;
        }

        if (!GazeAnalyseManager.IsAnalyzing)
        {
            return;
        }

        registeredId = GazeAnalyseManager.Add(
            targetObject: gameObject,
            targetId: targetId,
            label: string.IsNullOrEmpty(label) ? gameObject.name : label,
            group: group,
            startMode: startMode,
            radiusPx: radiusPx,
            targetShape: targetShape,
            keepRawSamples: keepRawSamples
        );

        isRegistered = !string.IsNullOrEmpty(registeredId);
    }

    public GazeTargetResult Unregister()
    {
        if (!isRegistered || string.IsNullOrEmpty(registeredId))
        {
            return null;
        }

        if (!GazeAnalyseManager.HasInstance)
        {
            registeredId = "";
            isRegistered = false;
            return null;
        }

        GazeTargetResult result = GazeAnalyseManager.Remove(registeredId);

        registeredId = "";
        isRegistered = false;

        return result;
    }
}