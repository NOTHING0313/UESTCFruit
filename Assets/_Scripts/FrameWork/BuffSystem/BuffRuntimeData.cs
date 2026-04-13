using PoolSystem;
using System;
using System.Collections.Generic;
using UnityEngine;
using static Unity.VisualScripting.Member;
namespace BuffSystem
{
    public class BuffRuntimeData : IReference<BuffRuntimeData>
    {
        /// <summary>
        /// Buff的施加者
        /// </summary>
        public GameObject Sourse;
        /// <summary>
        /// Buff的接收者
        /// </summary>
        public GameObject Target;
        /// <summary>
        /// 实际的Buff运行时间上限
        /// </summary>
        public virtual float ActualDuration { get; set; }
        /// <summary>
        /// Buff的运行时间
        /// </summary>
        public float RunTime;
        /// <summary>
        /// Buff的触发次数
        /// </summary>
        public int Ticks;
        /// <summary>
        /// 层数
        /// </summary>
        public int Stack;
        public BuffEffectState effectState;
        public bool RemoveQueued;
        public int SourceKey { get; private set; }
        public int RuntimeHandle { get; internal set; }
        public BuffRuntimeData() => effectState = new();
        public virtual void Init(GameObject sourse, GameObject target, int stack)
        {
            Sourse = sourse;
            Target = target;
            SourceKey = sourse ? sourse.GetInstanceID() : 0;
            Stack = stack;
            ActualDuration = 0f;
            RunTime = 0f;
            Ticks = 0;
            RemoveQueued = false;
            effectState.Clear();
        }
        #region IReference
        int IReference.IndexInReferencePool { get; set; }
        public IReference GetNewInstance() => new BuffRuntimeData();
        public virtual void OnRecycle()
        {
            Sourse = null;
            Target = null;
            ActualDuration = 0;
            RunTime = 0;
            Ticks = 0;
            Stack = 0;
            effectState?.Clear();
            RemoveQueued = false;
            SourceKey = 0;
            RuntimeHandle = 0;
        }
        public virtual void Dispose()
        {
            OnRecycle();
            effectState = null;
        }
        #endregion
    }
    public sealed class BuffEffectState
    {
        private readonly Dictionary<int, object> _state = new();
        public bool TryGet<T>(int key, out T value)
        {
            if (_state.TryGetValue(key, out object obj) && obj is T t)
            {
                value = t;
                return true;
            }
            value = default;
            return false;
        }
        public void Set(int key, object value) { if (value == null) return; _state[key] = value; }
        public void Remove(int key) { if (_state.ContainsKey(key)) _state.Remove(key); }
        public void Clear() => _state?.Clear();
    }
    public readonly struct BuffLookupKey : IEquatable<BuffLookupKey>
    {
        public readonly int ConfigId;
        public readonly int SourceKey;

        public BuffLookupKey(int configId, int sourceKey)
        {
            ConfigId = configId;
            SourceKey = sourceKey;
        }

        public bool Equals(BuffLookupKey other)
            => ConfigId == other.ConfigId && SourceKey == other.SourceKey;

        public override bool Equals(object obj)
            => obj is BuffLookupKey other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(ConfigId, SourceKey);
    }
}
