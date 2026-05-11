using System;
using UnityEngine;

namespace ECSFrameWork
{

/// <summary>PlayerTagComponent 的 Unity 预设。</summary>
[CreateAssetMenu(fileName = "PlayerTagComponentPreset", menuName = "ECSFrameWork/Component Preset/Player Tag")]
public sealed class PlayerTagComponentPresetSO : ComponentPresetSO
{
    public override Type ComponentType => typeof(PlayerTagComponent);

    /// <summary>写入 PlayerTagComponent。</summary>
    public override void Apply(World world, Entity entity)
    {
        if (world == null || !world.IsAlive(entity))
            return;

        world.SetComponent(entity, new PlayerTagComponent());
    }
}

}
