using UnityEngine;

namespace BuffSystem
{
    public interface IParallelBuffStackUpStrategy : IBuffStackStrategy
    {
        void Apply(ParallelBuff buff, ParallelBuffRunTimeData incoming);
    }
    /// <summary>
    /// 新增层，每层独立持续时间
    /// 例：毒、流血、炸弹标记
    /// </summary>
    public sealed class ParallelAppendStackUpStrategy : IParallelBuffStackUpStrategy
    {
        public string ID => "ParallelAppendStackUpStrategy";

        public void Apply(ParallelBuff buff, ParallelBuffRunTimeData incoming)
        {
            if (buff == null || incoming == null || buff.ParallelData == null) return;
            buff.ParallelData.AddStacks(
                Time.time,
                buff.ConfigData.Duration,
                incoming.Stack,
                buff.ConfigData.Unlimited,
                buff.ConfigData.MaxStack
            );
            buff.RunTimeData.RunTime = 0f;
        }
    }
    /// <summary>
    /// 不新增总层数，优先刷新最早到期的层
    /// 例：护盾充能、易伤标记续命
    /// </summary>
    public sealed class ParallelRefreshEarliestUpStrategy : IParallelBuffStackUpStrategy
    {
        public string ID => "ParallelRefreshEarliestUpStrategy";

        public void Apply(ParallelBuff buff, ParallelBuffRunTimeData incoming)
        {
            if (buff == null || incoming == null || buff.ParallelData == null) return;

            int refreshCount = Mathf.Min(incoming.Stack, buff.RunTimeData.Stack);
            int remainToAdd = incoming.Stack - refreshCount;

            if (refreshCount > 0)
                buff.ParallelData.RefreshEarliestStacks(Time.time, buff.ConfigData.Duration, refreshCount);

            if (remainToAdd > 0)
            {
                buff.ParallelData.AddStacks(
                    Time.time,
                    buff.ConfigData.Duration,
                    remainToAdd,
                    buff.ConfigData.Unlimited,
                    buff.ConfigData.MaxStack
                );
            }

            buff.RunTimeData.RunTime = 0f;
        }
    }
    /// <summary>
    /// 所有现有层统一续满，再按需要补新增层
    /// 例：连击维持型 Buff
    /// </summary>
    public sealed class ParallelRefreshAllUpStrategy : IParallelBuffStackUpStrategy
    {
        public string ID => "ParallelRefreshAllUpStrategy";

        public void Apply(ParallelBuff buff, ParallelBuffRunTimeData incoming)
        {
            if (buff == null || incoming == null || buff.ParallelData == null) return;

            if (buff.RunTimeData.Stack > 0)
                buff.ParallelData.RefreshAllStacks(Time.time, buff.ConfigData.Duration);

            buff.ParallelData.AddStacks(
                Time.time,
                buff.ConfigData.Duration,
                incoming.Stack,
                buff.ConfigData.Unlimited,
                buff.ConfigData.MaxStack
            );

            buff.RunTimeData.RunTime = 0f;
        }
    }
    /// <summary>
    /// 满层时，不丢这次叠加，而是替换最早到期层
    /// </summary>
    public sealed class ParallelReplaceEarliestWhenFullUpStrategy : IParallelBuffStackUpStrategy
    {
        public string ID => "ParallelReplaceEarliestWhenFullUpStrategy";

        public void Apply(ParallelBuff buff, ParallelBuffRunTimeData incoming)
        {
            if (buff == null || incoming == null || buff.ParallelData == null) return;

            int count = incoming.Stack;
            if (count <= 0) return;

            if (buff.ConfigData.Unlimited || buff.ConfigData.MaxStack <= 0)
            {
                buff.ParallelData.AddStacks(Time.time, buff.ConfigData.Duration, count, true, 0);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                if (buff.RunTimeData.Stack < buff.ConfigData.MaxStack)
                {
                    buff.ParallelData.AddStacks(Time.time, buff.ConfigData.Duration, 1, false, buff.ConfigData.MaxStack);
                }
                else
                {
                    buff.ParallelData.RemoveEarliestStacks(1);
                    buff.ParallelData.AddStacks(Time.time, buff.ConfigData.Duration, 1, false, buff.ConfigData.MaxStack);
                }
            }

            buff.RunTimeData.RunTime = 0f;
        }
    }
}
