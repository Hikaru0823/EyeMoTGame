using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace EyeMoT
{
    public class TabManager : MonoBehaviour
    {
        [Header("Panel List")]
        public List<PanelItem> panels = new List<PanelItem>();

        [Header("Settings")]
        public int currentPanelIndex = 0;
        private int newPanelIndex;

        private GameObject currentPanel;
        private GameObject nextPanel;

        private Animator currentPanelAnimator;
        private Animator nextPanelAnimator;

        string panelFadeIn => StaticData.PANEL_FADE_IN;
        string panelFadeOut => StaticData.PANEL_FADE_OUT;

        [System.Serializable]
        public class PanelItem
        {
            public string panelName;
            public GameObject panelObject;
        }

        void OnEnable()
        {

            if(panels[currentPanelIndex].panelObject != null && panels[newPanelIndex].panelObject != null)
            {   
                currentPanel = panels[currentPanelIndex].panelObject;
                currentPanelAnimator = currentPanel.GetComponent<Animator>();
                currentPanelAnimator.Play(panelFadeIn);
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

                StartCoroutine("DisablePreviousPanel");
            }
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