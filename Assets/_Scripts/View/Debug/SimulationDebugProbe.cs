using System.Collections.Generic;
using BuffSystem;
using Contracts;
using Drivers;
using ECSFrameWork;

namespace View
{
    /// <summary>
    /// 仿真调试探针，为表现层调试面板提供只读数据。
    /// </summary>
    public sealed class SimulationDebugProbe : IDebugProbe
    {
        private readonly World _world;
        private readonly ISimulationDriver _driver;
        private readonly IBuffSystem _buffSystem;
        private readonly bool _isRollback;        // 临时方案，后续可从 RollbackCoordinator 动态获取
        /// <summary>创建一个空调试探针；未绑定运行时对象时返回默认值。</summary>
        public SimulationDebugProbe()
        {
        }

        /// <summary>创建绑定 World、Driver 与 BuffSystem 的调试探针。</summary>
        public SimulationDebugProbe(World world,
                                    ECSBuffSystem buffSystem,
                                    ISimulationDriver driver,
                                    bool isRollback = false)
        {
            _world = world;
            _buffSystem = buffSystem;
            _driver = driver;
            _isRollback = isRollback;
        }

        public int CurrentFrame => _driver == null ? 0 : _driver.CurrentFrame;
        public bool IsRollbacking => false;
        public int EntityCount => _world == null ? 0 : _world.GetStatistics().aliveEntityCount;
        public uint CurrentChecksum => 0;

        /// <summary>读取指定 Entity 当前拥有的 Buff 视图数据。</summary>
        public IReadOnlyList<BuffViewData> GetBuffs(Entity entity)
        {
            if (_buffSystem == null)
                return System.Array.Empty<BuffViewData>();

            return _buffSystem.GetBuffs(entity);
        }
    }
}
