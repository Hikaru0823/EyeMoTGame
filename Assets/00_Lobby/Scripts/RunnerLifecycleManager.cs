using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace EyeMoT.Fusion
{
    public class RunnerLifecycleManager : MonoBehaviour
    {
        [SerializeField] NetworkRunner _runnerPrefab;
        [SerializeField] NetworkObject _registryPrefab;

        NetworkRunner _runner;
        NetworkRunner _spawnListenerRunner;
        bool _managerSpawnedForCurrentRunner;

        public NetworkRunner Runner => _runner;

        public NetworkRunner CreateRunner(INetworkRunnerCallbacks callbacks, bool attachSpawnListener = false)
        {
            _runner = Instantiate(_runnerPrefab);
            _runner.ProvideInput = true;
            _runner.AddCallbacks(callbacks);

            if (attachSpawnListener)
            {
                NetworkEvents eventsComponent = _runner.GetComponent<NetworkEvents>();
                if (eventsComponent != null)
                {
                    eventsComponent.PlayerJoined.AddListener(HandlePlayerJoinedSpawn);
                    _spawnListenerRunner = _runner;
                    _managerSpawnedForCurrentRunner = false;
                }
            }

            return _runner;
        }

        public IEnumerator ShutdownRunnerRoutine()
        {
            if (_runner == null)
            {
                yield break;
            }

            NetworkRunner oldRunner = _runner;
            _runner = null;

            Task shutdownTask = oldRunner.Shutdown();
            while (!shutdownTask.IsCompleted)
            {
                yield return null;
            }

            ClearSpawnListenerIfMatches(oldRunner);
        }

        public IEnumerator ReleaseAllRoutine(Action onComplete = null)
        {
            List<NetworkRunner> runners = NetworkRunner.Instances.ToList();
            foreach (NetworkRunner runner in runners)
            {
                if (runner == null)
                {
                    continue;
                }

                Task shutdownTask = runner.Shutdown(shutdownReason: ShutdownReason.Ok);
                while (!shutdownTask.IsCompleted)
                {
                    yield return null;
                }
            }

            Clear();

            foreach (NetworkRunner runner in FindObjectsByType<NetworkRunner>(FindObjectsSortMode.None))
            {
                if (runner != null)
                {
                    Destroy(runner.gameObject);
                }
            }

            foreach (NetworkObject networkObject in FindObjectsByType<NetworkObject>(FindObjectsSortMode.None))
            {
                if (networkObject != null)
                {
                    Destroy(networkObject.gameObject);
                }
            }

            yield return null;
            onComplete?.Invoke();
        }

        public void HandleRunnerShutdown(NetworkRunner runner)
        {
            if (_runner == runner)
            {
                _runner = null;
            }

            ClearSpawnListenerIfMatches(runner);
        }

        public void Clear()
        {
            _runner = null;
            _spawnListenerRunner = null;
            _managerSpawnedForCurrentRunner = false;
        }

        void ClearSpawnListenerIfMatches(NetworkRunner runner)
        {
            if (_spawnListenerRunner == runner)
            {
                _spawnListenerRunner = null;
                _managerSpawnedForCurrentRunner = false;
            }
        }

        void HandlePlayerJoinedSpawn(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer || runner.LocalPlayer != player || _managerSpawnedForCurrentRunner)
            {
                return;
            }

            runner.Spawn(_registryPrefab);
            _managerSpawnedForCurrentRunner = true;
        }
    }
}
