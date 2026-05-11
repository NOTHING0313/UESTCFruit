using System;
using UnityEngine;

namespace ECSFrameWork
{

/// <summary>PositionComponent 的 Unity 预设。</summary>
[CreateAssetMenu(fileName = "PositionComponentPreset", menuName = "ECSFrameWork/Component Preset/Position")]
public sealed class PositionComponentPresetSO : ComponentPresetSO
{
    [SerializeField] private Vector3 position;

    public override Type ComponentType => typeof(PositionComponent);

    /// <summary>用于测试或运行时动态配置预设值。</summary>
    public void Configure(Vector3 position)
    {
        this.position = position;
    }

    /// <summary>写入 PositionComponent。</summary>
    public override void Apply(World world, Entity entity)
    {
        if (world == null || !world.IsAlive(entity))
            return;

        world.SetComponent(entity, new PositionComponent(position.x, position.y, position.z));
    }
}

}
