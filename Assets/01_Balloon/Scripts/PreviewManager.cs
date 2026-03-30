using System.Collections;
using System.Collections.Generic;
using EyeMoT.Baloon;
using UnityEngine;

public class PreviewManager : MonoBehaviour
{
    private Balloon _previewBalloon;
    
    void Start()
    {
        _previewBalloon = BalloonSpawner.Instance.SpawnBalloon(transform.position + Vector3.forward * 2.45f, Vector3.zero);
        _previewBalloon.VisibleCollision(true);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
