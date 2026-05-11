/*
 * 文件说明：ECS 框架源文件。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

using System;
using System.Collections;
using System.Collections.Generic;

namespace ECSFrameWork
{
/// <summary>
/// ECS 框架通用工具函数集合。
/// </summary>
internal static class ToolFunction
{
    /// <summary>
    /// 确保数组容量至少达到指定长度，不足时按倍增策略扩容。
    /// </summary>
    public static void EnsureArrayLength<T>(ref T[] array, int length)
    {
        if (length <= array.Length)
            return;

        int newLength = Math.Max(length, array.Length == 0 ? 16 : array.Length * 2);
        Array.Resize(ref array, newLength);
    }
}


}
