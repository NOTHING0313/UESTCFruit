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
            GameObject instance = GameObjectPoolCenter.Instance.GetInstance(prefab, position, rotation);
            if (instance == null) return null;
            instance.SetActive(true);
            if (_worldViewRoot != null)
                instance.transform.SetParent(_worldViewRoot, false);
            // 移除 ViewAutoRecycle 的挂载，正常回收由 ViewDestroySystem 负责
            return instance;
        }

        void IViewInstanceProvider.Release(GameObject instance)
        {
            if (instance != null)
                GameObjectPoolCenter.Instance.Release(instance);
        }

        void IViewInstanceProvider.Clear() { }
    }
    // 删除整个 ViewAutoRecycle 类定义
}