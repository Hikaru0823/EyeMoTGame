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
    public class PlayerBGMManager : Singleton<PlayerBGMManager>
    {
        [Header("Resources")]
        [SerializeField] private BGMItemUI _bgmPrefab;
        [SerializeField] private AudioClip[] _defaultBGMs;

        [Header("Settings")]
        [SerializeField] private string _originalPrefix = "EyeMoT_Module_0823";
        [SerializeField] private string _userBGMDirectory = "/YOUR_RESOURCES/BGM/";
        [SerializeField] private float _loadOffset = 0.5f;

        private CancellationTokenSource cancellationTokenSource;
        private bool _isLoading = false;
        public string CurrentUserBGMDirectory => Directory.GetParent(Application.dataPath)?.FullName + _userBGMDirectory;


        public async Task<List<BGMItemUI>> GenerateBGMButtons(Transform content, Action<BGMItemUI> onBGMButtonClicked)
        {
            if(_isLoading)
            {
                Debug.LogWarning($"<color=orange>[BGMLoader]</color> Already loading BGMs. Please wait.");
                return null;
            }
            #if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"<color=orange>[BGMLoader]</color> WebGL platform does not support BGMLoader. Initialization skipped.");
            return null;
            #endif
            string directoryPath = CurrentUserBGMDirectory;

            _isLoading = true;
            Debug.Log($"<color=orange>[BGMLoader]</color>Loading user BGMs from: {directoryPath}");

            List<BGMItemUI> bgmItems = new List<BGMItemUI>();

            try
            {
                await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(_loadOffset));
                cancellationTokenSource = new CancellationTokenSource();
                var bgmInfos = await BGMDirectoryLoader.LoadAllBgmsAsync(directoryPath, cancellationTokenSource.Token);
                foreach (var bgmInfo in bgmInfos)
                {
                    // Here you can convert the byte array to a AudioClip and use it in your game
                    AudioClip audioClip = bgmInfo.AudioClip;
                    var bgmItem = Instantiate(_bgmPrefab, content);
                    var fileName = Path.GetFileName(bgmInfo.FilePath);
                    bgmItem.Init(fileName, audioClip, onBGMButtonClicked);
                    bgmItems.Add(bgmItem);
                    //Debug.Log($"<color=orange>[BGMLoader]</color>Loaded BGM: {bgmInfo.FilePath}, Size: {audioClip.length} seconds");
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"<color=red>[BGMLoader]</color>Failed to load user BGMs from {directoryPath}: {exception}");
                return null;
            }
            finally
            {
                _isLoading = false;
            }
            return bgmItems;
        }

        public List<BGMItemUI> GenerateDefaultBGMButtons(Transform content, Action<BGMItemUI> onBGMButtonClicked)
        {
            List<BGMItemUI> bgmItems = new List<BGMItemUI>();
            foreach (var defaultBGM in _defaultBGMs)
            {
                var bgmItem = Instantiate(_bgmPrefab, content);
                bgmItem.Init(defaultBGM.name, defaultBGM, onBGMButtonClicked);
                bgmItems.Add(bgmItem);
            }

            return bgmItems;
        }

        void OnDestroy()
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }
    }
}
