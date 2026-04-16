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
    [HideInInspector] public PlayerRef Ref;
    
    public void Init(PlayerRef playerRef, string nickname, Texture2D characterIcon = null)
    {
        Ref = playerRef;
        _nicknameText.text = nickname;
        if(characterIcon != null)
            _image.texture = characterIcon;
    }

    public void SetReady(bool isReady)
    {
        _background.color = isReady ? Color.green : Color.white;
    }
}
