namespace FrameWork.RollBackSystem
{
    /// <summary>
    /// 第一版玩家输入。
    /// </summary>
    public readonly struct PlayerInput
    {
        public readonly int Horizontal;

        public readonly int Vertical;

        public readonly bool CastSkill;

        public PlayerInput(
            int horizontal,
            int vertical,
            bool castSkill)
        {
            Horizontal = horizontal;
            Vertical = vertical;
            CastSkill = castSkill;
        }
    }
}