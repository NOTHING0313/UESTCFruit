#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ECSFrameWork
{
/// <summary>
/// Phase 3G-4C 保留的历史占位类型。
/// 中文调试主入口已回到原 IMGUI `ECSWorldDebuggerWindow`，本类型不再注册菜单，避免误打开功能不完整的实验窗口。
/// </summary>
public sealed class ECSWorldOdinDebuggerWindow : EditorWindow
{
    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "该 Odin 实验窗口已停用。\n请使用主入口：Window / ECSFrameWork / World Debugger。",
            MessageType.Info);
    }
}
}
#endif
