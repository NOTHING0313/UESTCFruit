# BuffSystem MCP Testing Helper

该目录用于 Phase 3I-12A 的本地测试编排辅助。

## Unity 静态入口

Unity / MCP / batchmode 应调用以下 Editor-only 静态方法：

```text
BuffSystem.EditorTesting.BuffSystemMcpTestEntry.RunAllBuffSystemTests
BuffSystem.EditorTesting.BuffSystemMcpTestEntry.RunUnitTests
BuffSystem.EditorTesting.BuffSystemMcpTestEntry.RunIntegrationTests
BuffSystem.EditorTesting.BuffSystemMcpTestEntry.RunWhiteBoxTests
BuffSystem.EditorTesting.BuffSystemMcpTestEntry.RunBlackBoxTests
BuffSystem.EditorTesting.BuffSystemMcpTestEntry.RunSmokeTests
BuffSystem.EditorTesting.BuffSystemMcpTestEntry.RunAuthoringSmokeTests
```

示例 Unity batchmode 形态：

```powershell
Unity.exe -batchmode -quit -projectPath <UESTCFruit> -executeMethod BuffSystem.EditorTesting.BuffSystemMcpTestEntry.RunAllBuffSystemTests
```

当前仓库未固定 Unity MCP CLI 命令名，因此脚本不会硬编码不存在的 MCP API。

## 报告路径

测试入口会写入：

```text
Temp/BuffSystemTestReports/latest.json
Temp/BuffSystemTestReports/latest.md
```

读取报告：

```powershell
.\Tools\BuffSystemTesting\run_buffsystem_mcp_tests.ps1 -WaitForReport
```

## 边界

- 不修改 BuffSystem runtime。
- 不修改 registry / whitelist / eligibility。
- 不创建 Buff asset。
- 不生成 Effect 模板。
- 不保存 scene。
- 现有 MonoBehaviour ContextMenu Runner 仍保留为手动回归入口。
