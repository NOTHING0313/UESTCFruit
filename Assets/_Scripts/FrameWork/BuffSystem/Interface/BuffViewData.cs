using ECSFrameWork;

namespace BuffSystem
{
    public readonly struct BuffViewData
    {
        public readonly Entity Target, Source;
        public readonly int ConfigId, Stack, RemainingFrames, RuntimeHandle;
        public BuffViewData(Entity target, Entity source, int configId, int stack, int remainingFrames, int runtimeHandle)
        {
            Target = target; Source = source; ConfigId = configId; Stack = stack;
            RemainingFrames = remainingFrames; RuntimeHandle = runtimeHandle;
        }
    }
}