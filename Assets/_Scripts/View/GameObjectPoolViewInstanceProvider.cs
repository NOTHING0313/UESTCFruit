using ECSFrameWork;   // IViewInstanceProvider 位于此命名空间
using UnityEngine;
using PoolSystem;

namespace View
{
    /// <summary>
    /// 将现有对象池桥接到 ECS 的 IViewInstanceProvider。
    /// </summary>
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

            // 自动回收组件
            if (!instance.TryGetComponent<ViewAutoRecycle>(out _))
                instance.AddComponent<ViewAutoRecycle>().Initialize(this);

            return instance;
        }

        void IViewInstanceProvider.Release(GameObject instance)
        {
            if (instance != null)
                GameObjectPoolCenter.Instance.Release(instance);
        }

        void IViewInstanceProvider.Clear() { }
    }

    internal class ViewAutoRecycle : MonoBehaviour
    {
        private GameObjectPoolViewInstanceProvider _provider;

        public void Initialize(GameObjectPoolViewInstanceProvider provider) => _provider = provider;

        private void OnDestroy()
        {
            if (_provider != null)
                ((IViewInstanceProvider)_provider).Release(gameObject);
        }
    }
}