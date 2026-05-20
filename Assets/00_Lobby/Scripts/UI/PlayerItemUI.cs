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
    [SerializeField] private Image _image;
    [SerializeField] private TextMeshProUGUI _nicknameText;
    [SerializeField] private GameObject _readyIcon;
    [HideInInspector] public PlayerRef Ref;
    
    public void Init(PlayerRef playerRef, string nickname, Color color)
    {
        Ref = playerRef;
        _nicknameText.text = nickname;
        _background.color = color;
    }

    public void SetImage(Sprite sprite)
    {
        _image.sprite = sprite;
    }

    public void SetReady(bool isReady)
    {
        _readyIcon.SetActive(isReady);
    }
}
