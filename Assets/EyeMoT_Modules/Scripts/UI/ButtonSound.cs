using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EyeMoT
{
    public class ButtonSound : MonoBehaviour, IPointerEnterHandler
    {
        [Header("Resources")]
        public Button button;

        void Awake()
        {
            if(button == null)
                button = GetComponent<Button>();

            button.onClick.AddListener(() => OnClick());
        }

        public void OnClick()
        {
            //SEManager.Instance.Play(SEPath.CLICK);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            //if(button.interactable)
                //SEManager.Instance.Play(SEPath.HOVER);
        }
    }
}