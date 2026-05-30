using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

namespace EyeMoT
{
    public class Loading : Singleton<Loading>
    {
        [Header("Resources")]
        [SerializeField] private GameObject _panel;

        [Header("Settings")]
        [SerializeField] private float _timeout = 10f;

        Coroutine _timeoutCoroutine;

        public void SetVisible(bool isVisible, System.Action onTimeout = null)
        {
            _panel.SetActive(isVisible);
            if (_timeoutCoroutine != null)
                StopCoroutine(_timeoutCoroutine);
                
            if (isVisible)
            {
                _timeoutCoroutine = StartCoroutine(TimeoutCallback(onTimeout));
            }
        }

        private IEnumerator TimeoutCallback(System.Action onTimeout = null)
        {
            yield return new WaitForSeconds(_timeout);
            onTimeout?.Invoke();
        }


    }
}