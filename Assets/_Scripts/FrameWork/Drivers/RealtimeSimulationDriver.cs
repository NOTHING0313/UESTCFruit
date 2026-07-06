using ECSFrameWork;

namespace Drivers
{
    public sealed class RealtimeSimulationDriver : ISimulationDriver
    {
        private readonly SimulateRunner _runner;

        public int CurrentFrame => _runner == null ? 0 : _runner.FrameCount;

        public RealtimeSimulationDriver(SimulateRunner runner)
        {
            _runner = runner;
        }

        public void Step(in PlayerInputSnapshot input)
        {
            _runner?.StepNextFrame(false);
        }
    }
}