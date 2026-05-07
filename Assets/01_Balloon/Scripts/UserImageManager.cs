using System;
using System.IO;
using UnityEngine;

namespace EyeMoT
{
    public class UserImageManager : MonoBehaviour
    {
        [SerializeField] private string _userImageDirectory = "/YOUR_RESOURCES/Images/";
        [SerializeField] private int _maxImageSize = 512;
        [SerializeField] private Transform _imageContent;
        [SerializeField] private ImageItemUI _imagePrefab;

        private string CurrentUserImageDirectory => Directory.GetParent(Application.dataPath)?.FullName + _userImageDirectory;

        public void OnButtonClicked()
        {
            LoadUserImages();
        }
        public async void LoadUserImages()
        {
            string directoryPath = CurrentUserImageDirectory;
            Debug.Log($"<color=orange>[ImageLoader]</color>Loading user images from: {directoryPath}");

            try
            {
                Loading.Instance.SetVisible(true);
                var imageInfos = await ImageDirectoryLoader.LoadResizedImagesAsync(directoryPath, _maxImageSize);
                foreach (var imageInfo in imageInfos)
                {
                    Debug.Log($"<color=orange>[ImageLoader]</color>Loaded image: {imageInfo.FilePath}, Size: {imageInfo.Texture.width}x{imageInfo.Texture.height}");
                    // Here you can convert the byte array to a Texture2D and use it in your game
                    Texture2D texture = new Texture2D(imageInfo.Texture.width, imageInfo.Texture.height);
                    texture.LoadImage(imageInfo.Texture.EncodeToPNG());

                    var imageItem = Instantiate(_imagePrefab, _imageContent);
                    imageItem.SetImage(texture);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"<color=red>[ImageLoader]</color>Failed to load user images from {directoryPath}: {exception}");
            }
            finally
            {
                Loading.Instance.SetVisible(false);
            }
        }
    }
}
