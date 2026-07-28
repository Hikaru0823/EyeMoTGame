using UnityEngine;

public class CrowdfundingPicture : MonoBehaviour
{
    [SerializeField] private Vector2 _floatAmount = new Vector2(12f, 18f);
    [SerializeField, Min(0f)] private float _floatSpeed = 1.2f;
    [SerializeField, Min(0f)] private float _rotationAmount = 3f;
    [SerializeField, Range(0f, 360f)] private float _phaseOffset = 0f;

    private RectTransform _rectTransform;
    private Vector2 _defaultAnchoredPosition;
    private Quaternion _defaultLocalRotation;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        if (_rectTransform == null)
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        _defaultAnchoredPosition = _rectTransform.anchoredPosition;
        _defaultLocalRotation = _rectTransform.localRotation;
    }

    private void Update()
    {
        float phase = Time.time * _floatSpeed + _phaseOffset * Mathf.Deg2Rad;
        Vector2 offset = new Vector2(
            Mathf.Sin(phase * 0.8f) * _floatAmount.x,
            Mathf.Sin(phase) * _floatAmount.y
        );
        float rotationZ = Mathf.Sin(phase * 0.7f + Mathf.PI * 0.25f) * _rotationAmount;

        _rectTransform.anchoredPosition = _defaultAnchoredPosition + offset;
        _rectTransform.localRotation = _defaultLocalRotation * Quaternion.Euler(0f, 0f, rotationZ);
    }

    private void OnDisable()
    {
        if (_rectTransform != null)
        {
            _rectTransform.anchoredPosition = _defaultAnchoredPosition;
            _rectTransform.localRotation = _defaultLocalRotation;
        }
    }
}
