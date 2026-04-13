using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace PoolSystem
{
    public sealed class GameObjectPoolCenter : Singleton<GameObjectPoolCenter>
    {
        [SerializeField, LabelText("池子初始化个数")] 
        private int _defaultInitialCapacity = 8;
        [SerializeField, LabelText("UI池根节点")]
        private RectTransform _sceneUIRoot;
        private int _poolIDCounter = 0;
        private readonly Dictionary<int, GameObjectPool> _gameObjectPools = new();
        private readonly Dictionary<int, UIPool> _uiPools = new();
        private readonly Dictionary<int, int> _normalPrefabIdToPoolId = new();
        private readonly Dictionary<int, int> _uiPrefabIdToPoolId = new();
        private Transform _worldPoolRoot;
        private RectTransform _uiPoolRoot;
        protected override void Awake()
        {
            base.Awake();
            CreateRoots();
        }
        private void CreateRoots()
        {
            if (_worldPoolRoot == null)
            {
                GameObject go = new GameObject("WorldPoolRoot");
                go.transform.SetParent(transform, false);
                _worldPoolRoot = go.transform;
            }
            if (_uiPoolRoot == null)
            {
                if (_sceneUIRoot == null)
                {
                    Debug.LogError("GameObjectPoolCenter CreateRoots Error: SceneUIRoot Is Null.");
                    return;
                }
                GameObject go = new GameObject("UIPoolRoot", typeof(RectTransform));
                go.transform.SetParent(_sceneUIRoot, false);
                _uiPoolRoot = go.GetComponent<RectTransform>();
                _uiPoolRoot.anchorMin = Vector2.zero;
                _uiPoolRoot.anchorMax = Vector2.one;
                _uiPoolRoot.offsetMin = Vector2.zero;
                _uiPoolRoot.offsetMax = Vector2.zero;
                _uiPoolRoot.localScale = Vector3.one;
                _uiPoolRoot.localRotation = Quaternion.identity;
            }
        }
        public GameObject GetInstance(GameObject prefab, Vector3 worldPosition, Quaternion worldRotation, Transform parent = null, Action<GameObject> onBeforeSetActive = null, int initialCapacity = -1)
        {
            if (prefab == null)
            {
                Debug.LogError("GameObjectPoolCenter GetInstance Error: Prefab Is Null.");
                return null;
            }
            int prefabID = prefab.GetInstanceID();
            if (!_normalPrefabIdToPoolId.TryGetValue(prefabID, out int poolID))
            {
                poolID = _poolIDCounter++;
                int capacity = initialCapacity > 0 ? initialCapacity : _defaultInitialCapacity;
                GameObject rootGO = new GameObject($"Pool_Normal_{poolID}_{prefab.name}");
                rootGO.transform.SetParent(_worldPoolRoot, false);
                var pool = new GameObjectPool(prefab, poolID, rootGO.transform, capacity);
                _gameObjectPools.Add(poolID, pool);
                _normalPrefabIdToPoolId.Add(prefabID, poolID);
            }
            return _gameObjectPools[poolID].Get(worldPosition, worldRotation, parent, onBeforeSetActive);
        }

        public GameObject GetUIInstance(GameObject prefab, RectTransform parent, Vector2 anchoredPosition = default, Action<GameObject> onBeforeSetActive = null, int initialCapacity = -1)
        {
            if (prefab == null)
            {
                Debug.LogError("GameObjectPoolCenter GetUIInstance Error: Prefab Is Null.");
                return null;
            }
            if (parent == null)
            {
                Debug.LogError("GameObjectPoolCenter GetUIInstance Error: Parent Is Null.");
                return null;
            }
            int prefabID = prefab.GetInstanceID();
            if (!_uiPrefabIdToPoolId.TryGetValue(prefabID, out int poolID))
            {
                poolID = _poolIDCounter++;
                int capacity = initialCapacity > 0 ? initialCapacity : _defaultInitialCapacity;
                GameObject rootGO = new GameObject($"Pool_UI_{poolID}_{prefab.name}", typeof(RectTransform));
                rootGO.transform.SetParent(_uiPoolRoot, false);
                var pool = new UIPool(prefab, poolID, rootGO.GetComponent<RectTransform>(), capacity);
                _uiPools.Add(poolID, pool);
                _uiPrefabIdToPoolId.Add(prefabID, poolID);
            }
            return _uiPools[poolID].Get(parent, anchoredPosition, onBeforeSetActive, false);
        }

        public void Release(GameObject instance, Action<GameObject> onBeforeRelease = null)
        {
            if (instance == null)
            {
                Debug.LogError("GameObjectPoolCenter Release Error: Instance Is Null.");
                return;
            }
            PoolItem item = instance.GetComponent<PoolItem>();
            if (item == null)
            {
                Debug.LogError("GameObjectPoolCenter Release Error: PoolItem Not Found.");
                return;
            }
            int poolID = item.PoolID;
            if (_gameObjectPools.TryGetValue(poolID, out var gameObjectPool))
            {
                gameObjectPool.Release(instance, onBeforeRelease);
                return;
            }
            if (_uiPools.TryGetValue(poolID, out var uiPool))
            {
                uiPool.Release(instance, onBeforeRelease);
                return;
            }
            Debug.LogError($"GameObjectPoolCenter Release Error: PoolID {poolID} Not Found.");
        }
        public void ClearAllPools()
        {
            foreach (var pool in _gameObjectPools.Values)
                pool.Clear();
            foreach (var pool in _uiPools.Values)
                pool.Clear();
            _gameObjectPools.Clear();
            _uiPools.Clear();
            _normalPrefabIdToPoolId.Clear();
            _uiPrefabIdToPoolId.Clear();
        }
        private void OnDestroy() => ClearAllPools();
    }
}