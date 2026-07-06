using UnityEngine;

namespace Contracts
{
    public interface IObjectPoolFacade
    {
        GameObject GetWorldView(
            int prefabId,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null);

        GameObject GetUIView(
            int prefabId,
            RectTransform parent,
            Vector2 anchoredPosition);

        GameObject GetEffectView(
            int prefabId,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null);

        void Release(GameObject instance);
    }
}