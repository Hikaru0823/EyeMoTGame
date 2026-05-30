using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EyeMoT
{
    public class PlayerImageEditor : MonoBehaviour
    {
        [Header("Resources")]
        [SerializeField] private Transform _imageContent;
        [SerializeField] private GameObject _loadingObject;
        [SerializeField] private Image _previewImage;
        [SerializeField] private TMP_Text _previewText;
        [SerializeField] private TMP_Text _pathText;

        public Dictionary<string, ImageItemUI> ImageItemByName = new();
        public Sprite CurrentSprite {get; private set;}

        private List<ImageItemUI> _defaultImageItems = new();
        private List<ImageItemUI> _customImageItems = new();

        private Animator _currentButtonAnimator;
        private string _currentSpriteName;

        string buttonPressed => StaticData.BUTTON_NORMAL_TO_PRESSED;
        string buttonNormal => StaticData.BUTTON_PRESSED_TO_NORMAL;

        void Awake()
        {
            var savedName = ES3.Load<string>(SaveKeys.PlayerImageName, defaultValue:"");
            _pathText.text = PlayerImageManager.Instance.CurrentUserImageDirectory;
            _defaultImageItems = PlayerImageManager.Instance.GenerateDefaultImageButtons(_imageContent, OnImageButtonClicked);
            foreach(var item in _defaultImageItems)
                if(!ImageItemByName.ContainsKey(item.Name))
                    ImageItemByName.Add(item.Name, item);

            if(ImageItemByName.ContainsKey(savedName))
            {
                UpdateImageState(ImageItemByName[savedName]);
                return;
            }

            var path = PlayerImageManager.Instance.CurrentUserImageDirectory + savedName;
            if(string.IsNullOrEmpty(savedName) || !File.Exists(path))
            {
                var randamImage = _defaultImageItems[UnityEngine.Random.Range(0, _defaultImageItems.Count)];
                UpdateImageState(randamImage);
                return;
            }

            byte[] pngData = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(pngData);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f)
            );
            _previewImage.sprite = sprite;
            _previewText.text = savedName;
            _currentSpriteName = savedName;
            PlayerData.Instance.PlayerImage = sprite;
            CurrentSprite = sprite;
            Debug.Log($"<color=orange>[PlayerImageEditor]</color> Loaded player image: {savedName}, Size: {texture.width}x{texture.height}, Bytes: {pngData.Length}");
        }

        private void UpdateImageState(ImageItemUI selectedItem)
        {
            _previewImage.sprite = selectedItem.Sprite;
            _previewText.text = selectedItem.Name;
            _currentSpriteName = selectedItem.Name;
            PlayerData.Instance.PlayerImage = selectedItem.Sprite;
            CurrentSprite = selectedItem.Sprite;

            _currentButtonAnimator?.Play(buttonNormal);
            _currentButtonAnimator = selectedItem.ButtonAnimator;
            _currentButtonAnimator?.Play(buttonPressed);
            ES3.Save<string>(SaveKeys.PlayerImageName, selectedItem.Name);
        }

        //UI hook
        public void OnLoadButtonClicked()
        {
            #if UNITY_WEBGL && !UNITY_EDITOR
            return;
            #endif
            ClearImageContent();
            LoadPlayerImages();
        }

        private void OnImageButtonClicked(ImageItemUI item)
        {
            if(_currentButtonAnimator == item.ButtonAnimator)
                return;

            UpdateImageState(item);
        }

        private async void LoadPlayerImages()
        {
            _loadingObject.SetActive(true);
            _customImageItems = await PlayerImageManager.Instance.GenerateImageButtons(_imageContent, OnImageButtonClicked);
            _loadingObject.SetActive(false);

            foreach(var item in _customImageItems)
                if(!ImageItemByName.ContainsKey(item.Name))
                    ImageItemByName.Add(item.Name, item);
            if(ImageItemByName.ContainsKey(_currentSpriteName))
                UpdateImageState(ImageItemByName[_currentSpriteName]);
        }

        private void ClearImageContent()
        {
            foreach (var item in _customImageItems)
            {
                if(ImageItemByName.ContainsKey(item.Name))
                    ImageItemByName.Remove(item.Name);
                if(_currentButtonAnimator == item.ButtonAnimator)
                    _currentButtonAnimator = null;
                Destroy(item.gameObject);
            }
            _customImageItems.Clear();
        }
    }
}
