using Contracts;
using UnityEngine;

namespace View
{
    /// <summary>
    /// 对象池门面空壳（4号实现，后续接入 GameObjectPoolCenter）。
    /// </summary>
    public sealed class ObjectPoolFacade : IObjectPoolFacade
    {
        public GameObject GetWorldView(int prefabId, Vector3 position, Quaternion rotation, Transform parent = null) => null;
        public GameObject GetUIView(int prefabId, RectTransform parent, Vector2 anchoredPosition) => null;
        public void Release(GameObject instance) { }
    }
}