/*
 * 文件说明：ViewManager 负责 viewID 到 Unity GameObject / Transform 的映射，以及 View 生命周期管理。
 * 设计约束：ViewManager 只维护映射关系，具体创建 / 回收由 IViewInstanceProvider 决定。
 */

using System.Collections.Generic;
using UnityEngine;

namespace ECSFrameWork
{

/// <summary>
/// Unity View 对象管理器，负责 viewID 与 GameObject / Transform 映射。
/// </summary>
public sealed class ViewManager
{
    private sealed class ViewObject
    {
        public readonly GameObject gameObject;
        public readonly Transform transform;
        public readonly bool canRelease;

        public ViewObject(GameObject gameObject, bool canRelease)
        {
            this.gameObject = gameObject;
            transform = gameObject != null ? gameObject.transform : null;
            this.canRelease = canRelease;
        }
    }

    private int _nextViewID = 1;

    private readonly Dictionary<int, GameObject> _prefabs = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, ViewObject> _views = new Dictionary<int, ViewObject>();

    private IViewInstanceProvider _instanceProvider;

    public int PrefabCount => _prefabs.Count;
    public int ViewCount => _views.Count;

    /// <summary>使用默认 Instantiate / Destroy Provider 创建 ViewManager。</summary>
    public ViewManager() : this(new DefaultViewInstanceProvider())
    {
    }

    /// <summary>使用指定 Provider 创建 ViewManager，便于接入对象池。</summary>
    public ViewManager(IViewInstanceProvider instanceProvider)
    {
        _instanceProvider = instanceProvider ?? new DefaultViewInstanceProvider();
    }

    /// <summary>切换 View 实例提供器，通常应在 SpawnView 之前调用。</summary>
    public void SetInstanceProvider(IViewInstanceProvider instanceProvider)
    {
        if (instanceProvider == null)
            return;

        if (_views.Count > 0)
        {
            Debug.LogWarning("[ViewManager] Cannot switch instance provider while managed views exist. Clear or destroy views first.");
            return;
        }

        _instanceProvider = instanceProvider;
    }

    /// <summary>注册 prefabID 与 Prefab 的映射关系。</summary>
    public void RegisterPrefab(int prefabID, GameObject prefab)
    {
        if (prefabID <= 0 || prefab == null)
            return;

        _prefabs[prefabID] = prefab;
    }

    /// <summary>注册已经存在的场景对象 Transform，并返回对应的 viewID。</summary>
    public int Register(Transform transform, bool canRelease = false)
    {
        if (transform == null)
            return 0;

        int viewID = CreateViewID();
        _views[viewID] = new ViewObject(transform.gameObject, canRelease);
        return viewID;
    }

    /// <summary>根据 prefabID 创建或取出 View，并返回对应的 viewID。</summary>
    public int SpawnView(int prefabID, Vector3 position, Quaternion rotation)
    {
        if (!_prefabs.TryGetValue(prefabID, out GameObject prefab) || prefab == null)
            return 0;

        GameObject instance = _instanceProvider.Spawn(prefab, position, rotation);

        if (instance == null)
            return 0;

        instance.SetActive(true);
        return Register(instance.transform, true);
    }

    /// <summary>尝试根据 viewID 获取 Transform。</summary>
    public bool TryGetTransform(int viewID, out Transform transform)
    {
        if (_views.TryGetValue(viewID, out ViewObject view) && view != null && view.transform != null)
        {
            transform = view.transform;
            return true;
        }

        transform = null;
        return false;
    }

    /// <summary>注销 viewID，但不释放 GameObject。</summary>
    public bool Unregister(int viewID)
    {
        if (viewID <= 0)
            return false;

        return _views.Remove(viewID);
    }

    /// <summary>释放 viewID 对应的 GameObject，并移除映射；池化场景下语义是 Release。</summary>
    public bool DestroyView(int viewID)
    {
        if (viewID <= 0)
            return false;

        if (!_views.TryGetValue(viewID, out ViewObject view))
            return false;

        _views.Remove(viewID);

        if (view != null && view.canRelease && view.gameObject != null)
            _instanceProvider.Release(view.gameObject);

        return true;
    }

    /// <summary>清理所有 View 和 Prefab 映射；只释放由 ViewManager 创建且允许释放的 View。</summary>
    public void Clear()
    {
        foreach (KeyValuePair<int, ViewObject> pair in _views)
        {
            ViewObject view = pair.Value;

            if (view != null && view.canRelease && view.gameObject != null)
                _instanceProvider.Release(view.gameObject);
        }

        _views.Clear();
        _prefabs.Clear();
        _nextViewID = 1;
        _instanceProvider.Clear();
    }

    /// <summary>创建新的 View ID。</summary>
    private int CreateViewID()
    {
        int viewID = _nextViewID;
        _nextViewID++;
        return viewID;
    }
}

}
