using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace EyeMoT
{
    public class NameEditor : Singleton<NameEditor>
    {
        public enum ConvertType
        {
            HIRAGANA,
            KATAKANA,
            UPPER_ALPHABET,
            LOWER_ALPHABET,
            NUMBER_SYMBOL,
            SPECIAL
        }

        private string[] _namePresets = new string[]{ "Lion", "Tiger", "Bear", "Wolf", "Fox", "Eagle", "Hawk", "Shark", "Dolphin", "Whale", "Panda", "Koala", "Monkey", "Giraffe", "Zebra", "Elephant", "Rhino", "Hippo", "Crocodile", "Alligator" };

        [Header("Resources")]
        [SerializeField] private TMP_InputField _inputField;
        [SerializeField] private TMP_Text _characterCountText;
        [SerializeField] private Transform _mainKeyButtonsParent;
        [SerializeField] private TMP_Text _previewText;

        [Header("Settings")]
        [SerializeField] private int _characterLimit = 12;
        public UnityEvent<string> OnNameChanged;

        private MainKeyButton[] _mainKeyButtons;

        override protected void OnAwake()
        {
            _characterCountRectTransform = _characterCountText.GetComponent<RectTransform>();
            _characterCountDefaultPosition = _characterCountRectTransform.anchoredPosition;
            _inputField.characterLimit = _characterLimit + 1; // 1文字多く設定して、制限に達したときにコールバックできるようにする
            _inputField.onValueChanged.AddListener(OnInputFieldChanged);
            _mainKeyButtons = _mainKeyButtonsParent.GetComponentsInChildren<MainKeyButton>();
            ConvertMainKey(ConvertType.HIRAGANA);
            if(string.IsNullOrEmpty(PlayerData.Instance.Nickname))
            {
                SetRandomPreset();
            }
            else
            {
                _inputField.text = PlayerData.Instance.Nickname;
            }
        }

        //UIhook
        public void DeleteLastCharacter()
        {
            if (_inputField.text.Length > 0)
            {
                _inputField.text = _inputField.text.Substring(0, _inputField.text.Length - 1);
            }
        }

        //UIhook
        public void SetEmpty()
        {
            _inputField.text = _inputField.text + " ";
        }

        //UIhook
        public void SetRandomPreset()
        {
            string randomName = _namePresets[UnityEngine.Random.Range(0, _namePresets.Length)];
            _inputField.text = randomName;
        }


        public TabManager tabmanager; //ここはシーンに依存するから、変更必須。変えろよ未来の俺
        //UIhook
        public void OnReturnButtonClick()
        {
            if(string.IsNullOrWhiteSpace(_inputField.text))
            {
                PopupUI.OnVisible("名前が空です", "名前を入力してください", PopupUI.Type.Alert);
            }
            else
            {
                tabmanager.OpenPanel("Title");
            }
        } 

        public void SetMainKey(string key)
        {
            _inputField.text = _inputField.text + key;
        }

        public void ConvertMainKey(ConvertType convertType)
        {
            if(convertType == ConvertType.SPECIAL)
            {
                if(JapaneseCharUtil.TryGetNextVariation(_inputField.text[^1], out var specialVariation))
                {
                    _inputField.text = _inputField.text.Substring(0, _inputField.text.Length - 1) + specialVariation;
                }
                return;
            }

            foreach (var mainKeyButton in _mainKeyButtons)
            {
                mainKeyButton.ConvertText(convertType);
            }
        }

        public void OnInputFieldChanged(string text)
        {
            if(text.Length < _characterLimit)
            {
                _characterCountText.color = Color.grey;
            }
            else if(text.Length > _characterLimit)
            {
                _inputField.text = text.Substring(0, _characterLimit);
                _characterCountText.color = Color.red;
                ShakeCharacterCountText();
            }
            else
            {
                _characterCountText.color = Color.red;
            }

            PlayerData.Instance.Nickname = _inputField.text;
            OnNameChanged?.Invoke(_inputField.text);
            _previewText.text = _inputField.text;
            _characterCountText.text = $"{_inputField.text.Length}/{_characterLimit}";
        }

        #region Shake
        [Header("Shake Settings")]
        [SerializeField] private float _characterCountShakeAmount = 8f;
        [SerializeField] private float _characterCountShakeDuration = 0.2f;

        private Coroutine _characterCountShakeCoroutine;
        private RectTransform _characterCountRectTransform;
        private Vector2 _characterCountDefaultPosition;
        private void ShakeCharacterCountText()
        {
            if (_characterCountShakeCoroutine != null)
            {
                StopCoroutine(_characterCountShakeCoroutine);
                _characterCountRectTransform.anchoredPosition = _characterCountDefaultPosition;
            }

            _characterCountShakeCoroutine = StartCoroutine(ShakeCharacterCountTextCoroutine());
        }

        private IEnumerator ShakeCharacterCountTextCoroutine()
        {
            float elapsedTime = 0f;

            while (elapsedTime < _characterCountShakeDuration)
            {
                elapsedTime += Time.deltaTime;
                float shakeRate = 1f - elapsedTime / _characterCountShakeDuration;
                float offsetX = Mathf.Sin(elapsedTime * 80f) * _characterCountShakeAmount * shakeRate;
                _characterCountRectTransform.anchoredPosition = _characterCountDefaultPosition + Vector2.right * offsetX;
                yield return null;
            }

            _characterCountRectTransform.anchoredPosition = _characterCountDefaultPosition;
            _characterCountShakeCoroutine = null;
        }
        #endregion
    }
}
