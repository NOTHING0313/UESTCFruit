/*
 * 文件说明：IViewInstanceProvider 把 ViewManager 与具体 GameObject 创建 / 回收方式解耦。
 * 设计约束：ECS View 层只依赖该接口，不直接依赖 Instantiate、Destroy 或具体对象池实现。
 */

using UnityEngine;

namespace ECSFrameWork
{

/// <summary>
/// View 实例创建与释放接口。
/// </summary>
public interface IViewInstanceProvider
{
    /// <summary>创建或取出一个 View 实例。</summary>
    GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation);

    /// <summary>释放一个 View 实例。</summary>
    void Release(GameObject instance);

    /// <summary>清理 Provider 内部资源；默认情况下不应清空全局对象池。</summary>
    void Clear();
}

}
