using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EyeMoT
{
    public class PlayerImageManager : Singleton<PlayerImageManager>
    {
        [Header("Resources")]
        [SerializeField] private ImageItemUI _imagePrefab;
        [SerializeField] private Sprite[] _defaultImages;

        [Header("Settings")]
        [SerializeField] private string _originalPrefix = "EyeMoT_Module_0823";
        [SerializeField] private string _userImageDirectory = "/YOUR_RESOURCES/Images/";
        [SerializeField] private int _maxImageSize = 512;
        [SerializeField] private float _loadOffset = 0.5f;

        private CancellationTokenSource cancellationTokenSource;
        private bool _isLoading = false;
        public string CurrentUserImageDirectory => Directory.GetParent(Application.dataPath)?.FullName + _userImageDirectory;


        public async Task<List<ImageItemUI>> GenerateImageButtons(Transform content, Action<ImageItemUI> onImageButtonClicked)
        {
            if(_isLoading)
            {
                Debug.LogWarning($"<color=orange>[ImageLoader]</color> Already loading images. Please wait.");
                return null;
            }
            #if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"<color=orange>[ImageLoader]</color> WebGL platform does not support ImageLoader. Initialization skipped.");
            return null;
            #endif
            string directoryPath = CurrentUserImageDirectory;

            _isLoading = true;
            Debug.Log($"<color=orange>[ImageLoader]</color>Loading user images from: {directoryPath}");

            List<ImageItemUI> imageItems = new List<ImageItemUI>();

            try
            {
                await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(_loadOffset));
                cancellationTokenSource = new CancellationTokenSource();
                var imageInfos = await ImageDirectoryLoader.LoadResizedImagesAsync(directoryPath, _maxImageSize, cancellationTokenSource.Token);
                foreach (var imageInfo in imageInfos)
                {
                    // Here you can convert the byte array to a Texture2D and use it in your game
                    Texture2D texture = new Texture2D(imageInfo.Texture.width, imageInfo.Texture.height);
                    var imageBytes = imageInfo.Texture.EncodeToPNG();
                    texture.LoadImage(imageBytes);
                    var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    var imageItem = Instantiate(_imagePrefab, content);
                    var fileName = Path.GetFileName(imageInfo.FilePath);
                    imageItem.Init(fileName, sprite, Color.white, onImageButtonClicked);
                    imageItems.Add(imageItem);
                    //Debug.Log($"<color=orange>[ImageLoader]</color>Loaded image: {imageInfo.FilePath}, Size: {imageInfo.Texture.width}x{imageInfo.Texture.height}, Bytes: {imageBytes.Length}");
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"<color=red>[ImageLoader]</color>Failed to load user images from {directoryPath}: {exception}");
                return null;
            }
            finally
            {
                _isLoading = false;
            }
            return imageItems;
        }

        public List<ImageItemUI> GenerateDefaultImageButtons(Transform content, Action<ImageItemUI> onImageButtonClicked)
        {
            List<ImageItemUI> imageItems = new List<ImageItemUI>();
            foreach (var defaultImage in _defaultImages)
            {
                var imageItem = Instantiate(_imagePrefab, content);
                imageItem.Init(defaultImage.name, defaultImage, Color.white, onImageButtonClicked);
                imageItems.Add(imageItem);
            }
            return imageItems;
        }

        private void OnDestroy()
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }
    }
}
