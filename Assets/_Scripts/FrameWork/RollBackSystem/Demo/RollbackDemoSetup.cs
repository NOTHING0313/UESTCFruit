/*
 * RollbackDemoSetup — 演示搭建脚本。
 */

using ECSFrameWork;
using System.Reflection;
using UnityEngine;
using View;

namespace FrameWork.RollBackSystem.Demo
{
    public sealed class RollbackDemoSetup : MonoBehaviour
    {
        [SerializeField] private bool _autoSetup = true;

        [Header("Player")]
        [SerializeField] private int _playerPrefabID = 1;
        [SerializeField] private float _moveSpeed = 5f;

        [Header("View")]
        [SerializeField] private GameObject _playerPrefab;

        private void Start()
        {
            if (!_autoSetup) return;

            var rollback = FindObjectOfType<RollbackBootstrap>();
            if (rollback == null)
            {
                Debug.LogError("[RollbackDemoSetup] RollbackBootstrap not found!");
                return;
            }

            StartCoroutine(Setup(rollback));
        }

        private System.Collections.IEnumerator Setup(RollbackBootstrap rollback)
        {
            yield return null;
            yield return null;

            var world = rollback.World;
            if (world == null) { Debug.LogError("[RollbackDemoSetup] World null."); yield break; }

            // 反射拿 SimulationInitializer 的 ViewManager 注册 prefab
            if (_playerPrefab != null)
            {
                var simInit = FindObjectOfType<SimulationInitializer>();
                if (simInit != null)
                {
                    var field = typeof(SimulationInitializer).GetField("_viewManager",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field?.GetValue(simInit) is ViewManager vm)
                        vm.RegisterPrefab(_playerPrefabID, _playerPrefab);
                }
            }

            var player = world.CreateEntity();
            world.SetComponent(player, new PositionComponent(0, 0, 0));
            world.SetComponent(player, new VelocityComponent(0, 0, 0));
            world.SetComponent(player, new MoveSpeedComponent(_moveSpeed));
            world.SetComponent(player, new PlayerInputSnapshotComponent(0f, 0f));

            if (_playerPrefab != null)
                world.SetComponent(player, new PrefabViewRequestComponent(_playerPrefabID));

            rollback.InputApplier?.RegisterPlayer(1, player);

            Debug.Log($"[RollbackDemoSetup] Player entity={player} created.");
        }
    }
}
