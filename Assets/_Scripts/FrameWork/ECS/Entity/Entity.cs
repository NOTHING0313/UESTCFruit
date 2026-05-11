/*
 * 文件说明：Entity 句柄、版本号和实体运行时数据。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

using System;

namespace ECSFrameWork
{

/// <summary>
/// Entity 句柄，使用 ID 与 Version 校验实体身份。
/// </summary>
public readonly struct Entity : IEquatable<Entity>
{
    public static readonly Entity Invalid = new Entity(-1, 0);

    private readonly int _id;
    private readonly int _version;

    public int ID => _id;
    public int Version => _version;

    public bool IsValid => (_id >= 0 && _version > 0);

    /// <summary>
    /// 创建实体句柄，记录实体 ID 与版本号。
    /// </summary>
    public Entity(int id, int version)
    {
        _id = id;
        _version = version;
    }

    /// <summary>
    /// 判断两个实体句柄是否指向同一个实体版本。
    /// </summary>
    public bool Equals(Entity other)
    {
        return _id == other._id
            && _version == other._version;
    }

    /// <summary>
    /// 按 object 入口判断是否为相同 Entity。
    /// </summary>
    public override bool Equals(object obj)
    {
        return obj is Entity other && Equals(other);
    }

    /// <summary>
    /// 生成与 Equals 对应的哈希值。
    /// </summary>
    public override int GetHashCode()
    {
        unchecked
        {
            return (_id * 397) ^ _version;
        }
    }

    /// <summary>
    /// 比较两个 Entity 是否相等。
    /// </summary>
    public static bool operator ==(Entity left, Entity right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// 比较两个 Entity 是否不相等。
    /// </summary>
    public static bool operator !=(Entity left, Entity right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// 返回便于调试的实体 ID 与版本号字符串。
    /// </summary>
    public override string ToString()
    {
        return $"Entity(ID: {_id}, Version: {_version})";
    }
}

}
