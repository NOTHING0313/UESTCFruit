using ECS;   // EntityHandle

namespace Contracts
{
    /// <summary>
    /// 纯表现特效命令（4号定义，不进入逻辑快照）。
    /// 由逻辑事件驱动产生，传递给 ViewBridge 播放，不影响回滚。
    /// </summary>
    public readonly struct ViewEffectCommand
    {
        public readonly int EffectId;
        public readonly EntityHandle Source;
        public readonly EntityHandle Target;
        public readonly int Frame;

        public ViewEffectCommand(int effectId, EntityHandle source, EntityHandle target, int frame)
        {
            EffectId = effectId;
            Source = source;
            Target = target;
            Frame = frame;
        }
    }
}