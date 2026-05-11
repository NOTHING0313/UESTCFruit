/*
 * 文件说明：EntityCreateContext 保存创建 Entity 时的运行时参数，例如出生位置、初始速度、所属者和目标。
 */

using UnityEngine;

namespace ECSFrameWork
{

/// <summary>
/// Entity 创建上下文。
/// SO 保存静态配置，Context 保存每次创建时才确定的动态参数。
/// </summary>
public struct EntityCreateContext
{
    /// <summary>出生位置。</summary>
    public Vector3 position;

    /// <summary>初始速度。</summary>
    public Vector3 velocity;

    /// <summary>所属玩家或创建者 ID。</summary>
    public int ownerID;

    /// <summary>阵营 ID。</summary>
    public int campID;

    /// <summary>来源 Entity，例如施法者、发射者或创建者。</summary>
    public Entity sourceEntity;

    /// <summary>目标 Entity。</summary>
    public Entity targetEntity;

    /// <summary>默认创建上下文。</summary>
    public static EntityCreateContext Default => new EntityCreateContext
    {
        sourceEntity = Entity.Invalid,
        targetEntity = Entity.Invalid,
    };
}

}
