using Contracts;
using PoolSystem;
using UnityEngine;

namespace View
{
    public sealed class ObjectPoolFacade : IObjectPoolFacade
    {
        private readonly ViewPrefabCatalog _catalog;

        public ObjectPoolFacade(ViewPrefabCatalog catalog)
        {
            _catalog = catalog;
        }

        public GameObject GetWorldView(
            int prefabId,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null)
        {
            if (_catalog == null || !_catalog.TryGetWorldPrefab(prefabId, out GameObject prefab))
                return null;

            return Spawn(prefab, position, rotation, parent);
        }

        public GameObject GetUIView(
            int prefabId,
            RectTransform parent,
            Vector2 anchoredPosition)
        {
            if (_catalog == null || !_catalog.TryGetUIPrefab(prefabId, out GameObject prefab))
                return null;

            GameObject instance = Spawn(prefab, Vector3.zero, Quaternion.identity, parent);
            if (instance == null)
                return null;

            if (instance.transform is RectTransform rect)
                rect.anchoredPosition = anchoredPosition;

            return instance;
        }

        public GameObject GetEffectView(
            int prefabId,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null)
        {
            if (_catalog == null || !_catalog.TryGetEffectPrefab(prefabId, out GameObject prefab))
                return null;

            return Spawn(prefab, position, rotation, parent);
        }

        public void Release(GameObject instance)
        {
            if (instance == null)
                return;

            GameObjectPoolCenter.Instance.Release(instance);
        }

        private static GameObject Spawn(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            Transform parent)
        {
            if (prefab == null)
                return null;

            GameObject instance = GameObjectPoolCenter.Instance.GetInstance(prefab, position, rotation);
            if (instance == null)
                return null;

            if (parent != null)
                instance.transform.SetParent(parent, false);

            instance.SetActive(true);
            return instance;
        }
    }
}