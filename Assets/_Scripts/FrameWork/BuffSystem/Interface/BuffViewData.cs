using ECS;

namespace BuffSystem
{
    public readonly struct BuffViewData
    {
        public readonly EntityHandle Target, Source;
        public readonly int ConfigId, Stack, RemainingFrames, RuntimeHandle;
        public BuffViewData(EntityHandle target, EntityHandle source, int configId, int stack, int remainingFrames, int runtimeHandle)
        {
            Target = target; Source = source; ConfigId = configId; Stack = stack;
            RemainingFrames = remainingFrames; RuntimeHandle = runtimeHandle;
        }
    }
}