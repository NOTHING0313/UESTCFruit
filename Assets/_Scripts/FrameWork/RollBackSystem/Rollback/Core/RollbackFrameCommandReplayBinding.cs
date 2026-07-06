using ECSFrameWork;

namespace FrameWork.RollBackSystem
{
    internal readonly struct RollbackFrameCommandReplayBinding
    {
        public readonly SimulationFrameCommandBuffer CommandBuffer;
        public readonly SimulationFrameCommandApplier CommandApplier;

        public RollbackFrameCommandReplayBinding(
            SimulationFrameCommandBuffer commandBuffer,
            SimulationFrameCommandApplier commandApplier)
        {
            CommandBuffer = commandBuffer;
            CommandApplier = commandApplier;
        }

        public bool IsValid => CommandBuffer != null && CommandApplier != null;
    }
}
