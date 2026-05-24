using BuffSystem;
using Drivers;
using ECSFrameWork;
using UnityEngine;

namespace View
{
    /// <summary>
    /// Simulation bootstrap for local fixed-frame mode.
    /// Unity drives real time here, but Buff runtime itself advances only through ECS fixed frames.
    /// </summary>
    public sealed class SimulationBootstrap : MonoBehaviour
    {
        [SerializeField] private LogicFrameDebugPanel _debugPanel;
        [SerializeField] private float _fixedDeltaTime = 1f / 60f;
        [SerializeField] private bool _useRollback = false;

        private World _world;
        private ECSBuffSystem _buffSystem;
        private ISimulationDriver _driver;
        private float _accumulatedTime;

        private void Awake()
        {
            _world = new World();
            _buffSystem = new ECSBuffSystem(BuffConfigDataLoader.Instance);
            _world.AddSystem(_buffSystem);
            _driver = new RealtimeSimulationDriver(_world, _fixedDeltaTime);
            // 在 Awake 末尾加入
            if (_debugPanel != null)
            {
                var probe = new SimulationDebugProbe(_world, _buffSystem, _driver, isRollback: _useRollback);
                _debugPanel.Initialize(probe);
            }
           
        }

        private void Update()
        {
            _accumulatedTime += Time.deltaTime;

            while (_accumulatedTime >= _fixedDeltaTime)
            {
                _accumulatedTime -= _fixedDeltaTime;

                PlayerInputSnapshot emptyInput = new PlayerInputSnapshot(_driver.CurrentFrame + 1, 0);
                _driver.Step(in emptyInput);

                Debug.Log($"[SimulationBootstrap] Frame {_driver.CurrentFrame} finished. RollbackMode={_useRollback}");
            }
            // 每渲染帧刷新一次调试面板（放在 Update 末尾）
            _debugPanel?.Refresh();
        }
    }
}
