using UnityEngine;
namespace BuffSystem
{
    /// <summary>
    /// 具体的Buff效果由这个类的派生类实现
    /// </summary>
    public abstract class BuffEffect : ScriptableObject
    {
        // 用于 EffectState 的 key
        public abstract int EffectKey { get; }

        public virtual void OnApply(in BuffContext ctx) { }
        public virtual void OnRefresh(in BuffContext ctx) { }          // 同 buff 再次 Add（刷新持续时间等）
        public virtual void OnStackChanged(in BuffContext ctx, int delta) { }
        public virtual void OnTick(in BuffContext ctx) { }
        public virtual void OnEvent(in BuffContext ctx) { }            // 事件触发型
        public virtual void OnRemove(in BuffContext ctx) { }           // 必须可清理
    }
}
