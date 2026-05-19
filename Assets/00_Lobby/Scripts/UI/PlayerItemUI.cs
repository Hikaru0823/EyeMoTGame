using System.Collections;
using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerItemUI : MonoBehaviour
{
    [Header("Resources")]
    [SerializeField] private Image _background;
    [SerializeField] private RawImage _image;
    [SerializeField] private TextMeshProUGUI _nicknameText;
    [SerializeField] private GameObject _readyIcon;
    [HideInInspector] public PlayerRef Ref;
    
    public void Init(PlayerRef playerRef, string nickname, Color color, Texture2D characterIcon = null)
    {
        Ref = playerRef;
        _nicknameText.text = nickname;
        _background.color = color;
        if(characterIcon != null)
            _image.texture = characterIcon;
    }

    public void SetReady(bool isReady)
    {
        _readyIcon.SetActive(isReady);
    }
}
