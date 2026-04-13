namespace BuffSystem.BuffInstance
{
    public class SimpleBuff : Buff
    {
        public override int DownBuffStack(int stackCount = 1) { return -1; }
        public override void UpperBuffStack(int stackCount = 1) { }
        public override void EndBuff() { }
        public override void StartBuff() { }
    }
}
