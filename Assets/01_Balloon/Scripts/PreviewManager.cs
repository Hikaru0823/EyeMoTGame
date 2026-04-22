using System;
using System.Collections;
using System.Collections.Generic;
using EyeMoT.Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EyeMoT.Balloon
{
    public class PreviewManager : SceneSingleton<PreviewManager>
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


        private void SpawnPreviewBalloon()
        {
            var offset = Vector3.forward * 2.45f + Vector3.up * 0.3f;
            _previewBalloon = BalloonSpawnManager.Instance.SpawnPreviewBalloon(transform.position + offset, Vector3.zero);
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

            if(_previewBalloon != null)
                _previewBalloon.SetEffectEnable(false);
            ResetBalloon();
            if(newItem == "Interaction")
            {
                _balloonDestroy = StartCoroutine(BalloonDestroyRoutine(0.5f));
            }
        }

        private IEnumerator BalloonDestroyRoutine(float _lifeTime)
        {
            while(true)
            {
                _previewBalloon.StartBalloonDestroy(_lifeTime);
                yield return new WaitForSeconds(_lifeTime + 1.0f);
                SpawnPreviewBalloon();
            }
        }

        public void UpdateBalloon()
        {
            if(_previewBalloon == null) return;
            _previewBalloon.UpdateData();
        }

        public void ResetBalloon(string newItem = "")
        {
            if(_balloonDestroy != null)
                StopCoroutine(_balloonDestroy);

            if(_previewBalloon != null && _previewBalloon.HasStateAuthority)
                LobbyManager.Instance.Runner.Despawn(_previewBalloon.Object);
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
                    break;
                case BGColor.White:
                    _thisCamera.clearFlags = CameraClearFlags.SolidColor;
                    _thisCamera.backgroundColor = Color.white;
                    _thisCamera.cullingMask &= ~(1 << stageLayer);
                    break;
                case BGColor.Black:
                    _thisCamera.clearFlags = CameraClearFlags.SolidColor;
                    _thisCamera.backgroundColor = Color.black;
                    _thisCamera.cullingMask &= ~(1 << stageLayer);
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
}