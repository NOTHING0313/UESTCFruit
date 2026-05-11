/*
 * 文件说明：GameplayEntityDefinitionSO 是通用业务实体配置，负责引用 BasePrefab 并保存组件配置覆盖值。
 */

using UnityEngine;

namespace ECSFrameWork
{

/// <summary>
/// 通用业务实体 DefinitionSO。
/// 用于配置单位、建筑、子弹、掉落物等业务实体的静态数据和组件覆盖配置。
/// </summary>
[CreateAssetMenu(fileName = "GameplayEntityDefinition", menuName = "ECSFrameWork/Gameplay Entity Definition")]
public class GameplayEntityDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string definitionID;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;

    [Header("ECS")]
    [SerializeField] private EntityPrefabSO basePrefab;

    [Header("Components")]
    [SerializeField] private GameplayComponentConfigSet components;

    /// <summary>业务配置 ID，适合存档、网络、配置表引用。</summary>
    public string DefinitionID => definitionID;

    /// <summary>显示名称。</summary>
    public string DisplayName => displayName;

    /// <summary>显示图标。</summary>
    public Sprite Icon => icon;

    /// <summary>基础 ECS Prefab。</summary>
    public EntityPrefabSO BasePrefab => basePrefab;

    /// <summary>组件覆盖配置集合。</summary>
    public GameplayComponentConfigSet Components => components;

    /// <summary>用于测试或运行时动态配置 DefinitionSO。</summary>
    public void Configure(string definitionID, string displayName, EntityPrefabSO basePrefab, in GameplayComponentConfigSet components)
    {
        this.definitionID = definitionID;
        this.displayName = displayName;
        this.basePrefab = basePrefab;
        this.components = components;
    }

    /// <summary>校验当前 DefinitionSO。</summary>
    public EntityDefinitionValidationResult ValidateDefinition(EntityDefinitionMismatchPolicy mismatchPolicy = EntityDefinitionMismatchPolicy.WarnAndAdd)
    {
        EntityDefinitionValidationResult result = new EntityDefinitionValidationResult();
        ValidateDefinition(result, mismatchPolicy);
        return result;
    }

    /// <summary>校验当前 DefinitionSO，并把结果写入外部 result。</summary>
    public void ValidateDefinition(EntityDefinitionValidationResult result, EntityDefinitionMismatchPolicy mismatchPolicy = EntityDefinitionMismatchPolicy.WarnAndAdd)
    {
        if (result == null)
            return;

        string ownerName = string.IsNullOrEmpty(name) ? nameof(GameplayEntityDefinitionSO) : name;

        if ((string.IsNullOrEmpty(definitionID) || definitionID.Trim().Length == 0))
            result.AddWarning($"[{ownerName}] definitionID is empty.");

        if (basePrefab == null)
        {
            result.AddError($"[{ownerName}] BasePrefab is null.");
            components.Validate(result, ownerName);
            return;
        }

        basePrefab.Validate(result);
        components.Validate(result, ownerName);
        ValidateMismatch(result, ownerName, mismatchPolicy);
    }

    /// <summary>校验 DefinitionSO 启用组件与 BasePrefab 组件集合之间的关系。</summary>
    private void ValidateMismatch(EntityDefinitionValidationResult result, string ownerName, EntityDefinitionMismatchPolicy mismatchPolicy)
    {
        if (result == null || basePrefab == null || mismatchPolicy == EntityDefinitionMismatchPolicy.AllowAdd)
            return;

        System.Collections.Generic.List<System.Type> enabledTypes = new System.Collections.Generic.List<System.Type>();
        components.FillEnabledComponentTypes(enabledTypes);

        for (int i = 0; i < enabledTypes.Count; i++)
        {
            System.Type type = enabledTypes[i];

            if (basePrefab.HasComponent(type))
                continue;

            string message = $"[{ownerName}] Definition enables {type.Name}, but BasePrefab [{basePrefab.Name}] does not contain it.";

            if (mismatchPolicy == EntityDefinitionMismatchPolicy.Reject)
                result.AddError(message);
            else
                result.AddWarning(message);
        }
    }

#if UNITY_EDITOR
    /// <summary>Inspector 配置变化时做轻量校验。</summary>
    private void OnValidate()
    {
        EntityDefinitionValidationResult result = ValidateDefinition();
        result.LogToUnity(this);
    }
#endif
}

}
