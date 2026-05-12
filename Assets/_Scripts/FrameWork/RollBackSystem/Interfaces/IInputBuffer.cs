namespace FrameWork.RollBackSystem.Interfaces
{
    public interface IInputBuffer<TInput>
    {
        void Save(int frame, in TInput input);

        bool TryGet(int frame, out TInput input);

        void ClearBefore(int frame);
    }
}