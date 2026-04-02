using System.Collections;
using System.Collections.Generic;
using EyeMoT.Heatmap;
using KanKikuchi.AudioManager;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem.Interactions;

namespace EyeMoT.Baloon
{
    public class GameManager : MonoBehaviour
    {
        [Header("Resources")]
        [SerializeField] private TMP_Text _gameTimeText;
        [SerializeField] private TMP_Text _balloonCountText;
        [SerializeField] private Animator _resultPanel;

        private bool _isStart = false;
        private float _time = 0f;
        private int _balloonCount = 0;

        void Start()
        {
            BGMManager.Instance.Play(BGMPath.BALLOON_TITLE, volumeRate: 0.5f);
            BalloonSpawner.Instance.OnBalloonDestroyed += UpdateBalloonCount;
        }

        void Update()
        {
            if(!_isStart) return;

            _time += Time.deltaTime;
            _gameTimeText.text = (SettingManager.Instance.GameData.GameTime - _time).ToString("F1") + "s";

            if(SettingManager.Instance.GameData.GameTime - _time <= 0)
            {
                _isStart = false;
                GameEnd();
            }
        }

        public void GameStart()
        {
            HeatmapRenderer.Instance.StartHeatmap("01_Balloon");
            BGMManager.Instance.Play(BGMPath.BALLOON_GAME, volumeRate: 0.5f);
            BalloonSpawner.Instance.SpawnInitialBalloons(SettingManager.Instance.GameData.BalloonGeneratePatern);
            _gameTimeText.text = SettingManager.Instance.GameData.GameTime.ToString("F1") + "s";
            _balloonCountText.text = "×" + 0;
            _balloonCount = 0;
            _time = 0f;
            _isStart = true;
        }

        public void GameEnd()
        {
            _resultPanel.Play(StaticData.PANEL_FADE_IN);
            var totalDistance = HeatmapRenderer.Instance.StopHeatmap();
            HeatmapRenderer.Instance.VisibleHeatmap(true);
            _gameTimeText.text = "0.0s";
            BalloonSpawner.Instance.ResetBalloons();
        }

        public void GameRestart()
        {
            _resultPanel.Play(StaticData.PANEL_FADE_OUT);
            HeatmapRenderer.Instance.VisibleHeatmap(false);
            GameStart();
        }

        // Update is called once per frame
        public void GameExit()
        {
            if(_resultPanel.GetCurrentAnimatorStateInfo(0).IsName(StaticData.PANEL_FADE_IN))
                _resultPanel.Play(StaticData.PANEL_FADE_OUT);
            var totalDistance = HeatmapRenderer.Instance.StopHeatmap(false);
            HeatmapRenderer.Instance.VisibleHeatmap(false);
            BGMManager.Instance.Play(BGMPath.BALLOON_TITLE, volumeRate: 0.5f);
            _isStart = false;
            BalloonSpawner.Instance.ResetBalloons();
        }

        private void UpdateBalloonCount()
        {
            _balloonCount++;
            _balloonCountText.text = "×" + _balloonCount;
        }
    }
}