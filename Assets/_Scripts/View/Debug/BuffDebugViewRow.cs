using BuffSystem;
using ECSFrameWork;

namespace View
{
    /// <summary>
    /// LogicFrameDebugPanel 中 GetBuffs(target) 表格使用的只读展示行。
    /// </summary>
    internal readonly struct BuffDebugViewRow
    {
        public readonly int ConfigId;
        public readonly int Stack;
        public readonly int RemainingFrames;
        public readonly int RuntimeHandle;
        public readonly Entity Target;
        public readonly Entity Source;

        public BuffDebugViewRow(in BuffViewData view)
        {
            ConfigId = view.ConfigId;
            Stack = view.Stack;
            RemainingFrames = view.RemainingFrames;
            RuntimeHandle = view.RuntimeHandle;
            Target = view.Target;
            Source = view.Source;
        }
    }
}
