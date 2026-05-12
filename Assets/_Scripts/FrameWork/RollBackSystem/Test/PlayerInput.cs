namespace FrameWork.RollBackSystem.Test
{
    public readonly struct PlayerInput
    {
        public readonly int Damage;

        public readonly bool Crit;

        public PlayerInput(
            int damage,
            bool crit)
        {
            Damage = damage;
            Crit = crit;
        }
    }
}