using System;
using UnityEngine;

namespace ECSFrameWork
{

/// <summary>VelocityComponent 的 Unity 预设。</summary>
[CreateAssetMenu(fileName = "VelocityComponentPreset", menuName = "ECSFrameWork/Component Preset/Velocity")]
public sealed class VelocityComponentPresetSO : ComponentPresetSO
{
    [SerializeField] private Vector3 velocity;

    public override Type ComponentType => typeof(VelocityComponent);

    /// <summary>用于测试或运行时动态配置预设值。</summary>
    public void Configure(Vector3 velocity)
    {
        this.velocity = velocity;
    }

    /// <summary>写入 VelocityComponent。</summary>
    public override void Apply(World world, Entity entity)
    {
        if (world == null || !world.IsAlive(entity))
            return;

        world.SetComponent(entity, new VelocityComponent(velocity.x, velocity.y, velocity.z));
    }
}

}
