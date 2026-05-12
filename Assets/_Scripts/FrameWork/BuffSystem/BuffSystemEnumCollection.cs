namespace BuffSystem
{
    public enum BuffInstanceType
    {
        normal,
        parallel
    }

    public enum BuffTriggerType
    {
        /// <summary>
        /// 按固定帧间隔触发。
        /// </summary>
        Tick,

        /// <summary>
        /// 由 ECS 逻辑事件触发。
        /// </summary>
        EventTrigger
    }

    public enum NormalBuffStackPolicy
    {
        RefreshDuration = 0,
        AddDuration = 1,
        AddStackOnly = 2,
        AddStackAndRefreshDuration = 3,
        CyclicStack = 4
    }

    public enum ParallelBuffStackUpPolicy
    {
        Append = 0,
        RefreshEarliest = 1,
        RefreshAll = 2,
        ReplaceEarliestWhenFull = 3
    }

    public enum ParallelBuffStackDownPolicy
    {
        RemoveEarliest = 0,
        RemoveLatest = 1,
        ClearAll = 2
    }
}
