using FrameWork.RollBackSystem.Interfaces;

namespace FrameWork.RollBackSystem
{
    public sealed class PlayerInputComparer
        : IInputComparer<PlayerInput>
    {
        public bool IsEqual(PlayerInput a, PlayerInput b)
        {
            return
                a.Horizontal == b.Horizontal
                && a.Vertical == b.Vertical
                && a.CastSkill == b.CastSkill;
        }
    }
}
