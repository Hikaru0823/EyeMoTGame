using KanKikuchi.AudioManager;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EyeMoT
{
    public class PlayerSettingButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("Resources")]
        [SerializeField] private GameObject[] _highlitedObjects;

        public void OnPointerEnter(PointerEventData eventData)
        {
            foreach (var obj in _highlitedObjects)
            {
                obj.SetActive(true);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            foreach (var obj in _highlitedObjects)
            {
                obj.SetActive(false);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            foreach (var obj in _highlitedObjects)
            {
                obj.SetActive(false);
            }
        }
    }
}