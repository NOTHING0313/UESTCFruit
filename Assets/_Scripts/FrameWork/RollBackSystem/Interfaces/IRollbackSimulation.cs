namespace FrameWork.RollBackSystem.Interfaces
{
    public interface IRollbackSimulation<TInput>
    {
        int CurrentFrame { get; }

        void Step(in TInput input);

        void SaveSnapshot();

        void ReceiveAuthoritativeInput(
            int frame,
            in TInput input);

        bool RollbackTo(int frame);

        void ResimulateTo(int targetFrame);
    }
}