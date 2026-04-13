using System.Collections.Generic;
using UnityEngine;
using System;

namespace PoolSystem
{
    public sealed class ReferencePool
    {
        public const int DEFAULT_REFERENCE_COUNT = 64;
        private Stack<int> _freeReferences;
        private List<IReference> _references;
        private IReference _referenceTemplate;
        private Type _referenceType;
        private int _totalReferencesCount;
        private bool isDistroy;
        public int ReferencesCount => _totalReferencesCount;
        public int FreeReferencesCount => _freeReferences.Count;
        public Type ReferenceType => _referenceType;
        /// <summary>
        /// 初始化池子
        /// </summary>
        /// <typeparam name="TReference"></typeparam>
        public void Init<TReference>() where TReference : IReference, new()
        {
            _referenceType = typeof(TReference);
            _referenceTemplate = new TReference();
            _freeReferences = new();
            _references = new();
            Expand();
        }
        /// <summary>
        /// 扩大池子
        /// </summary>
        public void Expand()
        {
            _references.Capacity += DEFAULT_REFERENCE_COUNT;
            for(int i = 0; i < DEFAULT_REFERENCE_COUNT; i++)
            {
                IReference temp = _referenceTemplate.GetNewInstance();
                temp.IndexInReferencePool = _totalReferencesCount;
                _references.Add(temp);
                _freeReferences.Push(_totalReferencesCount++);
            }
        }
        /// <summary>
        /// 获取池化对象
        /// </summary>
        /// <typeparam name="TReference"></typeparam>
        /// <returns></returns>
        public TReference GetReference<TReference>()where TReference:IReference<TReference>,new()
        {
            if (isDistroy)
            {
                Debug.LogWarning($"ReferencePool ReleaseReference Warning:Cant Call This When It Is Distroyed");
                return default;
            }
            if (_referenceType != typeof(TReference))
            {
                Debug.LogError($"ReferencePool GetReference Error:Wrong Type,Expect{_referenceType.Name},Got{typeof(TReference).Name}");
                return default;
            }
            if (_freeReferences.Count == 0)
                Expand();
            return (TReference)_references[_freeReferences.Pop()];
        }
        /// <summary>
        /// 回收池化对象
        /// </summary>
        /// <typeparam name="TReference"></typeparam>
        /// <param name="reference"></param>
        public void ReleaseReference<TReference>(TReference reference)where TReference : IReference<TReference>, new()
        {
            if (isDistroy)
            {
                Debug.LogWarning($"ReferencePool ReleaseReference Warning:Cant Call This When It Is Distroyed");
                return;
            }
            if (_referenceType != typeof(TReference))
            {
                Debug.LogError($"ReferencePool ReleaseReference Error:Wrong Type,Expect{_referenceType.Name},Got{typeof(TReference).Name}");
                return;
            }
            if (reference == null)
            {
                Debug.LogError($"ReferencePool ReleaseReference Error:Reference Is Null");
                return;
            }
            int index = reference.IndexInReferencePool;
            if (index < 0 || index >= ReferencesCount)
            {
                Debug.LogError($"ReferencePool ReleaseReference Error:{index} Is Out Of Range");
                return;
            }
            if (_freeReferences.Contains(index))
            {
                Debug.LogError($"ReferencePool ReleaseReference Error:{index} Is In FreeReferences,But You Still Add It");
                return;
            }
            reference.OnRecycle();
            _freeReferences.Push(index);
        }
        /// <summary>
        /// 摧毁对象池
        /// </summary>
        public void OnDestroy()
        {
            isDistroy = true;
            foreach (var reference in _references)
            {
                reference.Dispose();
            }
            _referenceTemplate.Dispose();
            _references.Clear();
            _freeReferences.Clear();
            _references = null;
            _referenceTemplate = null;
            _freeReferences = null;
            _referenceType = null;
        }
    }
}
