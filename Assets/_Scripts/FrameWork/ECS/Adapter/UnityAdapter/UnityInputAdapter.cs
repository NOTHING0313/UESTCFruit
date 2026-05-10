/*
 * 文件说明：UnityInputAdapter 只负责采样 Unity 输入并生成 PlayerInputSnapshot，推荐通过 InputSnapshotBuffer 再写入 World。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 从 Unity 新输入系统采样键盘和鼠标输入，并在逻辑帧开始前写入 ECS PlayerInputComponent。
/// </summary>
public sealed class UnityInputAdapter : MonoBehaviour
{
    [Serializable]
    private struct KeyboardBinding
    {
        public InputButtonFlags button;
        public Key key;
    }

    [Serializable]
    private struct MouseBinding
    {
        public InputButtonFlags button;
        public MouseButtonType mouseButton;
    }

    [Header("Player")]
    [SerializeField] private int playerID = 1;

    [Header("Keyboard Movement")]
    [SerializeField] private Key upKey = Key.W;
    [SerializeField] private Key downKey = Key.S;
    [SerializeField] private Key leftKey = Key.A;
    [SerializeField] private Key rightKey = Key.D;

    [Header("Keyboard Buttons")]
    [SerializeField]
    private KeyboardBinding[] keyboardBindings =
    {
        new KeyboardBinding { button = InputButtonFlags.KeySpace, key = Key.Space },
        new KeyboardBinding { button = InputButtonFlags.KeyE, key = Key.E },
        new KeyboardBinding { button = InputButtonFlags.KeyQ, key = Key.Q },
        new KeyboardBinding { button = InputButtonFlags.KeyR, key = Key.R },
        new KeyboardBinding { button = InputButtonFlags.KeyF, key = Key.F },
        new KeyboardBinding { button = InputButtonFlags.KeyLeftShift, key = Key.LeftShift },
        new KeyboardBinding { button = InputButtonFlags.KeyLeftCtrl, key = Key.LeftCtrl },
        new KeyboardBinding { button = InputButtonFlags.KeyEscape, key = Key.Escape },
    };

    [Header("Mouse Buttons")]
    [SerializeField]
    private MouseBinding[] mouseBindings =
    {
        new MouseBinding { button = InputButtonFlags.MouseLeft, mouseButton = MouseButtonType.Left },
        new MouseBinding { button = InputButtonFlags.MouseRight, mouseButton = MouseButtonType.Right },
        new MouseBinding { button = InputButtonFlags.MouseMiddle, mouseButton = MouseButtonType.Middle },
        new MouseBinding { button = InputButtonFlags.MouseBack, mouseButton = MouseButtonType.Back },
        new MouseBinding { button = InputButtonFlags.MouseForward, mouseButton = MouseButtonType.Forward },
    };

    private World _world;
    private EntityInfo _playerEntity;

    private Vector2 _moveInput;
    private Vector2 _mousePosition;
    private Vector2 _mouseDelta;
    private Vector2 _mouseScroll;

    private InputButtonFlags _heldButtons;
    private InputButtonFlags _pressedBuffer;
    private InputButtonFlags _releasedBuffer;

    public int PlayerID => playerID;

    /// <summary>初始化输入 Adapter 的写入目标。</summary>
    public void Init(World world, EntityInfo playerEntity)
    {
        _world = world;
        _playerEntity = playerEntity;
        ClearAllInput();
    }

    /// <summary>采样当前 Unity 输入设备状态；建议由 TimeSimulator 在 Unity Update 中统一调用。</summary>
    public void SampleInput()
    {
        SampleKeyboard();
        SampleMouse();
    }

    /// <summary>使用 SimulationContext 的 frameNumber 写入当前逻辑帧输入；保留给旧版单机接入使用。</summary>
    public void WriteInputToWorld(SimulationContext context)
    {
        WriteInputToWorld(context.frameNumber);
    }

    /// <summary>在每个 ECS 逻辑帧开始前，把缓存输入直接写入 PlayerInputComponent；推荐新代码优先使用 CollectSnapshot + InputSnapshotBuffer。</summary>
    public void WriteInputToWorld(int frameNumber)
    {
        PlayerInputSnapshot snapshot = CollectSnapshot(frameNumber);

        if (_world == null || !_world.IsAlive(_playerEntity))
            return;

        PlayerInputComponent input = PlayerInputComponent.FromSnapshot(in snapshot);
        _world.SetComponent(_playerEntity, in input);
    }

    /// <summary>收集当前缓存输入为帧输入快照，并清理 pressed/released/delta/scroll 等一次性输入缓存。</summary>
    public PlayerInputSnapshot CollectSnapshot(int frameNumber)
    {
        PlayerInputSnapshot snapshot = CreateSnapshot(frameNumber);
        ClearLogicFrameInput();
        return snapshot;
    }

    /// <summary>创建当前缓存输入对应的帧输入快照；该方法不清理输入缓存。</summary>
    public PlayerInputSnapshot CreateSnapshot(int frameNumber)
    {
        PlayerInputSnapshot snapshot = new PlayerInputSnapshot(frameNumber, playerID)
        {
            moveX = _moveInput.x,
            moveY = _moveInput.y,
            mouseX = _mousePosition.x,
            mouseY = _mousePosition.y,
            mouseDeltaX = _mouseDelta.x,
            mouseDeltaY = _mouseDelta.y,
            scrollX = _mouseScroll.x,
            scrollY = _mouseScroll.y,
            heldButtons = _heldButtons,
            pressedButtons = _pressedBuffer,
            releasedButtons = _releasedBuffer,
        };

        return snapshot;
    }

    /// <summary>清理全部输入缓存，通常在 Init 时使用。</summary>
    private void ClearAllInput()
    {
        _moveInput = Vector2.zero;
        _mousePosition = Vector2.zero;
        _mouseDelta = Vector2.zero;
        _mouseScroll = Vector2.zero;
        _heldButtons = InputButtonFlags.None;
        _pressedBuffer = InputButtonFlags.None;
        _releasedBuffer = InputButtonFlags.None;
    }

    /// <summary>清理只应该被一个逻辑帧消费一次的输入。</summary>
    private void ClearLogicFrameInput()
    {
        _pressedBuffer = InputButtonFlags.None;
        _releasedBuffer = InputButtonFlags.None;
        _mouseDelta = Vector2.zero;
        _mouseScroll = Vector2.zero;
    }

    /// <summary>采样键盘输入。</summary>
    private void SampleKeyboard()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            _moveInput = Vector2.zero;
            return;
        }

        Vector2 move = Vector2.zero;

        if (IsKeyHeld(keyboard, upKey))
            move.y += 1f;

        if (IsKeyHeld(keyboard, downKey))
            move.y -= 1f;

        if (IsKeyHeld(keyboard, leftKey))
            move.x -= 1f;

        if (IsKeyHeld(keyboard, rightKey))
            move.x += 1f;

        _moveInput = move.sqrMagnitude > 1f ? move.normalized : move;

        if (keyboardBindings == null)
            return;

        for (int i = 0; i < keyboardBindings.Length; i++)
        {
            KeyboardBinding binding = keyboardBindings[i];

            if (binding.button == InputButtonFlags.None)
                continue;

            SampleKeyButton(keyboard, binding.key, binding.button);
        }
    }

    /// <summary>采样鼠标输入。</summary>
    private void SampleMouse()
    {
        Mouse mouse = Mouse.current;

        if (mouse == null)
        {
            _mousePosition = Vector2.zero;
            _mouseDelta = Vector2.zero;
            _mouseScroll = Vector2.zero;
            return;
        }

        _mousePosition = mouse.position.ReadValue();
        _mouseDelta += mouse.delta.ReadValue();
        _mouseScroll += mouse.scroll.ReadValue();

        if (mouseBindings == null)
            return;

        for (int i = 0; i < mouseBindings.Length; i++)
        {
            MouseBinding binding = mouseBindings[i];

            if (binding.button == InputButtonFlags.None)
                continue;

            SampleMouseButton(mouse, binding.mouseButton, binding.button);
        }
    }

    /// <summary>采样单个键盘按钮。</summary>
    private void SampleKeyButton(Keyboard keyboard, Key key, InputButtonFlags button)
    {
        KeyControlWrapper control = GetKeyControl(keyboard, key);

        if (!control.isValid)
            return;

        ApplyButtonState(button, control.isPressed, control.wasPressedThisFrame, control.wasReleasedThisFrame);
    }

    /// <summary>采样单个鼠标按钮。</summary>
    private void SampleMouseButton(Mouse mouse, MouseButtonType mouseButton, InputButtonFlags button)
    {
        if (mouse == null)
            return;

        bool isPressed = false;
        bool wasPressedThisFrame = false;
        bool wasReleasedThisFrame = false;

        switch (mouseButton)
        {
            case MouseButtonType.Left:
                isPressed = mouse.leftButton.isPressed;
                wasPressedThisFrame = mouse.leftButton.wasPressedThisFrame;
                wasReleasedThisFrame = mouse.leftButton.wasReleasedThisFrame;
                break;

            case MouseButtonType.Right:
                isPressed = mouse.rightButton.isPressed;
                wasPressedThisFrame = mouse.rightButton.wasPressedThisFrame;
                wasReleasedThisFrame = mouse.rightButton.wasReleasedThisFrame;
                break;

            case MouseButtonType.Middle:
                isPressed = mouse.middleButton.isPressed;
                wasPressedThisFrame = mouse.middleButton.wasPressedThisFrame;
                wasReleasedThisFrame = mouse.middleButton.wasReleasedThisFrame;
                break;

            case MouseButtonType.Back:
                isPressed = mouse.backButton.isPressed;
                wasPressedThisFrame = mouse.backButton.wasPressedThisFrame;
                wasReleasedThisFrame = mouse.backButton.wasReleasedThisFrame;
                break;

            case MouseButtonType.Forward:
                isPressed = mouse.forwardButton.isPressed;
                wasPressedThisFrame = mouse.forwardButton.wasPressedThisFrame;
                wasReleasedThisFrame = mouse.forwardButton.wasReleasedThisFrame;
                break;
        }

        ApplyButtonState(button, isPressed, wasPressedThisFrame, wasReleasedThisFrame);
    }

    /// <summary>应用按钮状态到缓存。</summary>
    private void ApplyButtonState(InputButtonFlags button, bool isPressed, bool wasPressedThisFrame, bool wasReleasedThisFrame)
    {
        if (isPressed)
            _heldButtons |= button;
        else
            _heldButtons &= ~button;

        if (wasPressedThisFrame)
            _pressedBuffer |= button;

        if (wasReleasedThisFrame)
            _releasedBuffer |= button;
    }

    /// <summary>判断某个键是否按住。</summary>
    private bool IsKeyHeld(Keyboard keyboard, Key key)
    {
        KeyControlWrapper control = GetKeyControl(keyboard, key);
        return control.isValid && control.isPressed;
    }

    /// <summary>安全获取键盘按键 Control。</summary>
    private KeyControlWrapper GetKeyControl(Keyboard keyboard, Key key)
    {
        if (keyboard == null)
            return default;

        try
        {
            var control = keyboard[key];

            if (control == null)
                return default;

            return new KeyControlWrapper(control.isPressed, control.wasPressedThisFrame, control.wasReleasedThisFrame, true);
        }
        catch
        {
            return default;
        }
    }

    private readonly struct KeyControlWrapper
    {
        public readonly bool isPressed;
        public readonly bool wasPressedThisFrame;
        public readonly bool wasReleasedThisFrame;
        public readonly bool isValid;

        public KeyControlWrapper(bool isPressed, bool wasPressedThisFrame, bool wasReleasedThisFrame, bool isValid)
        {
            this.isPressed = isPressed;
            this.wasPressedThisFrame = wasPressedThisFrame;
            this.wasReleasedThisFrame = wasReleasedThisFrame;
            this.isValid = isValid;
        }
    }
}

/// <summary>
/// Unity Adapter 层使用的鼠标按钮类型。
/// </summary>
public enum MouseButtonType
{
    Left,
    Right,
    Middle,
    Back,
    Forward,
}
