/*
 * 文件说明：输入快照、输入组件和输入应用器。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

using System;

/// <summary>
/// ECS 层使用的输入按钮标记。
/// </summary>
[Flags]
/// <summary>
/// 输入按键位标记。
/// </summary>
public enum InputButtonFlags : ulong
{
    None = 0,

    KeySpace = 1UL << 0,
    KeyE = 1UL << 1,
    KeyQ = 1UL << 2,
    KeyR = 1UL << 3,
    KeyF = 1UL << 4,
    KeyLeftShift = 1UL << 5,
    KeyLeftCtrl = 1UL << 6,
    KeyEscape = 1UL << 7,

    MouseLeft = 1UL << 16,
    MouseRight = 1UL << 17,
    MouseMiddle = 1UL << 18,
    MouseBack = 1UL << 19,
    MouseForward = 1UL << 20,
}
