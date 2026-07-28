using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace EyeMoT.Graph
{
    public class GraphGenerater : MonoBehaviour
    {
        [SerializeField] private Bar _barPrefab;
        [SerializeField] private Transform _barParent;
        [SerializeField] private CriterialItem _criterialItemPrefab;
        [SerializeField] private CriterialItem _criterial_Zero;
        [SerializeField] private Transform _criterialItemParent;
        [SerializeField] private TMP_Text _averageText;
        [SerializeField] private RectTransform _averageLine;


        public void GenerateGraph(List<float> values, float average, float maxValue, List<Color> barColors)
        {
            ClearGraph();
            _averageText.text = average.ToString("F2");
            _averageLine.anchoredPosition = new Vector2(0, _criterialItemParent.GetComponent<RectTransform>().rect.height * ((average/maxValue)-1) -30);

            int numberOfCriterialItems = 5;

            GenerateCriterialItems(numberOfCriterialItems, maxValue);
            GenerateBars(values, barColors, numberOfCriterialItems, maxValue);
        }

        private void ClearGraph()
        {
            foreach (Transform child in _barParent)
            {
                Destroy(child.gameObject);
            }

            foreach (Transform child in _criterialItemParent)
            {
                if(child.gameObject != _criterial_Zero.gameObject)
                    Destroy(child.gameObject);
            }
        }

        private void GenerateCriterialItems(int items, float maxValue)
        {
            int numberOfCriterialItems = items;
            float step = maxValue / numberOfCriterialItems;

            for (int i = 1; i <= numberOfCriterialItems; i++)
            {
                var criterialItem = Instantiate(_criterialItemPrefab, _criterialItemParent);
                criterialItem.Init((step * (numberOfCriterialItems - i + 1)).ToString("F2"), (_criterialItemParent.GetComponent<RectTransform>().rect.height-30)/numberOfCriterialItems); // Assuming a fixed height for the scale items
            }
            _criterial_Zero.transform.SetAsLastSibling();
        }

        private void GenerateBars(List<float> values, List<Color> barColors, int items, float maxValue)
        {
            for(int i = 0; i < values.Count; i++)
            {
                var bar = Instantiate(_barPrefab, _barParent);
                var height = (_criterialItemParent.GetComponent<RectTransform>().rect.height-30) * (values[i]/maxValue);
                bar.Init(barColors[i], values[i], height); // Assuming a fixed height for the bars based on the value
            }
        }
    }
}

