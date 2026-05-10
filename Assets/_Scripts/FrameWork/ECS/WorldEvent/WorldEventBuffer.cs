using System;
using System.Collections.Generic;

/// <summary>
/// World 事件缓冲区，用于保存逻辑帧中产生的一次性事件。
/// </summary>
/// <remarks>
/// WorldEventBuffer 是 ECS 到表现层、UI、音效层的输出通道；它不参与 Entity 状态存储，也不负责驱动逻辑。
/// </remarks>
public sealed class WorldEventBuffer
{
    private readonly Dictionary<Type, IWorldEventList> _eventsByType = new Dictionary<Type, IWorldEventList>();

    /// <summary>当前缓存的事件总数。</summary>
    public int Count { get; private set; }

    /// <summary>写入一个 World 事件。</summary>
    public void Add<T>(T worldEvent) where T : struct, IWorldEvent
    {
        WorldEventList<T> events = GetOrCreateEvents<T>();
        events.Add(worldEvent);
        Count++;
    }

    /// <summary>获取指定类型的 World 事件只读列表；没有事件时返回空数组。</summary>
    public IReadOnlyList<T> GetEvents<T>() where T : struct, IWorldEvent
    {
        if (_eventsByType.TryGetValue(typeof(T), out IWorldEventList rawEvents) && rawEvents is WorldEventList<T> events)
            return events.Events;

        return EmptyWorldEventList<T>.Value;
    }

    /// <summary>清理所有 World 事件。</summary>
    public void Clear()
    {
        foreach (IWorldEventList events in _eventsByType.Values)
            events.Clear();

        Count = 0;
    }

    /// <summary>清理指定逻辑帧之前产生的事件。</summary>
    public void ClearBeforeFrame(int frameNumber)
    {
        if (Count <= 0)
            return;

        Count = 0;

        foreach (IWorldEventList events in _eventsByType.Values)
        {
            events.ClearBeforeFrame(frameNumber);
            Count += events.Count;
        }
    }

    /// <summary>获取或创建指定事件类型的缓存列表。</summary>
    private WorldEventList<T> GetOrCreateEvents<T>() where T : struct, IWorldEvent
    {
        Type eventType = typeof(T);

        if (_eventsByType.TryGetValue(eventType, out IWorldEventList rawEvents) && rawEvents is WorldEventList<T> events)
            return events;

        events = new WorldEventList<T>();
        _eventsByType[eventType] = events;
        return events;
    }

    private interface IWorldEventList
    {
        int Count { get; }
        void Clear();
        void ClearBeforeFrame(int frameNumber);
    }

    private sealed class WorldEventList<T> : IWorldEventList where T : struct, IWorldEvent
    {
        public readonly List<T> Events = new List<T>(16);
        public int Count => Events.Count;

        /// <summary>写入事件。</summary>
        public void Add(T worldEvent)
        {
            Events.Add(worldEvent);
        }

        /// <summary>清理全部事件。</summary>
        public void Clear()
        {
            Events.Clear();
        }

        /// <summary>清理指定逻辑帧之前的事件。</summary>
        public void ClearBeforeFrame(int frameNumber)
        {
            for (int i = Events.Count - 1; i >= 0; i--)
            {
                if (Events[i].frameNumber < frameNumber)
                    Events.RemoveAt(i);
            }
        }
    }

    private static class EmptyWorldEventList<T> where T : struct, IWorldEvent
    {
        public static readonly T[] Value = new T[0];
    }
}
