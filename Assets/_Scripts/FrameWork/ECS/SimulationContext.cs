//和 PlayerInput 通常放 Contracts，这里暂放 ECS 方便 1 号统一
namespace Contracts
{
    public readonly struct SimulationContext
    {
        public readonly int Frame;
        public readonly float FixedDeltaTime;
        public readonly bool IsRollback;
        public SimulationContext(int frame, float fixedDeltaTime, bool isRollback)
        {
            Frame = frame; FixedDeltaTime = fixedDeltaTime; IsRollback = isRollback;
        }
    }

    public readonly struct PlayerInput
    {
        public readonly int Horizontal;
        public readonly int Vertical;
        public readonly bool CastSkill;
        public PlayerInput(int h, int v, bool skill) { Horizontal = h; Vertical = v; CastSkill = skill; }
    }
}