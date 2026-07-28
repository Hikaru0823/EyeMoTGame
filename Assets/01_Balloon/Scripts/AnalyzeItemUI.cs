using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EyeMoT.Balloon
{
    public class AnalyzeItemUI : MonoBehaviour
    {
        [Header("Resources")]
        [SerializeField] private TMP_Text _accuracyText;
        [SerializeField] private TMP_Text _stabilityText;
        [SerializeField] private TMP_Text _attentionText;
        [SerializeField] private Image _balloonImage;
        public void Init(GazeTargetResult gazeTargetResult, Color balloonColor)
        {
            _accuracyText.text = gazeTargetResult.accuracyScore.ToString("F2");
            _stabilityText.text = gazeTargetResult.stabilityScore.ToString("F2");
            _attentionText.text = gazeTargetResult.attentionScore.ToString("F2");
            _balloonImage.color = balloonColor;
        }
    }
}