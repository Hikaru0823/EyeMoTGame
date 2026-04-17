using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using EyeMoT.Fusion;
using Fusion.Addons.Physics;
using KanKikuchi.AudioManager;
using UnityEngine;

#nullable enable

namespace EyeMoT.Baloon
{
    public class BalloonSpawnManager : SceneSingleton<BalloonSpawnManager>
    {
        #region Singleton

        protected override void OnAwake()
        {
            _vfxHolder.init();
        }
        #endregion

        [Header("Resources")]
        [SerializeField] private Balloon _balloonPrefab;
        [SerializeField] private GameObject _destroyEffectPrefab;
        [SerializeField] private VFXHolder _vfxHolder;

        [Header("Settings")]
        [SerializeField] private int _maxBalloons = 10;
        [SerializeField] private float _balloonSpeed = 2f;
        [SerializeField] private float _offsetFromVolumeEdge = 1.1f;

        private readonly List<Balloon> _activeBalloons = new List<Balloon>();
        public GameObject SpawnVolume;
        public int BalloonCount => _activeBalloons.Count;
        public Action OnBalloonDestroyed;

        public void SpawnInitialBalloons(GenerationPatern patern)
        {
            _maxBalloons = SettingManager.Instance.GameData.BalloonAmount;
            for(int i = 0; i < _maxBalloons; i++)
                SpawnBalloonPatern(patern);
        }

        public Balloon SpawnPreviewBalloon(Vector3 spawnPosition, Vector3 spawnRotation, bool randomColor = false)
        {
            Balloon newBalloon = LobbyManager.Instance.Runner.Spawn(_balloonPrefab, spawnPosition, Quaternion.Euler(spawnRotation), onBeforeSpawned: (runner, obj) => {
                        obj.GetComponent<Balloon>().NetworkedColor = randomColor ? new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value) : Color.red;
                        obj.GetComponent<Balloon>().EffectEnable = false;
                    });
            return newBalloon;
        }

        public void ResetBalloons()
        {
            foreach(var balloon in _activeBalloons)
            {
                LobbyManager.Instance.Runner.Despawn(balloon.Object);
            }
            _activeBalloons.Clear();
        }

        private void SpawnBalloonPatern(GenerationPatern patern)
        {
            BalloonSpawnData spawnData = GetBalloonSpawnData();
            var randomRotate = Quaternion.Euler(0, 0, UnityEngine.Random.Range(-90f, 90f));
            var randomColor = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
            switch(patern)
            {
                case GenerationPatern.Float:
                    Balloon newBalloon = LobbyManager.Instance.Runner.Spawn(_balloonPrefab, spawnData.Position, randomRotate, onBeforeSpawned: (runner, obj) => {
                        obj.GetComponent<Balloon>().NetworkedColor = randomColor;
                    });
                    newBalloon.StartMove(spawnData.MoveTargetDirection, _balloonSpeed);
                    _activeBalloons.Add(newBalloon);
                    break;
                case GenerationPatern.Fix:
                    Balloon newBalloon_fix = LobbyManager.Instance.Runner.Spawn(_balloonPrefab, GetRandomPositionWithinVolume(SpawnVolume), randomRotate, onBeforeSpawned: (runner, obj) => {
                        obj.GetComponent<Balloon>().NetworkedColor = randomColor;
                    });
                    newBalloon_fix.GetComponent<NetworkRigidbody3D>().RBIsKinematic = true;
                    _activeBalloons.Add(newBalloon_fix);
                    break;
            }
        }


        private Vector3 GetRandomPositionWithinVolume(GameObject volume)
        {
            Vector3 volumePosition = volume.transform.position;
            Vector3 volumeScale = volume.transform.localScale;

            return new Vector3(
                UnityEngine.Random.Range(volumePosition.x - volumeScale.x / 2f, volumePosition.x + volumeScale.x / 2f),
                UnityEngine.Random.Range(volumePosition.y - volumeScale.y / 2f, volumePosition.y + volumeScale.y / 2f),
                UnityEngine.Random.Range(volumePosition.z - volumeScale.z / 2f, volumePosition.z + volumeScale.z / 2f)
            );
        }

        private BalloonSpawnData GetBalloonSpawnData(Side? spawnSide = null)
        {
            if (!spawnSide.HasValue)
                spawnSide = (Side)UnityEngine.Random.Range(0, 4);

            Vector3 spawnPosition = GetPositionWithinVolume(spawnSide.Value, SpawnVolume);
            Side randomTargetSide = GetRandomSideExcept(spawnSide.Value);
            Side diagonalSide = GetDiagonalSide(randomTargetSide, spawnSide.Value, spawnPosition);
            TargetSideInfo targetSideInfo = new TargetSideInfo(diagonalSide, _offsetFromVolumeEdge);
            Vector3 targetPosition = GetPositionWithinVolume(randomTargetSide, SpawnVolume, targetSideInfo);
            Vector3 moveTargetDirection = (targetPosition - spawnPosition).normalized;

            return new BalloonSpawnData(spawnPosition, moveTargetDirection);
        }

