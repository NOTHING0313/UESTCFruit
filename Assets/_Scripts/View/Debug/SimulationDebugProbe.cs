using BuffSystem;
using Contracts;
using ECSFrameWork;
using System.Collections.Generic;

namespace View
{
    /// <summary>
    /// 从 World 和 Runner 提取只读调试数据。
    /// </summary>
    public class SimulationDebugProbe : IDebugProbe
    {
        private readonly World _world;
        private readonly BuffSystemCore _buffSystem;
        private readonly SimulateRunner _runner;

        public SimulationDebugProbe(World world, BuffSystemCore buffSystem, SimulateRunner runner)
        {
            _world = world;
            _buffSystem = buffSystem;
            _runner = runner;
        }

        public int CurrentFrame => _runner?.FrameCount ?? 0;
        public bool IsRollbacking => false;
        public uint CurrentChecksum => 0;
        public int EntityCount => _world?.AliveEntityCount ?? 0;

        public IReadOnlyList<BuffViewData> GetBuffs(Entity entity)
        {
            return _buffSystem.GetBuffs(entity);
        }
    }
}