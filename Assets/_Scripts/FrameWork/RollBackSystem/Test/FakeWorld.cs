using FrameWork.RollBackSystem.Interfaces;
using Simulation.Contracts;
using ECSFrameWork;

namespace FrameWork.RollBackSystem.Tests
{
    /// <summary>
    /// FakeWorld 模拟一个简单的位置推进世界：
    /// - 每帧根据 PlayerInput.Horizontal 改变 position
    /// - 快照保存 position 值
    /// - 用于验证回滚系统的核心流程
    /// </summary>
    public sealed class FakeWorld : IRollbackableWorld<PlayerInput>
    {
        public int Position { get; private set; }
        public int CurrentFrame { get; private set; }

        public void Simulate(PlayerInput input, SimulationContext context)
        {
            Position += input.Horizontal;
            CurrentFrame = context.frameNumber;
        }

        public ISnapshot Capture(int frame)
        {
            return new FakeSnapshot(frame, Position);
        }

        public void Restore(ISnapshot snapshot)
        {
            var s = (FakeSnapshot)snapshot;
            Position = s.Position;
            CurrentFrame = s.Frame;
        }

        public uint CalculateChecksum()
        {
            unchecked
            {
                return (uint)((Position * 31) + CurrentFrame);
            }
        }
    }
}
