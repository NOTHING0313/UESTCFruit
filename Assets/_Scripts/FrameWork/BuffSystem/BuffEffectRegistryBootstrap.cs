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

            // <buffsystem-auto-effect-registry>
            // This block is maintained by Buff Authoring Hub.
            // Manual edits inside this block may be overwritten.
            // Move long-term custom registrations outside this block.
            // Auto registration does not imply whitelist approval or runtime validation.
            // </buffsystem-auto-effect-registry>
        }
    }
}
