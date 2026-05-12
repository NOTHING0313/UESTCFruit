using ECSFrameWork;

namespace Drivers
{
    /// <summary>
    /// Fixed-frame local simulation driver. Systems, including ECSBuffSystem, run through World.Tick.
    /// </summary>
    public sealed class RealtimeSimulationDriver : ISimulationDriver
    {
        private readonly SimulateRunner _runner;

        public int CurrentFrame => _runner == null ? 0 : _runner.FrameCount;

        public RealtimeSimulationDriver(World world, float fixedDeltaTime = 1f / 60f, int maxCompensationTickCount = 1)
        {
            _runner = new SimulateRunner(world, fixedDeltaTime, maxCompensationTickCount);
        }

        public void Step(in PlayerInputSnapshot input)
        {
            _runner?.StepNextFrame(false);
        }
    }
}
