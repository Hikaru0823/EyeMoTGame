using System;
using UnityEngine;

public class SelecterUI : MonoBehaviour
{
    public virtual void Initialize(int idx) { }
    public virtual string[] GetItems() { return null; }
}