using ECSFrameWork;

namespace Drivers
{
    public interface ISimulationDriver
    {
        int CurrentFrame { get; }
        void Step(in PlayerInputSnapshot input);
    }
}