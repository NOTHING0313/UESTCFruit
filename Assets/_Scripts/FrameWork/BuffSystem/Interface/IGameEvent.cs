namespace BuffSystem
{
    /// <summary>
    /// ECS 逻辑事件基础接口；事件数据必须可回滚、可重放，不保存表现层引用。
    /// </summary>
    public interface IGameEvent
    {
        /// <summary>
        /// 事件发生的逻辑帧。
        /// </summary>
        int FrameNumber { get; }

        /// <summary>
        /// 策划配置和运行时过滤使用的事件编号。
        /// </summary>
        int EventId { get; }
    }

    /// <summary>
    /// 支持帧命令重建时安全改写逻辑帧的事件接口。
    /// </summary>
    /// <typeparam name="TEvent">事件自身类型。</typeparam>
    public interface IReframeableGameEvent<TEvent> where TEvent : struct, IGameEvent
    {
        /// <summary>
        /// 返回一个逻辑帧已替换的新事件，原事件实例不应被修改。
        /// </summary>
        TEvent WithFrame(int frameNumber);
    }
}
