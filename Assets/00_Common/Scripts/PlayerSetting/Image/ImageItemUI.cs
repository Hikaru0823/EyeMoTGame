using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EyeMoT
{
    public class ImageItemUI : MonoBehaviour
    {
        [SerializeField] private Image _imageDisplay;

        public void SetImage(Texture2D texture)
        {
            _imageDisplay.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
    }
}