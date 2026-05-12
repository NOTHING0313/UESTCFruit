using ECSFrameWork;

namespace BuffSystem
{
    /// <summary>
    /// 面向 UI、调试面板和表现同步的只读 Buff 视图数据。
    /// </summary>
    public readonly struct BuffViewData
    {
        public readonly Entity Target;
        public readonly Entity Source;
        public readonly int ConfigId;
        public readonly int Stack;

        /// <summary>
        /// 剩余固定帧数；-1 表示永久 Buff。
        /// </summary>
        public readonly int RemainingFrames;

        public readonly int RuntimeHandle;

        public BuffViewData(Entity target, Entity source, int configId, int stack, int remainingFrames, int runtimeHandle)
        {
            Target = target;
            Source = source;
            ConfigId = configId;
            Stack = stack;
            RemainingFrames = remainingFrames;
            RuntimeHandle = runtimeHandle;
        }
    }
}
