using BuffSystem;
using Contracts;
using ECSFrameWork;
using System.Collections.Generic;

namespace View
{
    public class SimulationDebugProbe : IDebugProbe
    {
        private readonly World _world;
        private readonly BuffSystemCore _buffSystem;
        private readonly SimulateRunner _runner;

        private bool _isRollbacking;
        private uint _currentChecksum;

        public SimulationDebugProbe(World world, BuffSystemCore buffSystem, SimulateRunner runner)
        {
            _world = world;
            _buffSystem = buffSystem;
            _runner = runner;
        }

        /// <summary>更新回滚状态，由 SimulationInitializer 每帧调用。</summary>
        public void SetRollbackInfo(bool isRollbacking, uint checksum)
        {
            _isRollbacking = isRollbacking;
            _currentChecksum = checksum;
        }

        public int CurrentFrame => _runner?.FrameCount ?? 0;
        public bool IsRollbacking => _isRollbacking;
        public uint CurrentChecksum => _currentChecksum;
        public int EntityCount => _world?.AliveEntityCount ?? 0;

        public IReadOnlyList<BuffViewData> GetBuffs(Entity entity)
        {
            return _buffSystem.GetBuffs(entity);
        }
    }
}