using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EyeMoT
{
    public class PlayerImageManager : Singleton<PlayerImageManager>
    {
        [Header("Resources")]
        [SerializeField] private Transform _imageContent;
        [SerializeField] private ImageItemUI _imagePrefab;
        [SerializeField] private GameObject _loadingObject;
        [SerializeField] private Image[] _previewImages;
        [SerializeField] private TMP_Text _previewText;
        [SerializeField] private Sprite[] _defaultImages;

        [Header("Settings")]
        [SerializeField] private string _userImageDirectory = "/YOUR_RESOURCES/Images/";
        [SerializeField] private string _originalPrefix = "EyeMoT_Module_0823";
        [SerializeField] private int _maxImageSize = 512;
        [SerializeField] private float _loadOffset = 0.5f;

        public Dictionary<string, Sprite> SpriteByName = new();
        public Sprite GetCurrentSprite() => SpriteByName.ContainsKey(_currentSpriteName) ? SpriteByName[_currentSpriteName] : null;

        private Animator _currentButtonAnimator;
        private string _currentSpriteName;
        private bool _isLoading = false;

        string buttonPressed => StaticData.BUTTON_NORMAL_TO_PRESSED;
        string buttonNormal => StaticData.BUTTON_PRESSED_TO_NORMAL;

        private string CurrentUserImageDirectory => Directory.GetParent(Application.dataPath)?.FullName + _userImageDirectory;

        void Start()
        {
            var path = ES3.Load<string>(SaveKeys.PlayerImagePath, defaultValue:"");

            if (string.IsNullOrEmpty(path))
            {
                var randamImage = _defaultImages[UnityEngine.Random.Range(0, _defaultImages.Length)];
                foreach(var image in _previewImages)
                    image.sprite = randamImage;
                _previewText.text = randamImage.name;
                _currentSpriteName = "";
                PlayerData.Instance.PlayerImage = randamImage;
                return;
            }

            if(!File.Exists(path))
            {
                Sprite defaultSprite =  null;
                foreach(var defaultImage in _defaultImages)
                {
                    if(defaultImage.name + _originalPrefix == path)
                        defaultSprite = defaultImage;
                }
                if(defaultSprite != null)
                {
                    foreach(var image in _previewImages)
                        image.sprite = defaultSprite;
                    _previewText.text = "";
                    _currentSpriteName = path;
                    PlayerData.Instance.PlayerImage = defaultSprite;
                }
                
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
            foreach(var image in _previewImages)
                image.sprite = sprite;
            _previewText.text = Path.GetFileName(path);
            _currentSpriteName = Path.GetFileName(path);
            PlayerData.Instance.PlayerImage = sprite;
        }

        //UI hook
        public void OnLoadButtonClicked()
        {
            ClearImageContent();
            LoadUserImages();
        }

        private void OnImageButtonClicked(Button button, string path)
        {
            var animator = button.GetComponent<Animator>();
            if(_currentButtonAnimator == animator)
                return;
            if(_currentButtonAnimator != null)
                _currentButtonAnimator.Play(buttonNormal);
            _currentButtonAnimator = animator;
            _currentSpriteName = button.name;
            ES3.Save<string>(SaveKeys.PlayerImagePath, path);
            var currentSprite = GetCurrentSprite();
            PlayerData.Instance.PlayerImage = currentSprite != null ? currentSprite : null;
            foreach(var image in _previewImages)
                image.sprite = currentSprite;
            _previewText.text = button.name.Contains(_originalPrefix) ? "" : button.name;
            _currentButtonAnimator?.Play(buttonPressed);
        }

        private void ClearImageContent()
        {
            SpriteByName.Clear();
            for (int i = _imageContent.childCount - 1; i >= 0; i--)
            {
                Destroy(_imageContent.GetChild(i).gameObject);
            }

            foreach(var sprite in _defaultImages)
            {
                var name = sprite.name + _originalPrefix;
                CreateImageButton(name, sprite, name);
            }
        }

        public async void LoadUserImages()
        {
            if(_isLoading)
            {
                Debug.LogWarning($"<color=orange>[ImageLoader]</color> Already loading images. Please wait.");
                return;
            }
            #if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"<color=orange>[ImageLoader]</color> WebGL platform does not support ImageLoader. Initialization skipped.");
            return;
            #endif
            string directoryPath = CurrentUserImageDirectory;
            _isLoading = true;
            Debug.Log($"<color=orange>[ImageLoader]</color>Loading user images from: {directoryPath}");

            try
            {
                _loadingObject.SetActive(true);
                await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(_loadOffset));
                var imageInfos = await ImageDirectoryLoader.LoadResizedImagesAsync(directoryPath, _maxImageSize);
                foreach (var imageInfo in imageInfos)
                {
                    // Here you can convert the byte array to a Texture2D and use it in your game
                    Texture2D texture = new Texture2D(imageInfo.Texture.width, imageInfo.Texture.height);
                    var imageBytes = imageInfo.Texture.EncodeToPNG();
                    texture.LoadImage(imageBytes);
                    var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    CreateImageButton(imageInfo.FileName, sprite, imageInfo.FilePath);
                    Debug.Log($"<color=orange>[ImageLoader]</color>Loaded image: {imageInfo.FilePath}, Size: {imageInfo.Texture.width}x{imageInfo.Texture.height}, Bytes: {imageBytes.Length}");
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"<color=red>[ImageLoader]</color>Failed to load user images from {directoryPath}: {exception}");
            }
            finally
            {
                _isLoading = false;
                _loadingObject.SetActive(false);
            }
        }

        private void CreateImageButton(string name, Sprite sprite, string path = "")
        {
            var imageItem = Instantiate(_imagePrefab, _imageContent);
            imageItem.name = name;
            if(!SpriteByName.ContainsKey(name))
                SpriteByName.Add(name, sprite);
            imageItem.SetImage(sprite);
            imageItem.Button.onClick.AddListener(() => OnImageButtonClicked(imageItem.Button, path));
            if(_currentSpriteName == name)
                OnImageButtonClicked(imageItem.Button, path);
        }
    }
}
