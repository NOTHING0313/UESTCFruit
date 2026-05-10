/*
 * 文件说明：EntityQueryDescription 是 Query 的值对象，保存 includeMask 与 excludeMask。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

using System;

/// <summary>
/// Entity 查询条件值对象。
/// </summary>
public readonly struct EntityQueryDescription : IEquatable<EntityQueryDescription>
{
    public readonly ComponentMask256 includeMask;
    public readonly ComponentMask256 excludeMask;

    /// <summary>
    /// 创建 Query 描述，保存 includeMask 与 excludeMask。
    /// </summary>
    public EntityQueryDescription(ComponentMask256 includeMask, ComponentMask256 excludeMask = default)
    {
        this.includeMask = includeMask;
        this.excludeMask = excludeMask;
    }

    /// <summary>
    /// 比较两个 Query 描述是否具有相同 include/exclude Mask。
    /// </summary>
    public bool Equals(EntityQueryDescription other)
    {
        return includeMask == other.includeMask && excludeMask == other.excludeMask;
    }

    /// <summary>
    /// 按 object 入口判断 Query 描述是否相等。
    /// </summary>
    public override bool Equals(object obj)
    {
        return obj is EntityQueryDescription other && Equals(other);
    }

    /// <summary>
    /// 生成与 Equals 对应的哈希值，供 QueryCache 字典使用。
    /// </summary>
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + includeMask.GetHashCode();
            hash = hash * 31 + excludeMask.GetHashCode();
            return hash;
        }
    }

    /// <summary>
    /// 比较两个 EntityQueryDescription 是否相等。
    /// </summary>
    public static bool operator ==(EntityQueryDescription left, EntityQueryDescription right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// 比较两个 EntityQueryDescription 是否不相等。
    /// </summary>
    public static bool operator !=(EntityQueryDescription left, EntityQueryDescription right)
    {
        return !left.Equals(right);
    }
}
