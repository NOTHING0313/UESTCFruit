/*
 * 文件说明：PlayerInputSnapshot 是可缓存、可传输、可回放的玩家输入快照。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

/// <summary>
/// 按逻辑帧保存的玩家输入快照；后续帧同步和回滚可以直接缓存、传输、重放该数据。
/// </summary>
public struct PlayerInputSnapshot
{
    public int frameNumber;
    public int playerID;

    public float moveX;
    public float moveY;

    public float mouseX;
    public float mouseY;
    public float mouseDeltaX;
    public float mouseDeltaY;
    public float scrollX;
    public float scrollY;

    public InputButtonFlags pressedButtons;
    public InputButtonFlags heldButtons;
    public InputButtonFlags releasedButtons;

    /// <summary>创建指定逻辑帧、指定玩家的空输入快照。</summary>
    public PlayerInputSnapshot(int frameNumber, int playerID)
    {
        this.frameNumber = frameNumber;
        this.playerID = playerID;

        moveX = 0f;
        moveY = 0f;

        mouseX = 0f;
        mouseY = 0f;
        mouseDeltaX = 0f;
        mouseDeltaY = 0f;
        scrollX = 0f;
        scrollY = 0f;

        pressedButtons = InputButtonFlags.None;
        heldButtons = InputButtonFlags.None;
        releasedButtons = InputButtonFlags.None;
    }

    /// <summary>判断按钮当前逻辑帧是否按住。</summary>
    public bool IsHeld(InputButtonFlags button)
    {
        return (heldButtons & button) != 0;
    }

    /// <summary>判断按钮是否在当前逻辑帧刚刚按下。</summary>
    public bool WasPressed(InputButtonFlags button)
    {
        return (pressedButtons & button) != 0;
    }

    /// <summary>判断按钮是否在当前逻辑帧刚刚松开。</summary>
    public bool WasReleased(InputButtonFlags button)
    {
        return (releasedButtons & button) != 0;
    }
}
