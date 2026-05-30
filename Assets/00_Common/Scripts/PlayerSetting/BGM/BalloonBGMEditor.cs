using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using KanKikuchi.AudioManager;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

namespace EyeMoT.Balloon
{
    public class BalloonBGMEditor : SceneSingleton<BalloonBGMEditor>
    {
        [Header("Resources")]
        [SerializeField] private Transform _bgmContent;
        [SerializeField] private GameObject _loadingObject;
        [SerializeField] private TMP_Text _previewText;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private TMP_Text _pathText;


        public Dictionary<string, BGMItemUI> BGMItemByName = new();
        public AudioClip CurrentBGM {get; private set;}

        private List<BGMItemUI> _defaultBGMItems = new();
        private List<BGMItemUI> _customBGMItems = new();

        private Animator _currentButtonAnimator;
        private string _currentBGMName;

        string buttonPressed => StaticData.BUTTON_NORMAL_TO_PRESSED;
        string buttonNormal => StaticData.BUTTON_PRESSED_TO_NORMAL;


        protected override void OnAwake()
        {
            var savedName = ES3.Load<string>(SaveKeys.BalloonBGM, defaultValue:"");
            _pathText.text = PlayerBGMManager.Instance.CurrentUserBGMDirectory;
            _defaultBGMItems = PlayerBGMManager.Instance.GenerateDefaultBGMButtons(_bgmContent, OnBGMButtonClicked);
            foreach(var item in _defaultBGMItems)
                if(!BGMItemByName.ContainsKey(item.Name))
                    BGMItemByName.Add(item.Name, item);

            if(BGMItemByName.ContainsKey(savedName))
            {
                UpdateBGMState(BGMItemByName[savedName]);
                return;
            }

            var path = PlayerBGMManager.Instance.CurrentUserBGMDirectory + savedName;
            if(string.IsNullOrEmpty(savedName) || !File.Exists(path))
            {
                var randamBGM = _defaultBGMItems[UnityEngine.Random.Range(0, _defaultBGMItems.Count)];
                UpdateBGMState(randamBGM);
                return;
            }

            LoadSavedBGM(path);
        }

        private void UpdateBGMState(BGMItemUI selectedItem)
        {
            _previewText.text = selectedItem.Name;
            _descriptionText.text = $"Length: {selectedItem.AudioClip.length:F2} sec, Sample Rate: {selectedItem.AudioClip.frequency} Hz, Channels: {selectedItem.AudioClip.channels}";
            _currentBGMName = selectedItem.Name;
            CurrentBGM = selectedItem.AudioClip;

            _currentButtonAnimator?.Play(buttonNormal);
            _currentButtonAnimator = selectedItem.ButtonAnimator;
            _currentButtonAnimator?.Play(buttonPressed);
            ES3.Save<string>(SaveKeys.BalloonBGM, selectedItem.Name);
        }

        //UI hook
        public void OnLoadButtonClicked()
        {
            #if UNITY_WEBGL && !UNITY_EDITOR
            return;
            #endif
            ClearBGMContent();
            LoadPlayerBGMs();
        }

        //UI hook
        public void SetTitleBGM()
        {
            if(BGMManager.Instance.GetCurrentAudioNames().Contains(Path.GetFileName(BGMPath.BALLOON_TITLE)))
                return;
            BGMManager.Instance.Play(BGMPath.BALLOON_TITLE, volumeRate: 0.5f);
        }

        private void OnBGMButtonClicked(BGMItemUI item)
        {
            if(_currentButtonAnimator == item.ButtonAnimator)
                return;
            BGMManager.Instance.Play(item.AudioClip, volumeRate: 0.5f);
            UpdateBGMState(item);
        }

        private async void LoadPlayerBGMs()
        {
            _loadingObject.SetActive(true);
            _customBGMItems = await PlayerBGMManager.Instance.GenerateBGMButtons(_bgmContent, OnBGMButtonClicked);
            _loadingObject.SetActive(false);

            foreach(var item in _customBGMItems)
                if(!BGMItemByName.ContainsKey(item.Name))
                    BGMItemByName.Add(item.Name, item);
            if(BGMItemByName.ContainsKey(_currentBGMName))
                UpdateBGMState(BGMItemByName[_currentBGMName]);
        }

        private void ClearBGMContent()
        {
            foreach (var item in _customBGMItems)
            {
                if(BGMItemByName.ContainsKey(item.Name))
                    BGMItemByName.Remove(item.Name);
                if(_currentButtonAnimator == item.ButtonAnimator)
                    _currentButtonAnimator = null;
                Destroy(item.gameObject);
            }
            _customBGMItems.Clear();
        }

        private async void LoadSavedBGM(string filePath)
        {
            CompressedBGMInfo info = await GetAudioClip(filePath);

            _previewText.text = info.FileName;
            _descriptionText.text = $"Length: {info.AudioClip.length:F2} sec,\n Sample Rate: {info.AudioClip.frequency} Hz,\n Channels: {info.AudioClip.channels}";
            _currentBGMName = info.FileName;
            CurrentBGM = info.AudioClip;
            Debug.Log($"<color=orange>[BalloonBGMEditor]</color> Loaded balloon BGM: {info.FileName}");
        }

        private async Task<CompressedBGMInfo> GetAudioClip(string filePath)
        {
            if (!BGMDirectoryLoader.TryGetAudioType(filePath, out AudioType audioType))
                return null;

            try
            {
                CompressedBGMInfo info = await BGMDirectoryLoader.LoadAudioClipAsync(filePath, audioType);

                return info;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"音源読み込み失敗: {filePath}\n{ex.Message}");
                return null;
            }
        }
    }
}