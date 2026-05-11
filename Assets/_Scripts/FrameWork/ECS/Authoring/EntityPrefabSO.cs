/*
 * 文件说明：EntityPrefabSO 是 Unity Authoring 层的 ECS 实体模板，用于在 Inspector 中组合多个 ComponentPresetSO。
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ECSFrameWork
{

/// <summary>
/// Unity 版本 EntityPrefab。
/// 它保存一组 ComponentPresetSO，用于创建 Entity 时写入默认组件。
/// </summary>
[CreateAssetMenu(fileName = "EntityPrefab", menuName = "ECSFrameWork/Entity Prefab")]
public sealed class EntityPrefabSO : ScriptableObject, IEntityPrefab, IEntityPrefabComponentInfo
{
    [SerializeField] private string key;
    [SerializeField] private ComponentPresetSO[] componentPresets;

    /// <summary>Prefab 注册或调试用 Key。</summary>
    public string Key => key;

    /// <summary>Prefab 名称，优先使用 key，key 为空时使用 SO 名称。</summary>
    public string Name => string.IsNullOrEmpty(key) ? name : key;

    /// <summary>组件预设数量。</summary>
    public int ComponentCount => componentPresets == null ? 0 : componentPresets.Length;

    /// <summary>只读访问当前组件预设数组。</summary>
    public IReadOnlyList<ComponentPresetSO> ComponentPresets => componentPresets;

    /// <summary>用于测试或运行时动态配置 PrefabSO。</summary>
    public void Configure(string key, params ComponentPresetSO[] presets)
    {
        this.key = key;
        componentPresets = presets;
    }

    /// <summary>基于当前模板创建新 Entity，并写入全部预设组件。</summary>
    public Entity Create(World world)
    {
        if (world == null || world.IsDisposing())
            return Entity.Invalid;

        Entity entity = world.CreateEntity();

        if (!entity.IsValid)
            return Entity.Invalid;

        ApplyTo(world, entity);
        return entity;
    }

    /// <summary>把当前模板中的全部预设组件写入已有 Entity。</summary>
    public void ApplyTo(World world, Entity entity)
    {
        if (world == null || !entity.IsValid || !world.IsAlive(entity) || componentPresets == null)
            return;

        for (int i = 0; i < componentPresets.Length; i++)
        {
            ComponentPresetSO preset = componentPresets[i];

            if (preset == null)
                continue;

            preset.Apply(world, entity);
        }
    }

    /// <summary>判断模板中是否包含指定组件类型。</summary>
    public bool HasComponent(Type componentType)
    {
        if (componentType == null || componentPresets == null)
            return false;

        for (int i = 0; i < componentPresets.Length; i++)
        {
            ComponentPresetSO preset = componentPresets[i];

            if (preset != null && preset.ComponentType == componentType)
                return true;
        }

        return false;
    }

    /// <summary>把模板中的组件类型填充到外部 List，并返回填充数量。</summary>
    public int FillComponentTypes(List<Type> results)
    {
        if (results == null)
            return 0;

        results.Clear();

        if (componentPresets == null)
            return 0;

        for (int i = 0; i < componentPresets.Length; i++)
        {
            ComponentPresetSO preset = componentPresets[i];

            if (preset == null || preset.ComponentType == null)
                continue;

            results.Add(preset.ComponentType);
        }

        return results.Count;
    }

    /// <summary>校验当前 PrefabSO，结果写入外部 ValidationResult。</summary>
    public void Validate(EntityDefinitionValidationResult result)
    {
        if (result == null)
            return;

        if ((string.IsNullOrEmpty(key) || key.Trim().Length == 0))
            result.AddWarning($"[{Name}] EntityPrefabSO key is empty.");

        if (componentPresets == null || componentPresets.Length == 0)
        {
            result.AddWarning($"[{Name}] EntityPrefabSO has no ComponentPresetSO.");
            return;
        }

        HashSet<Type> types = new HashSet<Type>();

        for (int i = 0; i < componentPresets.Length; i++)
        {
            ComponentPresetSO preset = componentPresets[i];

            if (preset == null)
            {
                result.AddWarning($"[{Name}] ComponentPresetSO at index {i} is null.");
                continue;
            }

            if (!types.Add(preset.ComponentType))
                result.AddWarning($"[{Name}] Duplicate ComponentPresetSO type: {preset.ComponentType.Name}. Later preset will overwrite earlier component data.");

            preset.Validate(result, Name);
        }
    }

#if UNITY_EDITOR
    /// <summary>Inspector 配置变化时做轻量校验。</summary>
    private void OnValidate()
    {
        EntityDefinitionValidationResult result = new EntityDefinitionValidationResult();
        Validate(result);
        result.LogToUnity(this);
    }
#endif
}

}