        private Side GetRandomSideExcept(Side excludeSide)
        {
            Side[] candidateSides = new Side[4];
            int index = 0;

            foreach (Side side in Enum.GetValues(typeof(Side)))
            {
                if (side != excludeSide)
                    candidateSides[index++] = side;
            }

            return candidateSides[UnityEngine.Random.Range(0, candidateSides.Length)];
        }

        private Side GetDiagonalSide(Side targetSide, Side spawnSide, Vector3 spawnPosition)
        {
            Side diagonalSide = Side.Left;
            Side opposedSide = Side.Left;
            switch (spawnSide)
            {
                case Side.Left:
                case Side.Right:
                    opposedSide = spawnPosition.y > SpawnVolume.transform.position.y ? Side.Up : Side.Down;
                    if((int)targetSide % 2 == 1)
                    {
                        diagonalSide = (Side)(((int)spawnSide + 2) % 4);
                        break;
                    } 
                    diagonalSide = opposedSide == Side.Up ? Side.Down : Side.Up;
                    break;
                case Side.Up:
                case Side.Down:
                    opposedSide = spawnPosition.x > SpawnVolume.transform.position.x ? Side.Right : Side.Left;
                    if((int)targetSide % 2 == 0)
                    {
                        diagonalSide = (Side)(((int)spawnSide + 2) % 4);
                        break;
                    }
                    diagonalSide = opposedSide == Side.Left ? Side.Right : Side.Left;
                    break;
            }

            //Debug.Log($"Spawned {spawnSide}{opposedSide}, Target {targetSide}{diagonalSide} ");
            return diagonalSide;
        }

        private Vector3 GetPositionWithinVolume(Side targetSide, GameObject volume, TargetSideInfo? targetSideInfo = null)
        {
            Vector3 result = Vector3.zero;
            Vector3 volumePosition = volume.transform.position;
            Vector3 volumeScale = targetSideInfo == null ? volume.transform.localScale : targetSideInfo.Offset * volume.transform.localScale; // Assuming the volume is centered and scale represents the full size

            switch (targetSide)
            {
                case Side.Left:
                case Side.Right:
                    volumePosition.x = targetSide == Side.Left ? volumePosition.x - volumeScale.x / 2f : volumePosition.x + volumeScale.x / 2f;
                    float y = 0f;
                    if(targetSideInfo == null)
                        y = UnityEngine.Random.Range(-volumeScale.y / 2f, volumeScale.y / 2f);
                    else
                        y = targetSideInfo.DiagonalSide == Side.Up ? UnityEngine.Random.Range(0, volumeScale.y / 2f) : UnityEngine.Random.Range(-volumeScale.y / 2f, 0);
                    result = volumePosition + new Vector3(0, y, 0);
                    break;
                case Side.Up:
                case Side.Down:
                    volumePosition.y = targetSide == Side.Up ? volumePosition.y + volumeScale.y / 2f : volumePosition.y - volumeScale.y / 2f;
                    float x = UnityEngine.Random.Range(-volumeScale.x / 2f, volumeScale.x / 2f);
                    if(targetSideInfo == null)
                        x = UnityEngine.Random.Range(-volumeScale.x / 2f, volumeScale.x / 2f);
                    else
                        x = targetSideInfo.DiagonalSide == Side.Right ? UnityEngine.Random.Range(0, volumeScale.x / 2f) : UnityEngine.Random.Range(-volumeScale.x / 2f, 0);
                    result = volumePosition + new Vector3(x, 0, 0);
                    break;
            }

            return result;
        }

        public void DestroyBalloon(Balloon balloon)
        {
            if (!balloon.Object.HasStateAuthority) return;

            _activeBalloons.Remove(balloon);
            LobbyManager.Instance.Runner.Despawn(balloon.Object);

            if (_activeBalloons.Count > 0)
            {
                SpawnBalloonPatern(SettingManager.Instance.GameData.BalloonGeneratePatern);
            }
        }

        public void PlayDestroyEffects(Vector3 pos)
        {
            if (!_vfxHolder.TryGet(SettingManager.Instance.BalloonData.VFXIdx, out var effect))
                return;

            Instantiate(effect.Object, pos, effect.Object.transform.rotation);
            SEManager.Instance.Play(effect.CurrentSEPath);
            OnBalloonDestroyed?.Invoke();
        }

        public void DeleteBalloon(Balloon balloon)
        {
            if(!LobbyManager.Instance.Runner.IsServer) return;
            _activeBalloons.Remove(balloon);
            LobbyManager.Instance.Runner.Despawn(balloon.Object);
            SpawnBalloonPatern(SettingManager.Instance.GameData.BalloonGeneratePatern);
        }

        private enum Side
        {
            Left, Up, Right, Down
        }

        public enum GenerationPatern
        {
            Float, Fix,
        }

        private class BalloonSpawnData
        {
            public Vector3 Position { get; }
            public Vector3 MoveTargetDirection { get; }

            public BalloonSpawnData(Vector3 position, Vector3 moveTargetDirection)
            {
                Position = position;
                MoveTargetDirection = moveTargetDirection;
            }
        }

        private class TargetSideInfo
        {
            public Side DiagonalSide { get; }
            public float Offset { get; }

            public TargetSideInfo(Side diagonalSide, float offset)
            {
                DiagonalSide = diagonalSide;
                Offset = offset;
            }
        }
    }
}
