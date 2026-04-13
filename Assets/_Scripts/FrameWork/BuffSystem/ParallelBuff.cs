using UnityEngine;
namespace BuffSystem
{
    /// <summary>
    /// 用串行数据结构去模拟并行叠加 Buff
    /// </summary>
    public class ParallelBuff : Buff
    {
        public override bool IsStackOver => false;
        public override bool IsCompletelyOver => RunTimeData.Stack <= 0;
        private ParallelBuffRunTimeData _parallelData;
        public ParallelBuffRunTimeData ParallelData
        {
            get
            {
                _parallelData ??= RunTimeData as ParallelBuffRunTimeData;
                return _parallelData;
            }
        }
        public override void EndBuff() { }
        public override void StartBuff()
        {
            ParallelData?.InitExpiries(Time.time, ConfigData.Duration);
            RunTimeData.RunTime = 0f;
            RunTimeData.Ticks = 0;
        }
        public override void UpperBuffStack(int stackCount = 1) => ParallelData?.AddStacks(Time.time, ConfigData.Duration, stackCount, ConfigData.Unlimited, ConfigData.MaxStack);
        public bool HasDueStack(float now) => ParallelData.HasAnyExpiry && ParallelData.NextExpiry <= now;
        public int ExpireDueStacks(float now) => ParallelData?.ExpireDue(now) ?? 0;
        public override int DownBuffStack(int stackCount = 1) => ParallelData?.RemoveEarliestStacks(stackCount) ?? 0;
    }
}
