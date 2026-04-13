namespace BuffSystem
{
    public enum BuffTriggerType
    {
        /// <summary>
        /// 周期性触发
        /// </summary>
        Tick,
        /// <summary>
        /// 事件触发
        /// </summary>
        EventTrigger
    }
    public interface IBuffStackStrategy
    {
        public string ID { get; }
    }
}
