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
    [SerializeField] private GameObject _progressBar;
    [SerializeField] private Image _progressBarImage;
    [HideInInspector] public PlayerRef Ref;
    
    
    public void Init(PlayerRef playerRef, string nickname, Color color, bool visibleBar = false)
    {
        Ref = playerRef;
        _nicknameText.text = nickname;
        _background.color = color;
        if(!visibleBar) return;
        _progressBar.SetActive(visibleBar);
        _progressBarImage.fillAmount = 0f;
    }

    public void SetImage(Sprite sprite)
    {
        _image.sprite = sprite;
        _progressBar?.SetActive(false);
    }

    public void SetReady(bool isReady)
    {
        _readyIcon.SetActive(isReady);
    }

    public void SetProgress(float progress)
    {
        _progressBarImage.fillAmount = progress;
    }
}
