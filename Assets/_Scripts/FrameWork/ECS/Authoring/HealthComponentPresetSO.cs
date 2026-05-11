using System;
using UnityEngine;

namespace ECSFrameWork
{

/// <summary>HealthComponent 的 Unity 预设。</summary>
[CreateAssetMenu(fileName = "HealthComponentPreset", menuName = "ECSFrameWork/Component Preset/Health")]
public sealed class HealthComponentPresetSO : ComponentPresetSO
{
    [SerializeField] private int current = 100;
    [SerializeField] private int max = 100;

    public override Type ComponentType => typeof(HealthComponent);

    /// <summary>用于测试或运行时动态配置预设值。</summary>
    public void Configure(int current, int max)
    {
        this.current = current;
        this.max = max;
    }

    /// <summary>写入 HealthComponent。</summary>
    public override void Apply(World world, Entity entity)
    {
        if (world == null || !world.IsAlive(entity))
            return;

        world.SetComponent(entity, new HealthComponent(current, max));
    }

    /// <summary>校验生命值配置。</summary>
    public override void Validate(EntityDefinitionValidationResult result, string ownerName)
    {
        if (result == null)
            return;

        if (max <= 0)
            result.AddError($"[{ownerName}] HealthComponentPreset max must be greater than 0.");

        if (current < 0)
            result.AddWarning($"[{ownerName}] HealthComponentPreset current is less than 0.");
    }
}

}
