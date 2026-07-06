namespace Contracts
{
    public interface IViewCommandSink
    {
        void Enqueue(in ViewEffectCommand command);
    }
}