using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace PoolSystem
{
    public sealed class GameObjectPool
    {
        private readonly int _poolID;
        private readonly GameObject _prefab;
        private readonly Transform _root;
        private readonly Stack<GameObject> _available = new();
        private readonly HashSet<GameObject> _inUse = new();
        public int PoolID => _poolID;
        public GameObject Prefab => _prefab;
        public Transform Root => _root;
        public int AvailableCount => _available.Count;
        public int InUseCount => _inUse.Count;
        public GameObjectPool(GameObject prefab, int poolID, Transform root, int initialCapacity)
        {
            _prefab = prefab;
            _poolID = poolID;
            _root = root;
            Expand(initialCapacity);
        }
        public void Expand(int count = 1)
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
                _available.Push(go);
            }
        }
        public GameObject Get(Vector3 worldPosition, Quaternion worldRotation, Transform parent = null, Action<GameObject> onBeforeGet = null)
        {
            if (_available.Count == 0)
                Expand();
            GameObject go = _available.Pop();
            _inUse.Add(go);
            Transform t = go.transform;
            t.SetParent(parent, true);
            t.position = worldPosition;
            t.rotation = worldRotation;
            t.localScale = Vector3.one;
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
                Debug.LogError("GameObjectPool Release Error: Instance Is Null.");
                return;
            }

            PoolItem item = instance.GetComponent<PoolItem>();
            if (item == null || item.PoolID != _poolID)
            {
                Debug.LogError("GameObjectPool Release Error: Instance Does Not Belong To This Pool.");
                return;
            }

            if (item.IsInPool)
            {
                Debug.LogError("GameObjectPool Release Error: Repeated Release Detected.");
                return;
            }
            onBeforeRelease?.Invoke(instance);
            InvokePoolableOnRelease(instance);
            instance.SetActive(false);
            Transform t = instance.transform;
            t.SetParent(_root, false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
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