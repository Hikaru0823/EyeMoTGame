using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EyeMoT
{
    public class ReturnMainTitleButton : MonoBehaviour
    {
        public void ReturnMainTitle()
        {
            SceneManager.LoadScene("00_Title");
        }
    }
}