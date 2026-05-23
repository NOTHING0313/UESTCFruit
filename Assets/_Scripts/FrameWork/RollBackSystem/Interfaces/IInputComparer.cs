namespace FrameWork.RollBackSystem.Interfaces
{
    public interface IInputComparer<TInput>
    {
        bool IsEqual(
            in TInput left,
            in TInput right);
    }
}