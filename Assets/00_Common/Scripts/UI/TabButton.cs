using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

[ExecuteInEditMode]
public class TabButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Resources")]
    public Animator buttonAnimator;

    void OnEnable()
    {
        if (buttonAnimator == null)
                buttonAnimator = gameObject.GetComponent<Animator>();
    }


    public void OnPointerEnter(PointerEventData eventData)
    {
#if !UNITY_ANDROID && !UNITY_IOS
        if (!buttonAnimator.GetCurrentAnimatorStateInfo(0).IsName("Normal to Pressed"))
            buttonAnimator.Play("Dissolve to Normal");
#endif
    }

    public void OnPointerExit(PointerEventData eventData)
    {
#if !UNITY_ANDROID && !UNITY_IOS
        if (!buttonAnimator.GetCurrentAnimatorStateInfo(0).IsName("Normal to Pressed"))
            buttonAnimator.Play("Normal to Dissolve");
#endif
    }
}
