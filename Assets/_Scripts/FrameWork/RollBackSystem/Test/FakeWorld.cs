using FrameWork.RollBackSystem.Interfaces;

namespace FrameWork.RollBackSystem.Test
{
    public sealed class FakeWorld
        : ISnapshotable<TestSnapshot>,
        ISimulation<PlayerInput>
    {
        public int HP { get; private set; } = 100;

        public void Damage(int value)
        {
            HP -= value;
        }

        public void Simulate(in PlayerInput input)
        {
            int finalDamage = input.Damage;

            if (input.Crit)
            {
                finalDamage *= 2;
            }

            Damage(finalDamage);
        }

        public TestSnapshot Capture(int frame)
        {
            return new TestSnapshot(frame, HP);
        }

        public void Restore(TestSnapshot snapshot)
        {
            HP = snapshot.HP;
        }
    }
}