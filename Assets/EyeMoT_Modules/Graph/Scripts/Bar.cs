using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EyeMoT.Graph
{
    public class Bar : MonoBehaviour
    {
        [SerializeField] private RectTransform _barImage;
        [SerializeField] private TMP_Text _valueText;
        [SerializeField] private Image _iconImage;

        public void Init(Color barColor, float value, float height)
        {
            _iconImage.color = barColor;
            _valueText.text = value.ToString("F2");
            _barImage.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            _barImage.GetComponent<Image>().color = barColor;
        }
    }
}

