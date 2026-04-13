using Unity.VisualScripting;
using UnityEngine;

namespace BuffSystem
{
    public interface IBuffStackUpStrategy : IBuffStackStrategy { public void Apply(Buff buff, BuffRuntimeData data); }
    public sealed class ResetRunTimeBuffStackUpStrategy : IBuffStackUpStrategy
    {
        public string ID => "ResetRuntimeBuffStackUpStrategy";
        public void Apply(Buff buff, BuffRuntimeData data)
        {
            buff.RunTimeData.RunTime = 0;
            buff.RunTimeData.Ticks = 0;
        }
    }
    public sealed class AddDurationBuffStackUpStrategy : IBuffStackUpStrategy
    {
        public string ID => "AddDurationBuffStackUpStrategy";
        public void Apply(Buff buff, BuffRuntimeData data) => buff.RunTimeData.ActualDuration += buff.ConfigData.DurationExtendPerStack * data.Stack;
    }
    public sealed class AddStackOnlyBuffStackUpStrategy : IBuffStackUpStrategy
    {
        public string ID => "AddStackOnlyBuffStackUpStrategy";
        public void Apply(Buff buff, BuffRuntimeData data)
        {
            if (!buff.ConfigData.Unlimited) 
                buff.RunTimeData.Stack = Mathf.Min(data.Stack + buff.RunTimeData.Stack, buff.ConfigData.MaxStack);
            else
                buff.RunTimeData.Stack += data.Stack;
        }
    }
    public sealed class CyclicallyAddStackOnlyBuffStackUpStrategy : IBuffStackUpStrategy
    {
        public string ID => "CyclicallyAddStackOnlyBuffStackUpStrategy";
        public void Apply(Buff buff, BuffRuntimeData data)
        {
            buff.RunTimeData.Stack += data.Stack;
            if (buff.ConfigData.MaxStack > 0 && !buff.ConfigData.Unlimited)
                buff.RunTimeData.Stack %= buff.ConfigData.MaxStack;
            else
                buff.RunTimeData.Stack += data.Stack;
        }
    }
    public sealed class AddStackAndResetRuntimeBuffStackUpStrategy : IBuffStackUpStrategy
    {
        public string ID => "AddStackAndResetRuntimeBuffStackUpStrategy";
        public void Apply(Buff buff, BuffRuntimeData data)
        {
            buff.RunTimeData.RunTime = 0;
            buff.RunTimeData.Ticks = 0;
            if (!buff.ConfigData.Unlimited)
                buff.RunTimeData.Stack = Mathf.Min(data.Stack + buff.RunTimeData.Stack, buff.ConfigData.MaxStack);
            else
                buff.RunTimeData.Stack += data.Stack;
        }
    }
}
