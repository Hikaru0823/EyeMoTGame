using System.Collections;
using System.Collections.Generic;
using KanKikuchi.AudioManager;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EyeMoT
{
    public class TitleManager : SceneSingleton<TitleManager>
    {
        void Start()
        {
            BGMManager.Instance.Play(BGMPath.TITLE, volumeRate: 0.5f);
        }

        public void StartGame(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}