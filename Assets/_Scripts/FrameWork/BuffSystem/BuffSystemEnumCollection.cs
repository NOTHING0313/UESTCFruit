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
        CyclicStack = 4,

        /// <summary>
        /// 重复添加时不改变当前层数，只重置持续时间与 Tick 计数。
        /// </summary>
        ResetDurationOnly = 5
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

    public enum ParallelBuffStorageMode
    {
        /// <summary>
        /// 默认模式：每一个并行层对应一个 Runtime Entity。
        /// </summary>
        EntityPerStack = 0,

        /// <summary>
        /// Phase 3B 预留模式：一个 Runtime Entity 内部用固定帧层表管理多个并行层；当前尚未接入运行时主流程。
        /// </summary>
        CompressedExpiryFrameList = 1
    }
}
