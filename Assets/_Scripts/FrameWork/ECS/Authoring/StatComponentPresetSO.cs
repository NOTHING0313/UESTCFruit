using System;
using UnityEngine;

namespace ECSFrameWork
{

/// <summary>StatComponent 的 Unity 预设。</summary>
[CreateAssetMenu(fileName = "StatComponentPreset", menuName = "ECSFrameWork/Component Preset/Stat")]
public sealed class StatComponentPresetSO : ComponentPresetSO
{
    [SerializeField] private int attack;
    [SerializeField] private int defense;
    [SerializeField] private int moveSpeed;

    public override Type ComponentType => typeof(StatComponent);

    /// <summary>用于测试或运行时动态配置预设值。</summary>
    public void Configure(int attack, int defense, int moveSpeed)
    {
        this.attack = attack;
        this.defense = defense;
        this.moveSpeed = moveSpeed;
    }

    /// <summary>写入 StatComponent。</summary>
    public override void Apply(World world, Entity entity)
    {
        if (world == null || !world.IsAlive(entity))
            return;

        world.SetComponent(entity, new StatComponent(attack, defense, moveSpeed));
    }
}

}
