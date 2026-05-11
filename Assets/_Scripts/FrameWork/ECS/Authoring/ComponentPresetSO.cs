/*
 * 文件说明：ComponentPresetSO 是 Unity Authoring 层的组件预设基类，用于把 Inspector 配置转换为 ECS Component。
 */

using System;
using UnityEngine;

namespace ECSFrameWork
{

/// <summary>
/// Unity 组件预设基类。
/// 每个子类对应一种 ECS Component，负责把 ScriptableObject 中的字段写入指定 Entity。
/// </summary>
public abstract class ComponentPresetSO : ScriptableObject
{
    /// <summary>当前预设对应的 ECS 组件类型。</summary>
    public abstract Type ComponentType { get; }

    /// <summary>把当前预设写入指定 Entity。</summary>
    public abstract void Apply(World world, Entity entity);

    /// <summary>编辑器或运行时校验当前预设配置。</summary>
    public virtual void Validate(EntityDefinitionValidationResult result, string ownerName)
    {
    }
}

}
