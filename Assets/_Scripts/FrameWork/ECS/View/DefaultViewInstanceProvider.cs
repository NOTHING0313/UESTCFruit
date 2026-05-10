/*
 * 文件说明：DefaultViewInstanceProvider 是 ViewManager 的默认实例提供器，直接使用 Instantiate / Destroy。
 */

using UnityEngine;

/// <summary>
/// 默认 View 实例提供器，直接使用 Unity Instantiate / Destroy。
/// </summary>
public sealed class DefaultViewInstanceProvider : IViewInstanceProvider
{
    /// <summary>直接实例化 Prefab。</summary>
    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return null;

        GameObject instance = Object.Instantiate(prefab, position, rotation);
        instance.SetActive(true);
        return instance;
    }

    /// <summary>直接销毁实例。</summary>
    public void Release(GameObject instance)
    {
        if (instance == null)
            return;

        Object.Destroy(instance);
    }

    /// <summary>默认 Provider 不持有额外资源。</summary>
    public void Clear()
    {
    }
}
