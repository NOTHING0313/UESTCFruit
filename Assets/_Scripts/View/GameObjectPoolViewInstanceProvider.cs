using ECSFrameWork;
using PoolSystem;
using UnityEngine;

namespace View
{
    public class GameObjectPoolViewInstanceProvider : IViewInstanceProvider
    {
        private readonly Transform _worldViewRoot;

        public GameObjectPoolViewInstanceProvider(Transform worldViewRoot = null)
        {
            _worldViewRoot = worldViewRoot;
        }

        GameObject IViewInstanceProvider.Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            GameObjectPoolCenter center=GameObjectPoolCenter.Instance;
            if (center == null || center.IsShuttingDown) return null;

            GameObject instance=center.GetInstance(prefab,position,rotation,_worldViewRoot);
            if (instance == null) return null;

            instance.SetActive(true);
            return instance;
        }

        void IViewInstanceProvider.Release(GameObject instance)
        {
            if (instance == null) return;

            GameObjectPoolCenter center=GameObjectPoolCenter.Instance;
            if (center == null || center.IsShuttingDown) return;

            center.Release(instance);
        }

        void IViewInstanceProvider.Clear() { }
    }
}
