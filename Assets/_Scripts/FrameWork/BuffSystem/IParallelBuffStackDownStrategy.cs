namespace BuffSystem
{
    public interface IParallelBuffStackDownStrategy : IBuffStackStrategy
    {
        int Apply(ParallelBuff buff, int stackCount = 1);
    }
    public sealed class ParallelRemoveEarliestDownStrategy : IParallelBuffStackDownStrategy
    {
        public string ID => "ParallelRemoveEarliestDownStrategy";

        public int Apply(ParallelBuff buff, int stackCount = 1)
        {
            if (buff?.ParallelData == null) return 0;
            return buff.ParallelData.RemoveEarliestStacks(stackCount);
        }
    }
    public sealed class ParallelRemoveLatestDownStrategy : IParallelBuffStackDownStrategy
    {
        public string ID => "ParallelRemoveLatestDownStrategy";

        public int Apply(ParallelBuff buff, int stackCount = 1)
        {
            if (buff?.ParallelData == null) return 0;
            return buff.ParallelData.RemoveLatestStacks(stackCount);
        }
    }
    public sealed class ParallelClearAllDownStrategy : IParallelBuffStackDownStrategy
    {
        public string ID => "ParallelClearAllDownStrategy";

        public int Apply(ParallelBuff buff, int stackCount = 1)
        {
            if (buff?.ParallelData == null) return 0;
            return buff.ParallelData.RemoveEarliestStacks(buff.RunTimeData.Stack);
        }
    }
}