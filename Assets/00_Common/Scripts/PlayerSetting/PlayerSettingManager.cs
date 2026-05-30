using System;
using System.Collections;
using KanKikuchi.AudioManager;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EyeMoT
{
    public class PlayerSettingManager : Singleton<PlayerSettingManager>
    {
        [Header("Resources")]
        [SerializeField] private Animator _animator;
        [SerializeField] private GameObject _bgpanel;
        public NameEditor NameEditor;
        public PlayerImageEditor PlayerImageManager;
        public string CurrentName => NameEditor.CurrentName;
        public Sprite CurrentPlayerImage => PlayerImageManager.CurrentSprite;
        
        string panelFadeIn => StaticData.PANEL_FADE_IN;
        string panelFadeOut => StaticData.PANEL_FADE_OUT;
        private Action _onInvisible;

        public void Visible(Action onInvisible = null)
        {
            PlayerData.Instance.CanUseShortCut = false;
            _bgpanel.SetActive(true);
            StopCoroutine("DisablePreviousPanel");
            _animator.Play(panelFadeIn);
            if(onInvisible != null)
                _onInvisible = onInvisible;
        }

        public void Invisible()
        {
            StopCoroutine("DisablePreviousPanel");
            _bgpanel.SetActive(false);
            _animator.Play(panelFadeOut);
            if(!_animator.gameObject.activeSelf)
                StartCoroutine("DisablePreviousPanel");
            PlayerData.Instance.CanUseShortCut = true;
            _onInvisible?.Invoke();
        }

        IEnumerator DisablePreviousPanel()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            _animator.gameObject.SetActive(false);
        }
    }
}