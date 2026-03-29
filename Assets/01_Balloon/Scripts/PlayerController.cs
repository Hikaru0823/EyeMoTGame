using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float _moveTime = 40f;
    [SerializeField] private float _moveDistance = 1f;
    private Vector3 _initPosition;

    void Start()
    {
        _initPosition = transform.position;
    }

    void FixedUpdate()
    {
        UpdatePosition();
    }

    private void UpdatePosition()
    {
        float xOffset = 0f;

        if (_moveTime > 0f)
        {
            float phase = Time.time / _moveTime * Mathf.PI * 2f;
            xOffset = Mathf.Sin(phase) * _moveDistance;
        }

        transform.position = _initPosition + new Vector3(xOffset, 0f, 0f);
    }
}
