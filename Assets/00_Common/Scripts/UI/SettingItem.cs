using System.Collections;
using System.Collections.Generic;
using KanKikuchi.AudioManager;
using UnityEngine;
using UnityEngine.EventSystems;

namespace EyeMoT
{
    public class SettingItem : MonoBehaviour, IPointerEnterHandler
    {
        [Header("Resources")]
        [SerializeField] private PreviewManager _previewManager;
        [Header("Seting")]
        [SerializeField] private string _itemName;

        public void OnPointerEnter(PointerEventData eventData)
        {
            SEManager.Instance.Play(SEPath.HOVER);
            _previewManager.ShowItem(_itemName);
        }
    }
}