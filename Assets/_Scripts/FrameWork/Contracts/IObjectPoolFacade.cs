using UnityEngine;

namespace Contracts
{
    /// <summary>
    /// 对象池门面接口（4号提供，自身使用）。
    /// 封装现有的 GameObjectPoolCenter，未来可切换池实现，不依赖具体池系统。
    /// </summary>
    public interface IObjectPoolFacade
    {
        GameObject GetWorldView(int prefabId, Vector3 position, Quaternion rotation, Transform parent = null);
        GameObject GetUIView(int prefabId, RectTransform parent, Vector2 anchoredPosition);
        void Release(GameObject instance);
    }
}