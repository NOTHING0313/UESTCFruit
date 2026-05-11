using System;
using UnityEngine;

namespace ECSFrameWork
{

/// <summary>PrefabViewRequestComponent 的 Unity 预设。</summary>
[CreateAssetMenu(fileName = "PrefabViewRequestPreset", menuName = "ECSFrameWork/Component Preset/Prefab View Request")]
public sealed class PrefabViewRequestComponentPresetSO : ComponentPresetSO
{
    [SerializeField] private int prefabID;

    public override Type ComponentType => typeof(PrefabViewRequestComponent);

    /// <summary>用于测试或运行时动态配置预设值。</summary>
    public void Configure(int prefabID)
    {
        this.prefabID = prefabID;
    }

    /// <summary>写入 PrefabViewRequestComponent。</summary>
    public override void Apply(World world, Entity entity)
    {
        if (world == null || !world.IsAlive(entity))
            return;

        world.SetComponent(entity, new PrefabViewRequestComponent(prefabID));
    }

    /// <summary>校验 View Prefab ID。</summary>
    public override void Validate(EntityDefinitionValidationResult result, string ownerName)
    {
        if (result == null)
            return;

        if (prefabID < 0)
            result.AddError($"[{ownerName}] PrefabViewRequestComponentPreset prefabID must not be less than 0.");
    }
}

}
