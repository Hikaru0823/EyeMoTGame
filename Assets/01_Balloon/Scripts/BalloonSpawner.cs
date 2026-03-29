using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using KanKikuchi.AudioManager;
using UnityEngine;

#nullable enable

namespace EyeMoT.Baloon
{
    public class BalloonSpawner : MonoBehaviour
    {
        [Header("Resources")]
        [SerializeField] private Balloon _balloonPrefab;
        [SerializeField] private GameObject _spawnVolume;
        [SerializeField] private GameObject _destroyEffectPrefab;

        [Header("Settings")]
        [SerializeField] private int _maxBalloons = 10;
        [SerializeField] private float _balloonSpeed = 2f;
        [SerializeField] private float _offsetFromVolumeEdge = 1.1f;

        private readonly List<Balloon> _activeBalloons = new List<Balloon>();

        void Start()
        {
            BGMManager.Instance.Play(BGMPath.BALLOON_GAME, volumeRate: 0.5f);
            _spawnVolume.GetComponent<BalloonVolume>().onBalloonExited += DestroyBalloon;
            SpawnInitialBalloons();
        }

        private void SpawnInitialBalloons()
        {
            for (int i = 0; i < _maxBalloons; i++)
                SpawnBalloon();
        }

        private void SpawnBalloon()
        {
            BalloonSpawnData spawnData = GetBalloonSpawnData();
            var randomRotate = Quaternion.Euler(0, 0, UnityEngine.Random.Range(-90f, 90f));
            var randomColor = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
            Balloon newBalloon = Instantiate(_balloonPrefab, spawnData.Position, randomRotate);
            newBalloon.StartMove(spawnData.MoveTargetDirection, _balloonSpeed);
            newBalloon.Initialize(randomColor, DestroyBalloon);
            _activeBalloons.Add(newBalloon);
        }

        private BalloonSpawnData GetBalloonSpawnData(Side? spawnSide = null)
        {
            if (!spawnSide.HasValue)
                spawnSide = (Side)UnityEngine.Random.Range(0, 4);

            Vector3 spawnPosition = GetPositionWithinVolume(spawnSide.Value, _spawnVolume);
            Side randomTargetSide = GetRandomSideExcept(spawnSide.Value);
            Side diagonalSide = GetDiagonalSide(randomTargetSide, spawnSide.Value, spawnPosition);
            TargetSideInfo targetSideInfo = new TargetSideInfo(diagonalSide, _offsetFromVolumeEdge);
            Vector3 targetPosition = GetPositionWithinVolume(randomTargetSide, _spawnVolume, targetSideInfo);
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
                    opposedSide = spawnPosition.y > _spawnVolume.transform.position.y ? Side.Up : Side.Down;
                    if((int)targetSide % 2 == 1)
                    {
                        diagonalSide = (Side)(((int)spawnSide + 2) % 4);
                        break;
                    } 
                    diagonalSide = opposedSide == Side.Up ? Side.Down : Side.Up;
                    break;
                case Side.Up:
                case Side.Down:
                    opposedSide = spawnPosition.x > _spawnVolume.transform.position.x ? Side.Right : Side.Left;
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

        private void DestroyBalloon(Balloon balloon)
        {
            Instantiate(_destroyEffectPrefab, balloon.transform.position, Quaternion.identity);
            _activeBalloons.Remove(balloon);
            Destroy(balloon.gameObject);
            SpawnBalloon();
        }

        private enum Side
        {
            Left,
            Up,
            Right,
            Down
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
