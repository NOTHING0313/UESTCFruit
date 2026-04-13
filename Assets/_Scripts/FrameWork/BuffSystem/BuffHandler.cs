using System;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.UI.Image;
namespace BuffSystem
{
    /// <summary>
    /// Buff驱动类，用于驱动Buff生命周期并对外提供接口
    /// </summary>
    public class BuffHandler : MonoBehaviour
    {
        private readonly Dictionary<int, Buff> _buffs = new();
        // (configId, sourceKey) -> runtimeHandle
        private readonly Dictionary<BuffLookupKey, int> _handleByLookup = new();
        // runtimeHandle -> (configId, sourceKey)
        private readonly Dictionary<int, BuffLookupKey> _lookupByHandle = new();
        //双缓冲机制
        private List<BuffEffectRequest> _pendingEffects = new();
        private List<BuffEffectRequest> _executingEffects = new();
        private readonly HashSet<int> _timeOutBuff = new();
        private EventRouter _router;
        private int _nextRuntimeHandle = 1;
        private int NewRuntimeHandle() => _nextRuntimeHandle++;
        private long _nextSequence = 0;
        private long NextSequence() => _nextSequence++;
        private static BuffLookupKey MakeLookupKey(int configId, BuffRuntimeData data) => new(configId, data.SourceKey);
        private static BuffLookupKey MakeLookupKey(int configId, GameObject source) => new(configId, source ? source.GetInstanceID() : 0);
        // 给 EventRouter内部流程用
        internal bool ContainBuffHandle(int handle) => _buffs.ContainsKey(handle);
        #region API
        /// <summary>
        /// 任意来源是否存在该Buff
        /// </summary>
        /// <param name="configId"></param>
        /// <returns></returns>
        public bool ContainBuff(int configId) => GetBuff(configId) != null;
        /// <summary>
        /// 指定来源是否存在该Buff
        /// </summary>
        /// <param name="configId"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        public bool ContainBuff(int configId, GameObject source) => TryGetBuff(configId, source, out _);
        /// <summary>
        /// 任意来源取第一个
        /// </summary>
        /// <param name="configId"></param>
        /// <returns></returns>
        public Buff GetBuff(int configId)
        {
            foreach (var pair in _handleByLookup)
                if (pair.Key.ConfigId == configId && _buffs.TryGetValue(pair.Value, out Buff buff))
                    return buff;
            return null;
        }
        /// <summary>
        /// 任意来源取第一个,精确到来源
        /// </summary>
        /// <param name="configId"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        public Buff GetBuff(int configId, GameObject source)
        {
            TryGetBuff(configId, source, out Buff buff);
            return buff;
        }
        public bool TryGetBuff(int configId, GameObject source, out Buff buff)
        {
            if (_handleByLookup.TryGetValue(MakeLookupKey(configId, source), out int handle))
                return _buffs.TryGetValue(handle, out buff);
            buff = null;
            return false;
        }
        public void AddBuff<TBuff>(int configId, BuffRuntimeData runTimeData, Action<Buff, BuffRuntimeData> onAddBuff = null, bool isAutoReleaseRuntimeData = true) where TBuff : Buff, new()
        {
            BuffLookupKey lookupKey = MakeLookupKey(configId, runTimeData);

            if (_handleByLookup.TryGetValue(lookupKey, out int handle) &&
                _buffs.TryGetValue(handle, out Buff existed))
            {
                if (existed is not TBuff buff)
                {
                    Debug.LogError($"BuffHandler AddBuff Error: ConfigId={configId} Already Exists But Type Mismatch.");
                    if (isAutoReleaseRuntimeData)
                        BuffRuntimeDataFactory.Release(runTimeData);
                    return;
                }
                int origin = buff.RunTimeData.Stack;
                float beforeDuration = buff.RunTimeData.ActualDuration;
                float beforeRunTime = buff.RunTimeData.RunTime;
                switch (buff.ConfigData.BuffType)
                {
                    case BuffInstanceType.normal:
                        {
                            if (!string.IsNullOrEmpty(buff.ConfigData.BuffStackUpStrategyID)
                                && BuffStackStrategyRegister.Instance.TryGet(buff.ConfigData.BuffStackUpStrategyID, out IBuffStackStrategy strategy)&& strategy is IBuffStackUpStrategy buffStrategy)
                                buffStrategy.Apply(buff, runTimeData);
                            else
                                buff.UpperBuffStack(runTimeData.Stack);
                            break;
                        }
                    case BuffInstanceType.parallel:
                        {
                            if (!string.IsNullOrEmpty(buff.ConfigData.ParallelBuffStackUpStrategyID)
                                && BuffStackStrategyRegister.Instance.TryGet(buff.ConfigData.ParallelBuffStackUpStrategyID, out IBuffStackStrategy strategy)
                                && buff is ParallelBuff pBuff && runTimeData is ParallelBuffRunTimeData pData&& strategy is IParallelBuffStackUpStrategy parallelBuffStrategy)
                                parallelBuffStrategy.Apply(pBuff, pData);
                            else
                                buff.UpperBuffStack(runTimeData.Stack);
                            break;
                        }
                    default: buff.UpperBuffStack(runTimeData.Stack);break;
                }
                bool refreshed = !Mathf.Approximately(buff.RunTimeData.ActualDuration, beforeDuration) || !Mathf.Approximately(buff.RunTimeData.RunTime, beforeRunTime);
                if (refreshed)
                    RegistBuffEffectRequest(handle, EffectPhase.Refresh, buff.ConfigData.Priority);
                RegistBuffEffectRequest(handle, EffectPhase.StackChanged, buff.ConfigData.Priority, buff.RunTimeData.Stack - origin);
                onAddBuff?.Invoke(buff, runTimeData);
                if (isAutoReleaseRuntimeData)
                    BuffRuntimeDataFactory.Release(runTimeData);
                return;
            }
            TBuff newBuff = BuffFactory.CreateBuff<TBuff>(configId, runTimeData);
            if (newBuff == null) return;
            int newHandle = NewRuntimeHandle();
            runTimeData.RuntimeHandle = newHandle;
            newBuff.StartBuff();
            _buffs.Add(newHandle, newBuff);
            _handleByLookup[lookupKey] = newHandle;
            _lookupByHandle[newHandle] = lookupKey;
            RegisterBuffEventListeners(newHandle, newBuff);
            RegistBuffEffectRequest(newHandle, EffectPhase.Apply, newBuff.ConfigData.Priority);
            RegistBuffEffectRequest(newHandle, EffectPhase.StackChanged, newBuff.ConfigData.Priority, newBuff.RunTimeData.Stack);
            onAddBuff?.Invoke(newBuff, runTimeData);
        }
        public void RemoveBuffStack(int configId, GameObject source, int stackCount = 1,
            Action<Buff, int> onRemove = null)
        {
            if (!TryGetBuff(configId, source, out Buff buff))
            {
                Debug.LogWarning($"{gameObject.name} BuffHandler RemoveBuffStack Warning:Cant Found Buff ConfigID:{configId}, Source:{source?.name}");
                return;
            }
            int handle = buff.RunTimeData.RuntimeHandle;
            switch (buff.ConfigData.BuffType)
            {
                case BuffInstanceType.normal:
                    {
                        if (!string.IsNullOrEmpty(buff.ConfigData.BuffStackDownStrategyID)
                            && BuffStackStrategyRegister.Instance.TryGet(buff.ConfigData.BuffStackDownStrategyID, out IBuffStackStrategy strategy)
                            && strategy is IBuffStackDownStrategy buffStrategy)
                            buffStrategy.Apply(buff, stackCount);
                        else
                            buff.DownBuffStack(stackCount);
                        break;
                    }
                case BuffInstanceType.parallel:
                    {
                        if (!string.IsNullOrEmpty(buff.ConfigData.ParallelBuffStackDownStrategyID)
                            && BuffStackStrategyRegister.Instance.TryGet(buff.ConfigData.ParallelBuffStackDownStrategyID, out IBuffStackStrategy strategy)
                            && buff is ParallelBuff pBuff
                            && strategy is IParallelBuffStackDownStrategy parallelBuffStrategy)
                            parallelBuffStrategy.Apply(pBuff, stackCount);
                        else
                            buff.DownBuffStack(stackCount);
                        break;
                    }
                default:
                    buff.DownBuffStack(stackCount);
                    break;
            }
            onRemove?.Invoke(buff, stackCount);
            RegistBuffEffectRequest(handle, EffectPhase.StackChanged, buff.ConfigData.Priority, -stackCount);
            if (buff.IsCompletelyOver)
                QueueRemoveOnce(handle, buff);
        }
        public void ClearBuff(int configId, GameObject source)
        {
            if (!TryGetBuff(configId, source, out Buff buff))
                return;
            QueueRemoveOnce(buff.RunTimeData.RuntimeHandle, buff);
        }
        #endregion
        private void RegisterBuffEventListeners(int buffHandle, Buff buff)
        {
            if (buff is not IBuffEventSubscriber s) return;
            foreach (var listener in s.GetEventListeners())
                _router.Register(buffHandle, listener);
        }
        private void UnregisterBuffEventListeners(int buffHandle) => _router.UnregisterAll(buffHandle);
        private void QueueRemoveOnce(int handle, Buff buff)
        {
            _timeOutBuff.Add(handle);
            if (!buff.RunTimeData.RemoveQueued)
            {
                buff.RunTimeData.RemoveQueued = true;
                RegistBuffEffectRequest(handle, EffectPhase.Remove, buff.ConfigData.Priority);
            }
        }
        public void Raise<TEvent>(in TEvent e) where TEvent : struct, IGameEvent => _router.Raise(in e);
        public void RegistBuffEffectRequest(int buffHandle, EffectPhase effectPhase, int priority, int stackDelta = 0, object ev = null)
        {
            BuffEffectRequest request = new(this, buffHandle, effectPhase, priority, NextSequence(), stackDelta, ev);
            int index = _pendingEffects.BinarySearch(request);
            _pendingEffects.Insert(index < 0 ? ~index : index, request);
        }
        internal void ExecuteEffect(in BuffEffectRequest req)
        {
            if (!_buffs.TryGetValue(req.BuffHandle, out var buff)) return; // 已移除则忽略
            var ctx = new BuffContext(this, buff, buff.RunTimeData, req.EventBoxed);

            var effect = buff.ConfigData.BuffEffect; // 现在是 BuffEffect
            if (effect == null) return;
            switch (req.Phase)
            {
                case EffectPhase.Apply: effect.OnApply(in ctx); break;
                case EffectPhase.Refresh: effect.OnRefresh(in ctx); break;
                case EffectPhase.StackChanged: effect.OnStackChanged(in ctx, req.StackDelta); break;
                case EffectPhase.Tick: effect.OnTick(in ctx); break;
                case EffectPhase.Event: effect.OnEvent(in ctx); break;
                case EffectPhase.Remove: effect.OnRemove(in ctx); buff.RunTimeData.RemoveQueued = false; break;
            }
        }
        #region 生命周期
        private void Awake() => _router = new(this);
        private void Update() => UpdateBuffState();
        private void LateUpdate()
        {
            FlushEffects();
            if (_timeOutBuff.Count > 0)
            {
                foreach (int handle in _timeOutBuff)
                {
                    if (_buffs.TryGetValue(handle, out Buff buff))
                    {
                        _buffs.Remove(handle);
                        if (_lookupByHandle.TryGetValue(handle, out BuffLookupKey lookupKey))
                        {
                            _lookupByHandle.Remove(handle);
                            _handleByLookup.Remove(lookupKey);
                        }
                        UnregisterBuffEventListeners(handle);
                        buff.EndBuff();
                        BuffRuntimeDataFactory.Release(buff.RunTimeData);
                        buff.RunTimeData = null;
                    }
                }
                _timeOutBuff.Clear();
            }
        }
        private void FlushEffects()
        {
            if (_pendingEffects.Count == 0) return;
            var temp = _executingEffects;
            _executingEffects = _pendingEffects;
            _pendingEffects = temp;
            for (int i = 0; i < _executingEffects.Count; i++)
                _executingEffects[i].Invoke();
            _executingEffects.Clear();
        }
        private void UpdateBuffState()
        {
            float now = Time.time;
            foreach (var pair in _buffs)
            {
                int id = pair.Key;
                Buff buff = pair.Value;
                buff.RunTimeData.RunTime += Time.deltaTime;
                bool isParallelBuff = buff is ParallelBuff;
                if (isParallelBuff)
                {
                    ParallelBuff pb = buff as ParallelBuff;
                    int expired = pb.ExpireDueStacks(now);
                    if (expired > 0)
                    {
                        RegistBuffEffectRequest(id, EffectPhase.StackChanged, buff.ConfigData.Priority, -expired);
                        if (buff.RunTimeData.Stack <= 0) // 完全结束
                        {
                            QueueRemoveOnce(id, buff);
                            continue;
                        }
                    }
                }
                while (buff.ConfigData.BuffTriggerType == BuffTriggerType.Tick &&
                    buff.RunTimeData.RunTime < buff.RunTimeData.ActualDuration &&
                    buff.ConfigData.TickTime > 0 &&
                    (int)(buff.RunTimeData.RunTime / buff.ConfigData.TickTime) >= buff.RunTimeData.Ticks)
                {
                    RegistBuffEffectRequest(id, EffectPhase.Tick, buff.ConfigData.Priority);
                    buff.RunTimeData.Ticks++;
                }
                if (buff.ConfigData.BuffType == BuffInstanceType.normal)
                {
                    if (buff.IsCompletelyOver && !isParallelBuff)
                    {
                        _timeOutBuff.Add(id);
                        if (!buff.RunTimeData.RemoveQueued)
                        {
                            RegistBuffEffectRequest(id, EffectPhase.StackChanged, buff.ConfigData.Priority, buff.RunTimeData.Stack);
                            RegistBuffEffectRequest(id, EffectPhase.Remove, buff.ConfigData.Priority);
                            buff.RunTimeData.RemoveQueued = true;
                        }
                        continue;
                    }
                    if (buff.IsStackOver)
                    {
                        int before = buff.RunTimeData.Stack;
                        if (!string.IsNullOrEmpty(buff.ConfigData.BuffStackDownStrategyID) &&
                            BuffStackStrategyRegister.Instance.TryGet(buff.ConfigData.BuffStackDownStrategyID, out IBuffStackStrategy buffStackStrategy))
                            (buffStackStrategy as IBuffStackDownStrategy)?.Apply(buff);
                        else
                            buff.DownBuffStack();
                        int delta = buff.RunTimeData.Stack - before;
                        if (delta != 0)
                            RegistBuffEffectRequest(id, EffectPhase.StackChanged, buff.ConfigData.Priority, delta);
                    }
                    if (buff.IsCompletelyOver && !isParallelBuff)
                    {
                        _timeOutBuff.Add(id);
                        if (!buff.RunTimeData.RemoveQueued)
                        {
                            RegistBuffEffectRequest(id, EffectPhase.StackChanged, buff.ConfigData.Priority, buff.RunTimeData.Stack);
                            RegistBuffEffectRequest(id, EffectPhase.Remove, buff.ConfigData.Priority);
                            buff.RunTimeData.RemoveQueued = true;
                        }
                    }
                }
            }
        }
        #endregion
    }
    public enum EffectPhase { Apply, Refresh, StackChanged, Tick, Event, Remove }
    public readonly struct BuffEffectRequest : IComparable<BuffEffectRequest>
    {
        public readonly int BuffHandle;
        public readonly EffectPhase Phase;
        public readonly int Priority;
        public readonly int StackDelta;
        public readonly object EventBoxed; // 仅 Phase.Event 用
        private readonly BuffHandler _handler;
        private readonly long Sequence;
        public BuffEffectRequest(BuffHandler handler, int buffHandle, EffectPhase phase, int priority, long sequence, int stackDelta = 0, object ev = null)
     => (_handler, BuffHandle, Phase, Priority, StackDelta, EventBoxed, Sequence) = (handler, buffHandle, phase, priority, stackDelta, ev, sequence);
        public void Invoke() => _handler.ExecuteEffect(this);
        public int CompareTo(BuffEffectRequest other)
        {
            int compare = Priority.CompareTo(other.Priority);
            if (compare != 0) return compare;
            compare = Phase.CompareTo(other.Phase);
            if (compare != 0) return compare;
            compare = BuffHandle.CompareTo(other.BuffHandle);
            if (compare != 0) return compare;
            compare = Sequence.CompareTo(other.Sequence);
            return compare;
        }
    }
}
