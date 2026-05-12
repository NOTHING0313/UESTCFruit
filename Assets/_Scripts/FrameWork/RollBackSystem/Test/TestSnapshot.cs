using FrameWork.RollBackSystem.Interfaces;

namespace FrameWork.RollBackSystem.Test
{
    public sealed class TestSnapshot : ISnapshot
    {
        public int Frame { get; private set; }

        public int HP;

        public TestSnapshot(int frame, int hp)
        {
            Frame = frame;
            HP = hp;
        }

        public void Release()
        {
        }
    }
}