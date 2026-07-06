using ECSFrameWork;
using FrameWork.RollBackSystem;

namespace Drivers
{
    public sealed class RollbackSimulationDriver : ISimulationDriver
    {
        private readonly RollbackBootstrap _bootstrap;
        private readonly SimulateRunner _runner;

        public int CurrentFrame
        {
            get
            {
                if (_bootstrap != null && _bootstrap.Coordinator != null)
                    return _bootstrap.Coordinator.CurrentFrame;

                return _runner == null ? 0 : _runner.FrameCount;
            }
        }

        public RollbackSimulationDriver(RollbackBootstrap bootstrap, SimulateRunner runner)
        {
            _bootstrap = bootstrap;
            _runner = runner;
        }

        public void Step(in PlayerInputSnapshot input)
        {
            // RollbackBootstrap 已经接管 Runner.BeforeTick。
            // Driver 只负责推进固定帧，不直接写 World。
            _runner?.StepNextFrame(false);
        }
    }
}