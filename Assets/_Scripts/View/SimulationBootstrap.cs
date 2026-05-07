using UnityEngine;
using ECS;
using BuffSystem;
using Contracts;
using Drivers;

namespace View
{
    /// <summary>
    /// 仿真启动器（4号负责，挂载到场景 GameObject）。
    /// 创建逻辑世界、Buff 系统、驱动器，按固定帧步推进，后续接入表现同步。
    /// 第一天只需让此脚本跑起来，空帧输出日志。
    /// </summary>
    public sealed class SimulationBootstrap : MonoBehaviour
    {
        [SerializeField] private float _fixedDeltaTime = 1f / 60f;
        [SerializeField] private bool _useRollback = false;   // 暂时只用实时模式

        private World _world;
        private BuffSystemCore _buffSystem;
        private ISimulationDriver _driver;
        private float _accumulatedTime;

        private void Awake()
        {
            // 1号提供 World, 3号提供 BuffSystemCore
            _world = new World();
            _buffSystem = new BuffSystemCore();

            // 先使用实时驱动器，回滚模式后续接入
            _driver = new RealtimeSimulationDriver(_world, _buffSystem, _fixedDeltaTime);
        }

        private void Update()
        {
            _accumulatedTime += Time.deltaTime;
            while (_accumulatedTime >= _fixedDeltaTime)
            {
                _accumulatedTime -= _fixedDeltaTime;

                // 当前无操作，送入空输入
                var emptyInput = new PlayerInput(0, 0, false);
                _driver.Step(emptyInput);

                // 后续在此调用 ViewBridge.Sync(...)
                Debug.Log($"[Bootstrap] Frame {_driver.CurrentFrame} finished");
            }
        }
    }
}