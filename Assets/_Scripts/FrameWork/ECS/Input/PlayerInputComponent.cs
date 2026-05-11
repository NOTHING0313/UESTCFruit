namespace ECSFrameWork
{
/*
 * 文件说明：PlayerInputSnapshotComponent 是当前逻辑帧输入在 ECS World 中的组件投影，供 System 在 Tick 内读取。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

/// <summary>
/// 玩家输入快照组件，由 UnityInputAdapter 或网络输入模块写入，由 ECS System 消费。
/// </summary>
public struct PlayerInputSnapshotComponent : IComponentData
{
    public int inputFrame;
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

    /// <summary>创建不绑定具体逻辑帧的输入数据，主要用于测试或非帧同步模式。</summary>
    public PlayerInputSnapshotComponent(float moveX, float moveY)
    {
        inputFrame = 0;
        playerID = 0;

        this.moveX = moveX;
        this.moveY = moveY;

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

    /// <summary>创建绑定具体逻辑帧和玩家编号的输入数据。</summary>
    public PlayerInputSnapshotComponent(int inputFrame, int playerID, float moveX, float moveY)
    {
        this.inputFrame = inputFrame;
        this.playerID = playerID;

        this.moveX = moveX;
        this.moveY = moveY;

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

    /// <summary>根据输入快照创建组件数据。</summary>
    public PlayerInputSnapshotComponent(in PlayerInputSnapshot snapshot)
    {
        inputFrame = snapshot.frameNumber;
        playerID = snapshot.playerID;

        moveX = snapshot.moveX;
        moveY = snapshot.moveY;

        mouseX = snapshot.mouseX;
        mouseY = snapshot.mouseY;
        mouseDeltaX = snapshot.mouseDeltaX;
        mouseDeltaY = snapshot.mouseDeltaY;
        scrollX = snapshot.scrollX;
        scrollY = snapshot.scrollY;

        pressedButtons = snapshot.pressedButtons;
        heldButtons = snapshot.heldButtons;
        releasedButtons = snapshot.releasedButtons;
    }


    /// <summary>根据输入快照创建组件数据。</summary>
    public static PlayerInputSnapshotComponent FromSnapshot(in PlayerInputSnapshot snapshot)
    {
        return new PlayerInputSnapshotComponent(in snapshot);
    }

    /// <summary>判断该输入是否可用于指定逻辑帧；inputFrame 为 0 时表示测试或非帧同步输入。</summary>
    public bool IsValidForFrame(int frameNumber)
    {
        return inputFrame <= 0 || inputFrame == frameNumber;
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

}
