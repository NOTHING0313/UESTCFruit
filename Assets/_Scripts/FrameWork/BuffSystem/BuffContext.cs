namespace BuffSystem
{
    public readonly struct BuffContext
    {
        public readonly BuffHandler Handler;
        public readonly Buff Buff;
        public readonly BuffRuntimeData Runtime;
        public readonly object Event; // 用 object 是为了支持泛型事件（热路径仍走泛型监听器）
        public BuffContext(BuffHandler handler, Buff buff, BuffRuntimeData runtime, object ev = null) => (Handler, Buff, Runtime, Event) = (handler, buff, runtime, ev);
    }
}

