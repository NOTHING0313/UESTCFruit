using UnityEngine;

namespace BuffSystem
{
    public interface IBuffStackDownStrategy : IBuffStackStrategy { public void Apply(Buff buff, int stackCount = 1); }
    public sealed class ReduceBuffStackDownStrategy : IBuffStackDownStrategy
    {
        public string ID => "ReduceBuffStackDownStrategy";
        public void Apply(Buff buff, int stackCount = 1)
        {
            buff.RunTimeData.RunTime = 0;
            buff.RunTimeData.Stack = Mathf.Max(buff.RunTimeData.Stack - stackCount, 0);
            buff.RunTimeData.Ticks = 0;
        }
    }
    public sealed class ClearBuffStackDownStrategy : IBuffStackDownStrategy
    {
        public string ID => "ClearBuffStackDownStrategy";
        public void Apply(Buff buff, int stackCount = 1)=> buff.RunTimeData.Stack = 0;
    }
}
