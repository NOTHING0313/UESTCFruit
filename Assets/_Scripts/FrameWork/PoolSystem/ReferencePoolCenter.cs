using UnityEngine;
using Utility;
using System;
using System.Collections.Generic;
namespace PoolSystem
{
    public sealed class ReferencePoolCenter : Singleton<ReferencePoolCenter>
    {
        private Dictionary<Type, ReferencePool> _referencePools = new();
        /// <summary>
        /// 获取池化对象
        /// </summary>
        /// <typeparam name="TReference">
        /// 池化对象类型
        /// </typeparam>
        /// <returns></returns>
        public TReference GetReference<TReference>() where TReference : IReference<TReference>, new()
        {
            Type type = typeof(TReference);
            if (!_referencePools.ContainsKey(type))
            {
                ReferencePool referencePool = new ReferencePool();
                referencePool.Init<TReference>();
                _referencePools.Add(type, referencePool);
            }
            if (_referencePools.TryGetValue(type, out ReferencePool pool) && pool != null)
                return pool.GetReference<TReference>();
            else
            {
                Debug.Log($"ReferencePoolCenter GetReference Error:Cant Get Pool");
                return default;
            }
        }
        /// <summary>
        /// 归还池化对象
        /// </summary>
        /// <typeparam name="TReference">
        /// 池化对象类型
        /// </typeparam>
        /// <param name="reference"></param>
        public void ReleaseReference<TReference>(TReference reference) where TReference : IReference<TReference>, new()
        {
            Type type = typeof(TReference);
            if (!_referencePools.TryGetValue(type,out ReferencePool pool)||pool==null)
            {
                Debug.LogError($"ReferencePool ReleaseReference Error:Cant Find ReferencePool");
                return;
            }
            pool.ReleaseReference(reference);
        }
        public void OnDestroy()
        {
            foreach (var temp in _referencePools.Values)
                temp?.OnDestroy();
            _referencePools.Clear();
            _referencePools = null;
        }
    }
}
