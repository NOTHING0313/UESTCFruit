using BuffSystem;
using Drivers;
using ECSFrameWork;
using UnityEngine;

namespace View
{
    /// <summary>
    /// 仿真启动器空壳，负责创建 World、BuffSystemCore 与实时驱动器，并按固定逻辑帧推进。
    /// </summary>
    public sealed class SimulationBootstrap : MonoBehaviour
    {
        [SerializeField] private float _fixedDeltaTime = 1f / 60f;
        [SerializeField] private bool _useRollback = false;

        private World _world;
        private BuffSystemCore _buffSystem;
        private ISimulationDriver _driver;
        private float _accumulatedTime;

        private void Awake()
        {
            _world = new World();
            _buffSystem = new BuffSystemCore();

            // 当前先使用实时驱动器；回滚驱动器可以后续在这里按 _useRollback 切换。
            _driver = new RealtimeSimulationDriver(_world, _buffSystem, _fixedDeltaTime);
        }

        private void Update()
        {
            _accumulatedTime += Time.deltaTime;
            while (_accumulatedTime >= _fixedDeltaTime)
            {
                _accumulatedTime -= _fixedDeltaTime;

                PlayerInputSnapshot emptyInput = new PlayerInputSnapshot(_driver.CurrentFrame, 0);
                _driver.Step(in emptyInput);

                Debug.Log($"[SimulationBootstrap] Frame {_driver.CurrentFrame} finished. RollbackMode={_useRollback}");
            }
        }
    }
}
