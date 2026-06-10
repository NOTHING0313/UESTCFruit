/*
 * FrameCommandSourceAdapter — bridges ECS SimulationFrameCommandApplier to
 * the RollBack system's IFrameCommandSource.
 *
 * Normal path:  applies new commands to World (via ApplyCommandsToWorld)
 * Replay path:  replays cached commands for a given frame (via ReplayCommandsToWorld)
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

        public void ApplyCommandsAtTiming(World world, int frame, SimulationFrameCommandTiming timing, bool isReplay)
        {
            if (_applier == null) return;

            if (isReplay)
                _applier.ReplayCommandsToWorld(frame, timing);
            else
                _applier.ApplyCommandsToWorld(frame, timing);
        }
    }
}
