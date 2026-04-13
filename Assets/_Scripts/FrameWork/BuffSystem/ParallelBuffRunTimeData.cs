using PoolSystem;
using UnityEngine;
using Utility;
namespace BuffSystem
{
    /// <summary>
    /// 并行叠层：每层独立计时。用 Deque 存每层到期的绝对时间
    /// </summary>
    public class ParallelBuffRunTimeData : BuffRuntimeData, IReference<ParallelBuffRunTimeData>
    {
        private Deque<float> _expiries;
        public ParallelBuffRunTimeData() => _expiries = new();
        public override void Init(GameObject sourse, GameObject target, int stack)
        {
            base.Init(sourse, target, stack);
            _expiries.Clear();
        }
        public override float ActualDuration
        {
            get
            {
                if (_expiries.Count == 0) return 0f;
                // 返回剩余时间
                return Mathf.Max(0f, _expiries.PeekHead() - Time.time);
            }
            set { }
        }
        public void InitExpiries(float now, float duration)
        {
            _expiries.Clear();
            if (Stack <= 0) { Stack = 0; return; }
            for (int i = 0; i < Stack; i++)
                _expiries.PushTail(now + duration);
        }

        public int AddStacks(float now, float duration, int count, bool unlimited, int maxStack)
        {
            if (count <= 0) return 0;
            int canAdd = count;
            if (!unlimited && maxStack > 0)
            {
                int remain = Mathf.Max(0, maxStack - Stack);
                canAdd = Mathf.Min(count, remain);
            }
            for (int i = 0; i < canAdd; i++)
                _expiries.PushTail(now + duration);
            Stack += canAdd;
            return canAdd;
        }
        public int ExpireDue(float now)
        {
            int expired = 0;
            while (_expiries.Count > 0 && _expiries.PeekHead() <= now)
            {
                _expiries.PopHead();
                expired++;
            }
            if (expired > 0)
                Stack = Mathf.Max(0, Stack - expired);
            return expired;
        }
        public int RemoveStacks(int count)
        {
            if (count <= 0 || _expiries.Count == 0) return 0;
            int removed = 0;
            while (removed < count && _expiries.Count > 0)
            {
                _expiries.PopHead();
                removed++;
            }
            if (removed > 0)
                Stack = Mathf.Max(0, Stack - removed);
            return removed;
        }
        public int RemoveEarliestStacks(int count)
        {
            if (count <= 0 || _expiries.Count == 0) return 0;
            int removed = 0;
            while (removed < count && _expiries.Count > 0)
            {
                _expiries.PopHead();
                removed++;
            }
            if (removed > 0)
                Stack = Mathf.Max(0, Stack - removed);
            return removed;
        }
        public int RemoveLatestStacks(int count)
        {
            if (count <= 0 || _expiries.Count == 0) return 0;
            int removed = 0;
            while (removed < count && _expiries.Count > 0)
            {
                _expiries.PopTail();
                removed++;
            }
            if (removed > 0)
                Stack = Mathf.Max(0, Stack - removed);
            return removed;
        }
        public int RefreshEarliestStacks(float now, float duration, int count)
        {
            if (count <= 0 || _expiries.Count == 0) return 0;
            int refreshed = 0;
            int actual = Mathf.Min(count, _expiries.Count);
            for (int i = 0; i < actual; i++)
            {
                _expiries.PopHead();
                refreshed++;
            }
            for (int i = 0; i < refreshed; i++)
                _expiries.PushTail(now + duration);
            return refreshed;
        }
        public int RefreshAllStacks(float now, float duration)
        {
            int count = _expiries.Count;
            if (count == 0) return 0;
            _expiries.Clear();
            for (int i = 0; i < Stack; i++)
                _expiries.PushTail(now + duration);
            return count;
        }

        public bool HasAnyExpiry => _expiries.Count > 0;
        public float NextExpiry => _expiries.Count > 0 ? _expiries.PeekHead() : float.PositiveInfinity;
        #region IReference
        int IReference.IndexInReferencePool { get; set; }
        public new IReference GetNewInstance() => new ParallelBuffRunTimeData();
        public override void OnRecycle()
        {
            base.OnRecycle();
            _expiries.Clear();
        }
        public override void Dispose()
        {
            OnRecycle();
            _expiries = null;
        }
        #endregion
    }
}
