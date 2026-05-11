/*
 * 文件说明：IEntityPrefabComponentInfo 提供 Prefab 组件类型查询能力，用于 DefinitionSO 与 PrefabSO 的匹配校验。
 */

using System;
using System.Collections.Generic;

namespace ECSFrameWork
{

/// <summary>
/// 可选的 Prefab 组件类型信息接口。
/// 实现该接口后，业务工厂可以在创建前检查 DefinitionSO 启用的组件是否已存在于 BasePrefab 中。
/// </summary>
public interface IEntityPrefabComponentInfo
{
    /// <summary>判断 Prefab 是否包含指定组件类型。</summary>
    bool HasComponent(Type componentType);

    /// <summary>把 Prefab 中包含的组件类型填充到外部 List 中，并返回填充数量。</summary>
    int FillComponentTypes(List<Type> results);
}

}
