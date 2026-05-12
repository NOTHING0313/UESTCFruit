namespace FrameWork.RollBackSystem.Interfaces
{
    public interface IRollbackSimulation<TInput>
    {
        int CurrentFrame { get; }

        void Step(in TInput input);

        void RollbackTo(int frame);

        void ResimulateTo(int targetFrame);
    }
}