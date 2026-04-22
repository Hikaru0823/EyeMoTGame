using System;
using System.Collections;
using System.Collections.Generic;
using EyeMoT.Fusion;
using UnityEngine;

namespace EyeMoT.Balloon
{
    public class BalloonVolume : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private string _balloonTag = "Balloon";
        [SerializeField] private bool _isVisible = false;

        public Action<Balloon> onBalloonExited;

        private void Start()
        {
            if (!_isVisible)
                GetComponent<MeshRenderer>().material.SetColor( "_BaseColor", new Color(1f, 1f, 1f, 0.0f)); 
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(_balloonTag))
            {
                // バルーンが範囲から出たときの処理
                Balloon balloon = other.GetComponent<Balloon>();
                if (balloon != null && LobbyManager.Instance.Runner.IsServer)
                {
                    onBalloonExited?.Invoke(balloon);
                    BalloonSpawnManager.Instance.DeleteBalloon(balloon);
                }
            }
        }
    }
}