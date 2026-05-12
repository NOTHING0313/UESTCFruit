namespace FrameWork.RollBackSystem.Interfaces
{
    public interface IInputComparer<TInput>
    {
        bool IsEqual(
            in TInput predicted,
            in TInput authoritative);
    }
}