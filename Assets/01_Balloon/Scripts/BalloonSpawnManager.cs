using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using EyeMoT.Fusion;
using Fusion;
using Fusion.Addons.Physics;
using KanKikuchi.AudioManager;
using UnityEngine;

#nullable enable

namespace EyeMoT.Balloon
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
        [SerializeField] private GameObject _spawnVolume;

        [Header("Settings")]
        [SerializeField] private float _balloonSpeed = 2f;
        [SerializeField] private float _offsetFromVolumeEdge = 1.1f;
        [SerializeField, Min(0f)] private float _spawnInsetFromVolumeEdge = 0.8f;
        [SerializeField] private bool _visibleCollision = false;

        public List<Color> balloonColorHistory = new List<Color>();
        public readonly List<Balloon> ActiveBalloons = new List<Balloon>();
        public int BalloonCount => ActiveBalloons.Count;
        public Action OnBalloonDestroyed;
        private BalloonSpawnManager.GenerationPatern _currentPatern;
        private int _maxBalloons;

        public bool TryGetFirstBalloonScreenPosition(out Vector2 screenPosition)
        {
            screenPosition = Vector2.zero;

            if (ActiveBalloons.Count == 0)
                return false;

            Balloon balloon = ActiveBalloons[0];
            if (balloon == null)
                return false;

            var targetCamera = Camera.main;

            if (targetCamera == null)
                return false;

            Vector3 screenPoint = targetCamera.WorldToScreenPoint(balloon.transform.position);
            if (screenPoint.z <= 0f)
                return false;

            screenPosition = new Vector2(screenPoint.x, screenPoint.y);
            return true;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.D) && PlayerData.Instance.CanUseShortCut)
            {
                _visibleCollision = !_visibleCollision;
                foreach(var balloon in ActiveBalloons)
                {
                    balloon.VisibleCollision(_visibleCollision);
                }
            }
        }

        void LateUpdate()
        {
            if (GameManager.Instance.IsStart && ActiveBalloons.Count < _maxBalloons)
            {
                SpawnBalloonPatern(_currentPatern);
            }
        }

        public void SpawnInitialBalloons(GenerationPatern patern, int maxBalloons)
        {
            _currentPatern = patern;
            _maxBalloons = maxBalloons;
            for(int i = 0; i < _maxBalloons; i++)
                SpawnBalloonPatern(patern);
        }

        public Balloon SpawnPreviewBalloon(Vector3 spawnPosition, Vector3 spawnRotation, bool randomColor = false)
        {
            Balloon newBalloon = LobbyManager.Instance.Runner.Spawn(_balloonPrefab, spawnPosition, Quaternion.Euler(spawnRotation), onBeforeSpawned: (runner, obj) => {
                        obj.GetComponent<Balloon>().NetworkedColor = randomColor ? new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value) : Color.red;
                    });
            return newBalloon;
        }

        public void ResetBalloons()
        {
            foreach(var balloon in ActiveBalloons)
            {
                LobbyManager.Instance.Runner.Despawn(balloon.Object);
            }
            ActiveBalloons.Clear();
            balloonColorHistory.Clear();
        }

        private void SpawnBalloonPatern(GenerationPatern patern)
        {
            if(!GameManager.Instance.IsStart) return;
            BalloonSpawnData spawnData = GetBalloonSpawnData();
            var randomRotate = Quaternion.Euler(0, 0, UnityEngine.Random.Range(-90f, 90f));
            var randomColor = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
            switch(patern)
            {
                case GenerationPatern.Float:
                    Balloon newBalloon = LobbyManager.Instance.Runner.Spawn(_balloonPrefab, spawnData.Position, randomRotate, onBeforeSpawned: (runner, obj) => {
                        obj.GetComponent<Balloon>().NetworkedColor = randomColor;
                    });
                    newBalloon.StartMove(spawnData.MoveTargetDirection, GameManager.Instance.IsAnalyze ? 0.2f : _balloonSpeed);
                    newBalloon.VisibleCollision(_visibleCollision);
                    ActiveBalloons.Add(newBalloon);
                    balloonColorHistory.Add(randomColor);
                    break;
                case GenerationPatern.Fix:
                    Balloon newBalloon_fix = LobbyManager.Instance.Runner.Spawn(_balloonPrefab, GetRandomPositionWithinVolume(_spawnVolume), randomRotate, onBeforeSpawned: (runner, obj) => {
                        obj.GetComponent<Balloon>().NetworkedColor = randomColor;
                        obj.GetComponent<Balloon>().EnableSpawnAnimation = true;
                    });
                    newBalloon_fix.GetComponent<NetworkRigidbody3D>().RBIsKinematic = true;
                    newBalloon_fix.VisibleCollision(_visibleCollision);
                    ActiveBalloons.Add(newBalloon_fix);
                    balloonColorHistory.Add(randomColor);
                    break;
            }
        }


        private Vector3 GetRandomPositionWithinVolume(GameObject volume)
        {
            Bounds bounds = GetVolumeBounds(volume);

            return new Vector3(
                GetRandomValueInside(bounds.min.x/1.5f, bounds.max.x/1.5f, _spawnInsetFromVolumeEdge),
                GetRandomValueInside(bounds.min.y/1.2f, bounds.max.y/1.2f, _spawnInsetFromVolumeEdge),
                bounds.center.z
            );
        }

        private BalloonSpawnData GetBalloonSpawnData(Side? spawnSide = null)
        {
            if (!spawnSide.HasValue)
                spawnSide = (Side)UnityEngine.Random.Range(0, 4);

            Bounds volumeBounds = GetVolumeBounds(_spawnVolume);
            float spawnRangeRatio = GameManager.Instance.IsAnalyze ? 4f / 5f : 1f;
            Vector3 spawnPosition = GetSpawnPosition(
                spawnSide.Value,
                volumeBounds,
                spawnRangeRatio
            );
            Side randomTargetSide = GetRandomSideExcept(spawnSide.Value);
            Side diagonalSide = GetDiagonalSide(
                randomTargetSide,
                spawnSide.Value,
                spawnPosition,
                volumeBounds.center
            );
            Vector3 targetPosition = GetTargetPositionOutsideVolume(
                randomTargetSide,
                diagonalSide,
                volumeBounds
            );
            Vector3 moveTargetDirection = (targetPosition - spawnPosition).normalized;

            return new BalloonSpawnData(spawnPosition, moveTargetDirection);
        }

        private Side GetRandomSideExcept(Side excludeSide)
        {
            int side = UnityEngine.Random.Range(0, 3);
            if (side >= (int)excludeSide)
                side++;

            return (Side)side;
        }

        private Side GetDiagonalSide(
            Side targetSide,
            Side spawnSide,
            Vector3 spawnPosition,
            Vector3 volumeCenter
        )
        {
            Side diagonalSide = Side.Left;
            Side opposedSide = Side.Left;
            switch (spawnSide)
            {
                case Side.Left:
                case Side.Right:
                    opposedSide = spawnPosition.y > volumeCenter.y ? Side.Up : Side.Down;
                    if((int)targetSide % 2 == 1)
                    {
                        diagonalSide = (Side)(((int)spawnSide + 2) % 4);
                        break;
                    } 
                    diagonalSide = opposedSide == Side.Up ? Side.Down : Side.Up;
                    break;
                case Side.Up:
                case Side.Down:
                    opposedSide = spawnPosition.x > volumeCenter.x ? Side.Right : Side.Left;
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

        private Vector3 GetSpawnPosition(Side spawnSide, Bounds bounds, float rangeRatio)
        {
            rangeRatio = Mathf.Clamp01(rangeRatio);
            float xDistance = Mathf.Max(
                0f,
                bounds.extents.x * rangeRatio - _spawnInsetFromVolumeEdge
            );
            float yDistance = Mathf.Max(
                0f,
                bounds.extents.y * rangeRatio - _spawnInsetFromVolumeEdge
            );
            Vector3 position = bounds.center;

            switch (spawnSide)
            {
                case Side.Left:
                case Side.Right:
                    position.x += spawnSide == Side.Left ? -xDistance : xDistance;
                    position.y += UnityEngine.Random.Range(-yDistance, yDistance);
                    break;
                case Side.Up:
                case Side.Down:
                    position.x += UnityEngine.Random.Range(-xDistance, xDistance);
                    position.y += spawnSide == Side.Up ? yDistance : -yDistance;
                    break;
            }

            return position;
        }

        private Vector3 GetTargetPositionOutsideVolume(
            Side targetSide,
            Side diagonalSide,
            Bounds bounds
        )
        {
            Vector3 position = bounds.center;
            float targetOffset = Mathf.Max(1.01f, _offsetFromVolumeEdge);

            switch (targetSide)
            {
                case Side.Left:
                case Side.Right:
                    position.x +=
                        (targetSide == Side.Left ? -1f : 1f) *
                        bounds.extents.x *
                        targetOffset;
                    position.y = diagonalSide == Side.Up
                        ? UnityEngine.Random.Range(bounds.center.y, bounds.max.y)
                        : UnityEngine.Random.Range(bounds.min.y, bounds.center.y);
                    break;
                case Side.Up:
                case Side.Down:
                    position.x = diagonalSide == Side.Right
                        ? UnityEngine.Random.Range(bounds.center.x, bounds.max.x)
                        : UnityEngine.Random.Range(bounds.min.x, bounds.center.x);
                    position.y +=
                        (targetSide == Side.Up ? 1f : -1f) *
                        bounds.extents.y *
                        targetOffset;
                    break;
            }

            return position;
        }

        private static Bounds GetVolumeBounds(GameObject volume)
        {
            if (volume.TryGetComponent<BoxCollider>(out var boxCollider))
                return boxCollider.bounds;

            Debug.LogWarning(
                $"{volume.name} has no BoxCollider. Transform scale is used as a fallback."
            );
            return new Bounds(volume.transform.position, Abs(volume.transform.lossyScale));
        }

        private static float GetRandomValueInside(float min, float max, float inset)
        {
            inset = Mathf.Max(0f, inset);
            if (max - min <= inset * 2f)
                return (min + max) * 0.5f;

            return UnityEngine.Random.Range(min + inset, max - inset);
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(
                Mathf.Abs(value.x),
                Mathf.Abs(value.y),
                Mathf.Abs(value.z)
            );
        }

        public void DestroyBalloon(Balloon balloon, HashSet<PlayerRef> sources)
        {
            if (!balloon.Object.HasStateAuthority) return;

            foreach(var playerRef in sources)
            {
                if(PlayerContent.GetPlayer(playerRef) != null)
                    PlayerContent.GetPlayer(playerRef).NetwrokedBalloonCount++;
            }

            if (_currentPatern == GenerationPatern.Float && ActiveBalloons.Count <= _maxBalloons)
            {
                balloon.GetComponent<GazeAnalyseTarget>()?.Unregister(GazeTargetEndReason.Destroyed);
            }
            LobbyManager.Instance.Runner.Despawn(balloon.Object);
            ActiveBalloons.Remove(balloon);
        }

        public void PlayDestroyEffects(Vector3 pos)
        {
            if (!_vfxHolder.TryGet(SettingManager.Instance.BalloonData.VFXIdx, out var effect))
                return;

            Instantiate(effect.Object, pos, effect.Object.transform.rotation);
            SEManager.Instance.Play(effect.CurrentSEPath);
        }

        public void DeleteBalloon(Balloon balloon)
        {
            if(!LobbyManager.Instance.Runner.IsServer) return;

            balloon.GetComponent<GazeAnalyseTarget>()?.Unregister(_currentPatern == GenerationPatern.Float ? GazeTargetEndReason.Removed : GazeTargetEndReason.Destroyed);

            LobbyManager.Instance.Runner.Despawn(balloon.Object);
            ActiveBalloons.Remove(balloon);
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

    }
}
