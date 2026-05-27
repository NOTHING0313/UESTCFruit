using Simulation.Contracts;

namespace FrameWork.RollBackSystem.Tests
{
    public sealed class FakeSnapshot
        : ISnapshot
    {
        public int Frame { get; }

        public int Position { get; }

        public FakeSnapshot(
            int frame,
            int position)
        {
            Frame = frame;
            Position = position;
        }

        public void Release()
        {
        }
    }
}