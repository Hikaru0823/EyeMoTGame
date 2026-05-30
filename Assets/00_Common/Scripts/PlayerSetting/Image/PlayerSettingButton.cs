using KanKikuchi.AudioManager;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EyeMoT
{
    public class PlayerSettingButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("Resources")]
        [SerializeField] private GameObject[] _highlitedObjects;
        [SerializeField] private TMP_Text previewText;
        [SerializeField] private Image _previewImage;

        void Start()
        {
            Init();
        }

        void Init()
        {
            previewText.text = PlayerSettingManager.Instance?.CurrentName;
            _previewImage.sprite = PlayerSettingManager.Instance?.CurrentPlayerImage;
        }

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

        public void OpenPanel()
        {
            PlayerSettingManager.Instance?.Visible(() => Init());
        }
    }
}