using System;
using UnityEngine;

namespace ECSFrameWork
{

/// <summary>MoveSpeedComponent 的 Unity 预设。</summary>
[CreateAssetMenu(fileName = "MoveSpeedComponentPreset", menuName = "ECSFrameWork/Component Preset/Move Speed")]
public sealed class MoveSpeedComponentPresetSO : ComponentPresetSO
{
    [SerializeField] private float value = 1f;

    public override Type ComponentType => typeof(MoveSpeedComponent);

    /// <summary>用于测试或运行时动态配置预设值。</summary>
    public void Configure(float value)
    {
        this.value = value;
    }

    /// <summary>写入 MoveSpeedComponent。</summary>
    public override void Apply(World world, Entity entity)
    {
        if (world == null || !world.IsAlive(entity))
            return;

        world.SetComponent(entity, new MoveSpeedComponent(value));
    }

    /// <summary>校验移动速度配置。</summary>
    public override void Validate(EntityDefinitionValidationResult result, string ownerName)
    {
        if (result == null)
            return;

        if (value < 0f)
            result.AddError($"[{ownerName}] MoveSpeedComponentPreset value must not be less than 0.");
    }
}

}
