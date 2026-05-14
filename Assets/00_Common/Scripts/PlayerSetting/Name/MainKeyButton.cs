using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace EyeMoT
{
    public class MainKeyButton : MonoBehaviour
    {
        [Header("Resources")]
        [SerializeField] private TMP_Text _text;
        [SerializeField] private GazeController _gazeController;

        [Header("Settings")]
        [SerializeField] private string _HIRAGANA;
        [SerializeField] private string _KATAKANA;
        [SerializeField] private string _ALPHABET;
        [SerializeField] private string _NUMBER_SYMBOL;

        public void OnClick()
        {
            NameEditor.Instance.SetMainKey(_text.text);
        }

        public void ConvertText(NameEditor.ConvertType convertType)
        {
            switch (convertType)
            {
                case NameEditor.ConvertType.HIRAGANA:
                    _gazeController.Enable = !string.IsNullOrEmpty(_HIRAGANA);
                    _text.text = _HIRAGANA;
                    break;
                case NameEditor.ConvertType.KATAKANA:
                    _gazeController.Enable = !string.IsNullOrEmpty(_KATAKANA);
                    _text.text = _KATAKANA;
                    break;
                case NameEditor.ConvertType.UPPER_ALPHABET:
                    _gazeController.Enable = !string.IsNullOrEmpty(_ALPHABET);
                    _text.text = _ALPHABET.ToUpper();
                    break;
                case NameEditor.ConvertType.LOWER_ALPHABET:
                    _gazeController.Enable = !string.IsNullOrEmpty(_ALPHABET);
                    _text.text = _ALPHABET.ToLower();
                    break;
                case NameEditor.ConvertType.NUMBER_SYMBOL:
                    _gazeController.Enable = !string.IsNullOrEmpty(_NUMBER_SYMBOL);
                    _text.text = _NUMBER_SYMBOL;
                    break;
            }
        }
    }
}