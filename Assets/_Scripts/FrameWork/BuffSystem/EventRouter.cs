using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuffSystem
{
    public sealed class EventRouter
    {
        private const int DEFUALT_LIST_COUNT = 4;
        private const int DEFUALT_BUFFLISTENERS_COUNT = 128;
        private readonly BuffHandler _handler;
        private readonly Dictionary<Type, Bucket> _buckets = new();
        private readonly Dictionary<int, List<IEventListener>> _buffListeners = new(DEFUALT_BUFFLISTENERS_COUNT);
        public EventRouter(BuffHandler handler) => _handler = handler;
        private Bucket GetOrCreateBucket(Type type)
        {
            if (!_buckets.TryGetValue(type, out Bucket bucket))
            {
                bucket = new();
                _buckets.Add(type, bucket);
            }
            return bucket;
        }
        public void Register(int buffID, IEventListener eventListener)
        {
            if (eventListener == null)
            {
                Debug.LogError("EventRouter Register Error:EventListener Is Null");
                return;
            }
            eventListener.OwnerBuffID = buffID;
            if (!_buffListeners.TryGetValue(buffID, out List<IEventListener> list))
            {
                list = new(DEFUALT_LIST_COUNT);
                _buffListeners.Add(buffID, list);
            }
            list.Add(eventListener);
            Bucket bucket = GetOrCreateBucket(eventListener.EventType);
            bucket.Add(eventListener);
        }
        public void UnregisterAll(int buffID)
        {
            if (!_buffListeners.TryGetValue(buffID, out List<IEventListener> list))
                return;
            foreach (IEventListener e in list)
                if (_buckets.TryGetValue(e.EventType, out Bucket bucket))
                    bucket.Remove(e);
            list.Clear();
            _buffListeners.Remove(buffID);
        }
        public void Raise<TEvent>(in TEvent e) where TEvent : struct, IGameEvent
        {
            if (_buckets.TryGetValue(typeof(TEvent), out Bucket b))
                b.Raise(_handler, in e);
        }
        private sealed class Bucket
        {
            //使用List+有序插入
            private readonly List<IEventListener> _listener = new(8);
            private bool _dirty;
            public void Add(IEventListener listener)
            {
                _listener?.Add(listener);
                _dirty = true;
            }
            public void Remove(IEventListener listener)
            {
                int index = _listener.IndexOf(listener);
                if (index >= 0)
                {
                    int last = _listener.Count - 1;
                    _listener[index] = _listener[last];
                    _listener.RemoveAt(last);
                    _dirty = true;
                }
            }
            public void EnsureSorted()
            {
                if (!_dirty)
                    return;
                _dirty = false;
                _listener.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            }
            public void Raise<TEvent>(BuffHandler buffHandler, in TEvent e) where TEvent : struct, IGameEvent
            {
                EnsureSorted();
                foreach (IEventListener listener in _listener)
                {
                    if (!buffHandler.ContainBuffHandle(listener.OwnerBuffID))
                        continue;
                    // 强转到对应泛型 listener（这是零装箱关键）
                    if (listener is EventListener<TEvent> eventListener)
                        eventListener.OnEvent(buffHandler, in e);
                    else
                        listener.Invoke(buffHandler, e);
                }
            }
        }
    }
}