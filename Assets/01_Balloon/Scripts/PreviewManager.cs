using System;
using System.Collections;
using System.Collections.Generic;
using EyeMoT.Baloon;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PreviewManager : MonoBehaviour
{
    [Header("Resources")]
    [SerializeField] private List<PreviewItem> _items = new();
    [SerializeField] private GameObject _previewImage;
    [SerializeField] private TMP_Text _description; 
    [SerializeField] private Camera _thisCamera;

    [Header("Settings")]
    [SerializeField] int _layerIdx = 6;

    private int _newItemIndex;
    private Balloon _previewBalloon;
    private Coroutine _balloonDestroy;


    void Start()
    {
        SpawnPreviewBalloon();
        // UpdateBalloon();
        // UpdateBackGround();
    }

    private void SpawnPreviewBalloon()
    {
        var offset = Vector3.forward * 2.45f + Vector3.up * 0.3f;
        _previewBalloon = BalloonSpawner.Instance.SpawnPreviewBalloon(transform.position + offset, Vector3.zero);
        _previewBalloon.VisibleCollision(true);
        foreach(Transform obj in _previewBalloon.transform)
        {
            obj.gameObject.layer = _layerIdx;
        }
    }

    public void ShowItem(string newItem)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i].ItemName == newItem)
            {
                //if(_newItemIndex == i) return;
                _newItemIndex = i;
                break;
            }
        }

        _previewImage.gameObject.SetActive(_items[_newItemIndex].ActivePreviewCamera);
        _description.text = _items[_newItemIndex].Description;

        ResetBalloon();
        switch(newItem)
        {
            case "Interaction":
                _balloonDestroy = StartCoroutine(BalloonDestroyRoutine(0.3f));
                break;
        }
    }

    private IEnumerator BalloonDestroyRoutine(float _lifeTime)
    {
        while(true)
        {
            _previewBalloon.StartBalloonDestroy(_lifeTime);
            yield return new WaitForSeconds(_lifeTime + 1.5f);
            SpawnPreviewBalloon();
        }
    }

    public void UpdateBalloon()
    {
        if(_previewBalloon == null) return;
        _previewBalloon.UpdateData();
    }

    private void ResetBalloon()
    {
        if(_balloonDestroy != null)
            StopCoroutine(_balloonDestroy);

        if(_previewBalloon != null)
            Destroy(_previewBalloon.gameObject);
        SpawnPreviewBalloon();
    }

    public void OnDisabled()
    {
        ResetBalloon();
    }

    public void OnEnabled()
    {
        ShowItem(_items[_newItemIndex].ItemName);
    }

    public void UpdateBackGround()
    {
        int stageLayer = LayerMask.NameToLayer("Stage");

        switch(SettingManager.Instance.GameData.BGColor)
        {
            case BGColor.Default:
                _thisCamera.clearFlags = CameraClearFlags.Skybox;
                _thisCamera.cullingMask |= (1 << stageLayer);
                Camera.main.clearFlags = CameraClearFlags.Skybox;
                Camera.main.cullingMask |= (1 << stageLayer);
                break;
            case BGColor.White:
                _thisCamera.clearFlags = CameraClearFlags.SolidColor;
                _thisCamera.backgroundColor = Color.white;
                _thisCamera.cullingMask &= ~(1 << stageLayer);
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = Color.white;
                Camera.main.cullingMask &= ~(1 << stageLayer);
                break;
            case BGColor.Black:
                _thisCamera.clearFlags = CameraClearFlags.SolidColor;
                _thisCamera.backgroundColor = Color.black;
                _thisCamera.cullingMask &= ~(1 << stageLayer);
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = Color.black;
                Camera.main.cullingMask &= ~(1 << stageLayer);
                break;
        }
    }

    public enum BGColor
    {
        Default, White, Black
    }

    [System.Serializable]
    public class PreviewItem
    {
        public string ItemName;
        [TextArea(3, 10)]public string Description;
        public bool ActivePreviewCamera;
    } 
}
