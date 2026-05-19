using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EyeMoT.Balloon
{
    public class ResultItemUI : MonoBehaviour
    {
        [Header("Resources")]
        [SerializeField] private TMP_Text _playerNameText;
        [SerializeField] private TMP_Text _scoreText;
        [SerializeField] private Image _playerImage;
        [SerializeField] private Image _bgImage;

        public void Init(string playerName, int score, Sprite playerIcon, Color bgColor)
        {
            _playerImage.sprite = playerIcon;
            _playerNameText.text = playerName;
            _scoreText.text = score.ToString();
            _bgImage.color = bgColor;
        }

        public void UpdateScore(int newScore)
        {
            _scoreText.text = newScore.ToString();
        }
    }
}