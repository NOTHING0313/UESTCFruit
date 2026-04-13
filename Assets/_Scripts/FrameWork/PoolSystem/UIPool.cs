using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace PoolSystem
{
    public sealed class UIPool
    {
        private readonly int _poolID;
        private readonly GameObject _prefab;
        private readonly RectTransform _root;
        private readonly Stack<GameObject> _available = new();
        private readonly HashSet<GameObject> _inUse = new();
        public int PoolID => _poolID;
        public GameObject Prefab => _prefab;
        public RectTransform Root => _root;
        public UIPool(GameObject prefab, int poolID, RectTransform root, int initialCapacity)
        {
            _prefab = prefab;
            _poolID = poolID;
            _root = root;
            Expand(initialCapacity);
        }
        public void Expand(int count)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject go = UnityEngine.Object.Instantiate(_prefab, _root);
                go.SetActive(false);
                PoolItem item = go.GetComponent<PoolItem>();
                if (item == null)
                    item = go.AddComponent<PoolItem>();
                item.PoolID = _poolID;
                item.PrefabInstanceID = _prefab.GetInstanceID();
                item.IsInPool = true;
                RectTransform rt = go.transform as RectTransform;
                if (rt != null)
                {
                    rt.anchoredPosition = Vector2.zero;
                    rt.localRotation = Quaternion.identity;
                    rt.localScale = Vector3.one;
                }
                _available.Push(go);
            }
        }
        public GameObject Get(RectTransform parent,Vector2 anchoredPosition,Action<GameObject> onBeforeGet = null,bool worldPositionStays = false)
        {
            if (_available.Count == 0)
                Expand(1);
            GameObject go = _available.Pop();
            _inUse.Add(go);
            RectTransform rt = go.transform as RectTransform;
            if (rt == null)
            {
                Debug.LogError("UIPool Get Error: object is not RectTransform.");
                return go;
            }
            rt.SetParent(parent, worldPositionStays);
            rt.anchoredPosition = anchoredPosition;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            PoolItem item = go.GetComponent<PoolItem>();
            item.IsInPool = false;
            onBeforeGet?.Invoke(go);
            InvokePoolableOnGet(go);
            go.SetActive(true);
            return go;
        }
        public void Release(GameObject instance, Action<GameObject> onBeforeRelease = null)
        {
            if (instance == null)
            {
                Debug.LogError("UIPool Release Error: Instance Is Null.");
                return;
            }
            PoolItem item = instance.GetComponent<PoolItem>();
            if (item == null || item.PoolID != _poolID)
            {
                Debug.LogError("UIPool Release Error: Instance Does Not Belong To This Pool.");
                return;
            }
            if (item.IsInPool)
            {
                Debug.LogError("UIPool Release Error: Repeated Release Detected.");
                return;
            }
            onBeforeRelease?.Invoke(instance);
            InvokePoolableOnRelease(instance);
            instance.SetActive(false);
            RectTransform rt = instance.transform as RectTransform;
            if (rt != null)
            {
                rt.SetParent(_root, false);
                rt.anchoredPosition = Vector2.zero;
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one;
            }
            else
                instance.transform.SetParent(_root, false);
            item.IsInPool = true;
            _inUse.Remove(instance);
            _available.Push(instance);
        }
        public void Clear()
        {
            foreach (GameObject go in _available)
                if (go != null)
                    UnityEngine.Object.Destroy(go);
            foreach (GameObject go in _inUse)
                if (go != null)
                    UnityEngine.Object.Destroy(go);
            _available.Clear();
            _inUse.Clear();
        }
        private void InvokePoolableOnGet(GameObject go)
        {
            foreach (var poolable in go.GetComponentsInChildren<IPoolable>(true))
                poolable.OnGetFromPool();
        }
        private void InvokePoolableOnRelease(GameObject go)
        {
            foreach (var poolable in go.GetComponentsInChildren<IPoolable>(true))
                poolable.OnReleaseToPool();
        }
    }
}