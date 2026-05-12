namespace FrameWork.RollBackSystem.Interfaces
{
    public interface ISimulation<TInput>
    {
        void Simulate(in TInput input);
    }
}