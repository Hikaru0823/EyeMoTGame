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
        [SerializeField] private Image _topRankImage;
        [SerializeField] private Sprite _defaultPlayerIcon;

        public void Init(string playerName, int score, int rank, Sprite playerIcon = null)
        {
            _playerImage.sprite = playerIcon ?? _defaultPlayerIcon;
            _playerNameText.text = playerName;
            _scoreText.text = score.ToString();
            _topRankImage.gameObject.SetActive(rank == 0);
        }
    }
}