/*
 * 文件说明：ComponentMask256 使用 4 个 ulong 表示最多 256 种组件组合，用于 ArcheType 分组和 Query 匹配。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

using System;

namespace ECSFrameWork
{

/// <summary>
/// 最多支持 256 种组件类型的组件组合 Mask。
/// </summary>
public struct ComponentMask256 : IEquatable<ComponentMask256>
{
    private ulong _word0;
    private ulong _word1;
    private ulong _word2;
    private ulong _word3;

    public static ComponentMask256 Empty => default;

    public bool IsEmpty
    {
        get
        {
            return _word0 == 0
                && _word1 == 0
                && _word2 == 0
                && _word3 == 0;
        }
    }

    /// <summary>
    /// 清空当前 Mask 中的全部组件位。
    /// </summary>
    public void Clear()
    {
        _word0 = 0;
        _word1 = 0;
        _word2 = 0;
        _word3 = 0;
    }

    /// <summary>
    /// 将指定组件类型对应的 bit 置为 1。
    /// </summary>
    public void Set(int bitIndex)
    {
        ValidateBitIndex(bitIndex);

        ulong mask = 1UL << (bitIndex & 63);

        switch (bitIndex >> 6)
        {
            case 0:
                _word0 |= mask;
                break;
            case 1:
                _word1 |= mask;
                break;
            case 2:
                _word2 |= mask;
                break;
            case 3:
                _word3 |= mask;
                break;
        }
    }

    /// <summary>
    /// 将指定组件类型对应的 bit 清为 0。
    /// </summary>
    public void Clear(int bitIndex)
    {
        ValidateBitIndex(bitIndex);

        ulong mask = ~(1UL << (bitIndex & 63));

        switch (bitIndex >> 6)
        {
            case 0:
                _word0 &= mask;
                break;
            case 1:
                _word1 &= mask;
                break;
            case 2:
                _word2 &= mask;
                break;
            case 3:
                _word3 &= mask;
                break;
        }
    }

    /// <summary>
    /// 判断指定组件类型对应的 bit 是否存在。
    /// </summary>
    public bool Has(int bitIndex)
    {
        ValidateBitIndex(bitIndex);

        ulong word = GetWordValue(bitIndex);
        ulong mask = 1UL << (bitIndex & 63);

        return (word & mask) != 0;
    }

    /// <summary>
    /// 判断当前 Mask 是否包含 required 中的全部组件位。
    /// </summary>
    public bool ContainsAll(in ComponentMask256 required)
    {
        return (_word0 & required._word0) == required._word0
            && (_word1 & required._word1) == required._word1
            && (_word2 & required._word2) == required._word2
            && (_word3 & required._word3) == required._word3;
    }

    /// <summary>
    /// 判断当前 Mask 是否与 other 至少有一个组件位重合。
    /// </summary>
    public bool ContainsAny(in ComponentMask256 other)
    {
        return (_word0 & other._word0) != 0
            || (_word1 & other._word1) != 0
            || (_word2 & other._word2) != 0
            || (_word3 & other._word3) != 0;
    }

    /// <summary>
    /// 判断两个 ComponentMask256 的四个 ulong 分段是否完全一致。
    /// </summary>
    public bool Equals(ComponentMask256 other)
    {
        return _word0 == other._word0
            && _word1 == other._word1
            && _word2 == other._word2
            && _word3 == other._word3;
    }

    /// <summary>
    /// 按 object 入口判断是否为相同的 ComponentMask256。
    /// </summary>
    public override bool Equals(object obj)
    {
        return obj is ComponentMask256 other && Equals(other);
    }

    /// <summary>
    /// 生成与 Equals 对应的哈希值，供字典和缓存作为 key 使用。
    /// </summary>
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + _word0.GetHashCode();
            hash = hash * 31 + _word1.GetHashCode();
            hash = hash * 31 + _word2.GetHashCode();
            hash = hash * 31 + _word3.GetHashCode();
            return hash;
        }
    }

    /// <summary>
    /// 比较两个 ComponentMask256 是否相等。
    /// </summary>
    public static bool operator ==(ComponentMask256 left, ComponentMask256 right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// 比较两个 ComponentMask256 是否不相等。
    /// </summary>
    public static bool operator !=(ComponentMask256 left, ComponentMask256 right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// 把可空 Mask 转换为非空 Mask，null 时返回 default。
    /// </summary>
    public static ComponentMask256 FromNullable(ComponentMask256? mask)
    {
        return mask ?? default;
    }

    /// <summary>
    /// 校验 bit 下标是否处于 0 到 255 的合法范围内。
    /// </summary>
    private static void ValidateBitIndex(int bitIndex)
    {
        if (bitIndex < 0 || bitIndex >= 256)
            throw new ArgumentOutOfRangeException(nameof(bitIndex));
    }

    /// <summary>
    /// 根据 bit 下标读取对应的 ulong 分段值。
    /// </summary>
    private ulong GetWordValue(int bitIndex)
    {
        switch (bitIndex >> 6)
        {
            case 0:
                return _word0;
            case 1:
                return _word1;
            case 2:
                return _word2;
            case 3:
                return _word3;
            default:
                throw new ArgumentOutOfRangeException(nameof(bitIndex));
        }
    }

    /// <summary>
    /// 统计当前 Mask 中已经置为 1 的组件位数量。
    /// </summary>
    public int CountBits()
    {
        return CountBits(_word0) + CountBits(_word1) + CountBits(_word2) + CountBits(_word3);
    }

    /// <summary>
    /// 返回便于 Debug.Log 查看的一行 Mask 文本。
    /// </summary>
    public override string ToString()
    {
        return $"Mask256(0x{_word3:X16}_{_word2:X16}_{_word1:X16}_{_word0:X16})";
    }

    /// <summary>
    /// 统计单个 ulong 中已经置为 1 的 bit 数。
    /// </summary>
    private static int CountBits(ulong value)
    {
        int count = 0;

        while (value != 0)
        {
            value &= value - 1;
            count++;
        }

        return count;
    }

    /// <summary>
    /// 把 other 的组件位合并到当前 Mask 中。
    /// </summary>
    public void Merge(in ComponentMask256 other)
    {
        _word0 |= other._word0;
        _word1 |= other._word1;
        _word2 |= other._word2;
        _word3 |= other._word3;
    }
}

}
