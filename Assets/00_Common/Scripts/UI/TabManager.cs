using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.VisualScripting;

namespace EyeMoT
{
    public class TabManager : MonoBehaviour
    {
        [Header("Panel List")]
        public List<PanelItem> panels = new List<PanelItem>();

        [Header("Settings")]
        public int currentPanelIndex = 0;
        private int newPanelIndex;
        public int currentButtonIndex = 0;

        private GameObject currentPanel;
        private GameObject nextPanel;

        private Animator currentPanelAnimator;
        private Animator nextPanelAnimator;

        private Animator currentButtonAnimator;
        private Animator nextButtonAnimator;

        string panelFadeIn => StaticData.PANEL_FADE_IN;
        string panelFadeOut => StaticData.PANEL_FADE_OUT;
        string buttonPressed => StaticData.BUTTON_NORMAL_TO_PRESSED;
        string buttonNormal => StaticData.BUTTON_PRESSED_TO_NORMAL;

        [System.Serializable]
        public class PanelItem
        {
            public string panelName;
            public GameObject panelObject;
            public Button buttonObject;
        }

        void OnEnable()
        {
            
            if(panels[currentPanelIndex].panelObject != null && panels[newPanelIndex].panelObject != null)
            {   
                currentPanel = panels[currentPanelIndex].panelObject;
                currentPanelAnimator = currentPanel.GetComponent<Animator>();
                currentPanelAnimator.Play(panelFadeIn);
            }

            if(panels[currentPanelIndex].buttonObject != null && panels[newPanelIndex].buttonObject != null)
            {
                currentButtonAnimator = panels[currentPanelIndex].buttonObject.GetComponent<Animator>();
                currentButtonAnimator.Play(buttonPressed);
            }

            StartCoroutine("DisablePreviousPanel");
        }

        public void OpenPanel(string newPanel)
        {
            for (int i = 0; i < panels.Count; i++)
            {
                if (panels[i].panelName == newPanel)
                {
                    newPanelIndex = i;
                    break;
                }
            }

            if (newPanelIndex != currentPanelIndex)
            {
                StopCoroutine("DisablePreviousPanel");

                //パネルの管理
                if(panels[currentPanelIndex].panelObject != null && panels[newPanelIndex].panelObject != null)
                {
                
                    //移動前パネルと移動後パネルを取得
                    currentPanel = panels[currentPanelIndex].panelObject;
                    currentPanelIndex = newPanelIndex;
                    nextPanel = panels[currentPanelIndex].panelObject;
                    nextPanel.SetActive(true);

                    //パネルのアニメーション管理
                    currentPanelAnimator = currentPanel.GetComponent<Animator>();
                    nextPanelAnimator = nextPanel.GetComponent<Animator>();
                    currentPanelAnimator.Play(panelFadeOut);
                    nextPanelAnimator.Play(panelFadeIn);
                }

                //ボタンの管理
                if(panels[currentPanelIndex].buttonObject != null && panels[newPanelIndex].buttonObject != null)
                {
                    //移動前ボタンと移動後ボタンを取得
                    currentButtonAnimator = panels[currentButtonIndex].buttonObject.GetComponent<Animator>();
                    currentButtonIndex = newPanelIndex;
                    nextButtonAnimator = panels[currentButtonIndex].buttonObject.GetComponent<Animator>();

                    //ボタンのアニメーション管理
                    currentButtonAnimator.Play(buttonNormal);
                    nextButtonAnimator.Play(buttonPressed);
                }

                StartCoroutine("DisablePreviousPanel");
            }
        }

        public string GetCurrentPanelName()
        {
            return panels[currentPanelIndex].panelName;
        }

        IEnumerator DisablePreviousPanel()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            for (int i = 0; i < panels.Count; i++)
            {
                if (i == currentPanelIndex)
                    continue;

                panels[i].panelObject.gameObject.SetActive(false);
            }
        }
    }
}