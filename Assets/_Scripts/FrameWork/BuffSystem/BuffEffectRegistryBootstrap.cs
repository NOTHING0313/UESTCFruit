namespace BuffSystem
{
    /// <summary>
    /// 生产 Buff Effect 注册入口。
    /// </summary>
    internal static class BuffEffectRegistryBootstrap
    {
        internal const int DebugNoOpTickEffectId = 990101;

        internal static void RegisterProductionEffects(BuffEffectRegistry registry)
        {
            if (registry == null)
                return;

            registry.Register(DebugNoOpTickEffectId, new DebugNoOpTickEffect());
        }
    }
}
