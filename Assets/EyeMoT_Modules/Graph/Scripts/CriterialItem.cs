using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace EyeMoT.Graph
{
    public class CriterialItem : MonoBehaviour
    {
        [SerializeField] private RectTransform _transform;
        [SerializeField] private TMP_Text _scaletText;
        public void Init(string scaleText, float height)
        {
            _scaletText.text = scaleText;
            _transform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }
    }
}