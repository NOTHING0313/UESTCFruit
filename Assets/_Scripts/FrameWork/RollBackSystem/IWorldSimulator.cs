using ECSFrameWork;

namespace FrameWork.RollBackSystem
{
    public interface IWorldSimulator<TInput>
    {
        void Simulate(
            World world,
            TInput input,
            SimulationContext context);
    }
}