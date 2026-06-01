namespace BuffSystem
{
    /// <summary>
    /// Debug 用空 Tick Effect，仅用于压缩并行 Buff 生产路径 smoke test。
    /// </summary>
    internal sealed class DebugNoOpTickEffect : BuffEffectExecutorBase
    {
        public override void OnTick(in BuffEffectContext context)
        {
        }
    }
}
