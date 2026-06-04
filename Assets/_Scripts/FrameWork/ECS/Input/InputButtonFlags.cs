/*
 * 文件说明：输入快照、输入组件和输入应用器。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

using System;

namespace ECSFrameWork
{

/// <summary>
/// ECS 层使用的输入按钮标记。
/// </summary>
[Flags]
/// <summary>
/// 输入按键位标记。
/// </summary>
public enum InputButtonFlags : long
{
    None = 0,

    KeySpace = 1L << 0,
    KeyE = 1L << 1,
    KeyQ = 1L << 2,
    KeyR = 1L << 3,
    KeyF = 1L << 4,
    KeyLeftShift = 1L << 5,
    KeyLeftCtrl = 1L << 6,
    KeyEscape = 1L << 7,

    MouseLeft = 1L << 16,
    MouseRight = 1L << 17,
    MouseMiddle = 1L << 18,
    MouseBack = 1L << 19,
    MouseForward = 1L << 20,
}

}
