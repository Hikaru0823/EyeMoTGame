using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using EyeMoT.GameRecoder;

public class GameRecoderTestManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timerText;
    private float timer = 0f;
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {    
            GameRecoder.Instance.RecordStart();
            timer = 0f;
        }
        if (Input.GetKeyDown(KeyCode.E))
            GameRecoder.Instance.RecordEnd();
        timer += Time.deltaTime;
        timerText.text = $"Timer: {timer:F2} sec";
    }
}
