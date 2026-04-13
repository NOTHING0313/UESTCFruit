namespace Utility.EventCenter
{
    public interface IEventData { }
    /// <summary>
    /// 无参事件继承该接口
    /// </summary>
    public interface INullDataEvent : IEventData { }
    /// <summary>
    /// 监控值变化的接口
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    public interface IValueChangedEvent<TValue> : IEventData
    {
        TValue OriginValue { get; }
        TValue CurrentValue { get; }
    }
}
