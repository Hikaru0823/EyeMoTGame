using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AlwaysFacingCamera : MonoBehaviour
{
    [SerializeField] private Camera _targetCamera;
    
    void Start()
    {
        if (_targetCamera == null)
            _targetCamera = Camera.main;
    }

    // Update is called once per frame
    void LateUpdate()
    {
        if (_targetCamera == null)            return;
        Vector3 directionToCamera = transform.position - _targetCamera.transform.position;
        if (directionToCamera.sqrMagnitude > 0.001f) // Avoid zero-length direction
        {
            transform.rotation = Quaternion.LookRotation(directionToCamera);
        }
    }
}
