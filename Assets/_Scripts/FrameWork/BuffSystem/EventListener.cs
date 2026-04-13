using System;

namespace BuffSystem
{
    public abstract class EventListener<TEvent> : IEventListener where TEvent : struct, IGameEvent
    {
        public Type EventType => typeof(TEvent);
        public abstract int Priority { get; }
        public int OwnerBuffID { get; set; }
        // 这里不会装箱：gameEvent 是 interface，但实际传递用 router 内部会走泛型分发
        // 因此这个 Invoke 最终我们不会在热路径用它直接 cast interface；
        // 真实热路径用 EventRouter.Raise<TEvent>(in TEvent e)
        // 热路径指的是频繁调用，执行时间占比很大的代码路径
        public void Invoke(BuffHandler buffHandler, in IGameEvent gameEvent) => OnEvent(buffHandler, (TEvent)gameEvent);//强转的临时值没有稳定的参数地址因此无法写in
        /// <summary>
        /// 仅当从非泛型入口进入时才会走这里
        /// </summary>
        /// <param name="buffHandler"></param>
        /// <param name="gameEvent"></param>
        public abstract void OnEvent(BuffHandler buffHandler, in TEvent gameEvent);
    }
    public sealed class SimpleEventTrigger<TEvent> : EventListener<TEvent>
    where TEvent : struct, IGameEvent
    {
        private readonly int _priority;
        private readonly System.Func<BuffHandler, TEvent, bool> _predicate;

        public override int Priority => _priority;

        public SimpleEventTrigger(int priority, System.Func<BuffHandler, TEvent, bool> predicate = null)
        {
            _priority = priority;
            _predicate = predicate;
        }

        public override void OnEvent(BuffHandler handler, in TEvent e)
        {
            if (_predicate != null && !_predicate(handler, e)) return;
            handler.RegistBuffEffectRequest(OwnerBuffID, EffectPhase.Event, Priority, ev: e);
        }
    }
}
