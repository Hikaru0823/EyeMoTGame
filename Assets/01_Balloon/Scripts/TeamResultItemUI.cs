using System.Collections;
using System.Collections.Generic;
using EyeMoT.Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EyeMoT.Balloon
{
    public class TeamResultItemUI : MonoBehaviour
    {
        [Header("Resources")]
        [SerializeField] private TMP_Text _totalScoreText;
        [SerializeField] private GameObject _topRankImage;
        [SerializeField] private Transform _playerResultItemHolder;
        [SerializeField] private ResultItemUI _playerResultItemPrefab;
        [SerializeField] private Sprite _defaultPlayerIcon;

        [Header("Settings")]
        [SerializeField] private PlayerRegistry.TeamState _team;

        public void Init(int totalScore, int rank)
        {
            _totalScoreText.text = totalScore.ToString();
            _topRankImage.SetActive(rank == 0);
        }

        public void AddPlayerResult(string playerName, int score, Sprite playerIcon = null)
        {
            var playerResultItem = Instantiate(_playerResultItemPrefab, _playerResultItemHolder);
            var color = PlayerRegistry.TeamColor[(int)_team];
            playerResultItem.Init(playerName, score, playerIcon ?? _defaultPlayerIcon, new Color(color.r, color.g, color.b, 0.8f));
        }

        public void ClearPlayerResults()
        {
            _topRankImage.SetActive(false);
            foreach(var item in _playerResultItemHolder.GetComponentsInChildren<ResultItemUI>())
            {
                Destroy(item.gameObject);
            }
        }
        public void RebuildLayout()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(
                _playerResultItemHolder.GetComponent<RectTransform>()
            );

            LayoutRebuilder.ForceRebuildLayoutImmediate(
                GetComponent<RectTransform>()
            );
        }
    }
}