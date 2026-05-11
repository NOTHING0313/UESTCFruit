using ECSFrameWork;
using BuffSystem;
using Contracts;

namespace Drivers
{
    /// <summary>
    /// 实时（无回滚）驱动器（4号实现，第一天即可用）。
    /// 直接按固定帧步推进 World 和 BuffSystemCore，不保存历史快照。
    /// </summary>
    public sealed class RealtimeSimulationDriver : ISimulationDriver
    {
        private readonly World _world;
        private readonly BuffSystemCore _buffSystem;
        private readonly float _fixedDeltaTime;
        private int _frame;

        public int CurrentFrame => _frame;

        public RealtimeSimulationDriver(World world, BuffSystemCore buffSystem, float fixedDeltaTime = 1f / 60f)
        {
            _world = world;
            _buffSystem = buffSystem;
            _fixedDeltaTime = fixedDeltaTime;
            _frame = 0;
        }

        public void Step(in PlayerInputSnapshot input)
        {
            var context = new SimulationContext(_frame, _fixedDeltaTime, false);
            _world.Tick(context);
            _buffSystem.Tick(_world, context);
            _frame++;
        }
    }
}