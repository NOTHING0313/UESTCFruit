using Contracts;
using UnityEngine;

namespace View
{
    /// <summary>
    /// 对象池门面空壳。
    /// 后续可以在这里接入项目已有的 GameObjectPoolCenter / UIPool，统一给表现层取用对象。
    /// </summary>
    public sealed class ObjectPoolFacade : IObjectPoolFacade
    {
        public GameObject GetWorldView(int prefabId, Vector3 position, Quaternion rotation, Transform parent = null) => null;
        public GameObject GetUIView(int prefabId, RectTransform parent, Vector2 anchoredPosition) => null;
        public void Release(GameObject instance) { }
    }
}
