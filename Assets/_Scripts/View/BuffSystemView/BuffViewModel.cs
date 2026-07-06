namespace View
{
    /// <summary>
    /// Read-only Buff data prepared for View presentation.
    /// </summary>
    public readonly struct BuffViewModel
    {
        public readonly int ConfigId;
        public readonly int Stack;
        public readonly int RemainingFrames;
        public readonly int SourceEntity;
        public readonly string EffectIdText;
        public readonly string DebugName;

        public BuffViewModel(
            int configId,
            int stack,
            int remainingFrames,
            int sourceEntity,
            string effectIdText,
            string debugName)
        {
            ConfigId = configId;
            Stack = stack;
            RemainingFrames = remainingFrames;
            SourceEntity = sourceEntity;
            EffectIdText = string.IsNullOrEmpty(effectIdText) ? "N/A" : effectIdText;
            DebugName = string.IsNullOrEmpty(debugName) ? $"Buff {configId}" : debugName;
        }
    }
}
