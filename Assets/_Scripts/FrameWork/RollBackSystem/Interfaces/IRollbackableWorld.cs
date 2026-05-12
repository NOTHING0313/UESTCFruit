namespace FrameWork.RollBackSystem.Interfaces
{
    public interface IRollbackableWorld<TInput>
        : ISnapshotable<ISnapshot>,
          ISimulation<TInput>,
          ISimulationChecksum
    {
    }
}