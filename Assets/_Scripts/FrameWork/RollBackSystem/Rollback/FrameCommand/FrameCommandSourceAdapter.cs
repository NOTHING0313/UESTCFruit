/*
 * FrameCommandSourceAdapter — bridges ECS SimulationFrameCommandApplier to
 * the RollBack system's IFrameCommandSource.
 *
 * Normal path: applies new commands to World (via ApplyCommandsToWorld)
 * Replay path: replays cached commands for a given frame (via ReplayCommandsToWorld)
 */

using ECSFrameWork;

namespace FrameWork.RollBackSystem
{
    public sealed class FrameCommandSourceAdapter
        : IFrameCommandSource
    {
        private readonly SimulationFrameCommandApplier _applier;

        public FrameCommandSourceAdapter(SimulationFrameCommandApplier applier)
        {
            _applier = applier;
        }

        public void ApplyCommandsToWorld(World world, int frame)
        {
            _applier?.ApplyCommandsToWorld(
                frame,
                SimulationFrameCommandTiming.BeforeTick);

            _applier?.ApplyCommandsToWorld(
                frame,
                SimulationFrameCommandTiming.AfterTick);
        }

        public void ReplayCommandsToWorld(World world, int frame)
        {
            _applier?.ReplayCommandsToWorld(
                frame,
                SimulationFrameCommandTiming.BeforeTick);

            _applier?.ReplayCommandsToWorld(
                frame,
                SimulationFrameCommandTiming.AfterTick);
        }
    }
}
