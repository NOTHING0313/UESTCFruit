AI Image Pipeline Kit v2.0

用于在 Unity 项目中安装 image_assets MCP Server，并让 Codex 通过自然语言完成：
- UI 图片资源生成
- mockup / 方案图生成
- ui_spec.json + asset_manifest.json 结构化方案输出
- 通过 Unity UI Workflow 从 spec 确定性生成 Prefab
- TMP / 中文字体检查与修复
- 控件图标完整性检查
- Unity MCP 的一键安装、构建、Codex 配置与端口检查

推荐安装流程：
1. Tools / AI Image Pipeline / Setup Window
2. 点击「1. 初始化 / 更新当前项目」
3. 如果 Image MCP Server 未构建，点击「2. 构建 MCP Server」
4. 点击「3. 一键配置 Unity MCP」
5. 点击「4. 启用当前项目到 Codex」
6. 打开 Unity MCP Server Window 并点击 Start Server
7. 重启 Codex

v2.0 重点优化：
- Setup Window 增加 Unity MCP One-Click Setup。
- 可通过 Unity Package Manager 在线安装 / 更新 Unity MCP。
- 默认安装源： https://github.com/CoderGamester/mcp-unity.git
- 支持在 Project Defaults 中改写 Unity MCP install source，可填写固定 commit / branch 的 Git URL。
- 自动检测 Unity MCP PackageCache / Packages 下的 Server~/build/index.js。
- 当 Server~/build/index.js 缺失时，可直接打开 PowerShell 构建 Unity MCP Server~。
- 自动写入用户级 ~/.codex/config.toml 的 Unity MCP 配置块。
- 使用 block marker 更新 Unity MCP 配置，避免覆盖 DeepSeek、image2 或用户自己的其他 MCP 配置。
- 自动设置 UNITY_HOST、UNITY_PORT、UNITY_REQUEST_TIMEOUT 用户级环境变量。
- 新增 Unity MCP Server Window 打开按钮。
- 新增 Unity MCP 端口检查，默认检查 127.0.0.1:8090。
- 新增 Codex 配置预览文件：Codex/config.ai_pipeline.preview.toml。
- image-mcp-server 保留 health_check 工具，用于检查配置，不会生成图片。
- image job 状态会持久化到输出目录下的 _jobs 文件夹，Codex 重连后可继续查询旧 jobId。
- 保留基础成本护栏环境变量：
  - IMAGE_MCP_MAX_JOBS_PER_SESSION，默认 10
  - IMAGE_MCP_MAX_ASSETS_PER_MANIFEST，默认 12
- AGENTS 模板补充 @image / @read / @plan / @review / @do / @handoff 短触发词规则。

Unity MCP 使用说明：
- 本包不会直接把 Unity MCP 源码塞进 Assets，而是通过 Unity Package Manager 安装。
- 点击「One-Click Setup」会按当前状态执行：
  1. 未安装时：安装 Unity MCP Package。
  2. 已安装但未构建时：打开 PowerShell 构建 Server~。
  3. 已构建时：写入 Codex 配置并测试端口。
- Package 安装和 npm 构建可能触发 Unity 编译或打开外部 PowerShell。完成后如状态未刷新，请重新打开 Setup Window 并再次点击 One-Click Setup。
- 点击「Configure Codex」只会修改 Codex 配置和环境变量，不会修改 Scene、Prefab、ScriptableObject 或 ProjectSettings。
- 点击「Test Port」只能证明端口可连接，最终仍建议让 Codex 读取当前场景信息验证完整链路。
- 本包不会自动保存场景，不会自动进入 PlayMode，不会自动修改 Unity 资源。

推荐 UI 工作流：
1. 对 Codex 说：按 AIImagePipelineKit UI 工作流执行，本轮只做方案阶段。
2. Codex 生成 mockup、ui_spec.json、asset_manifest.json。
3. 在 Unity 打开 Tools / AI Image Pipeline / UI Workflow。
4. 点击 Run Full Local Check。
5. 点击 Validate Spec。
6. 点击 Build UI Prefab From Spec。
7. 对生成 Prefab 做增量修复，而不是重建整个 UI。

日常 Prompt 示例：

方案阶段：
请为当前 Unity 项目从零设计一个建造面板 UI，名称为 BuildPanel。
按 AIImagePipelineKit UI 工作流执行，本轮只做方案阶段。
需求：左侧分类，中间卡片列表，右侧详情，顶部标题和关闭按钮，底部资源提示，暗色科幻风，支持中文和不同分辨率。

构建准备：
我确认 BuildPanel 的方案。进入 UI构建模式，请检查并完善 ui_spec.json 和 asset_manifest.json，告诉我下一步在 Unity 中点击哪个按钮。

增量修复：
进入 UI修复模式，检查当前 BuildPanel Prefab 的关闭按钮、控件图标、TMP 中文字体和布局差异。本轮先只给修复计划。

素材生成：
@image 在文件夹 BuildPanel/Icons 中生成文件名为 icon_factory 的工厂图标，2D 扁平科幻风格，不要文字，不要水印。

检查 image MCP：
@image 只检查 image_assets 是否可用，不要生成图片。

检查 Unity MCP：
@read 只检查 mcp_unity 是否可用，不要修改任何文件。读取当前 Unity 场景信息并汇总。

已有能力摘要：
- 自然语言生成 Unity UI 图像资源。
- 异步生图，避免 Codex 长时间卡在同步调用。
- preview-first 工作流。
- 自动 Sprite 导入。
- Setup Window 自动合并 AGENTS.md、生成 Codex 配置并启用当前项目。
- TextMeshPro Setup 工具：导入 TMP Essentials、创建中文 TMP FontAsset、修复 Prefab 内 TMP 字体。
- UI Workflow 工具：健康检查、创建样例 spec/manifest、校验 spec、从 spec 构建 UI Prefab。
- Unity MCP 配置辅助：在线安装、路径检测、Server~ 构建、Codex 配置写入、环境变量写入、端口测试。

中文字体说明：
本包不内置字体文件。请自行导入授权允许使用的中文 .ttf / .otf，例如 Noto Sans CJK 或 Source Han Sans，然后在 Unity 中选中该 Font，使用：
Tools / AI Image Pipeline / TextMeshPro Setup / Create Dynamic Chinese TMP Font From Selected Font

注意：
- UnityPackage 不会保存任何 API Key。
- OPENAI_API_KEY / ARK_API_KEY 仍然通过系统环境变量提供。
- 切换 Unity 项目后，需要在当前项目中重新点击「一键配置 Unity MCP」和「启用当前项目到 Codex」，然后重启 Codex。
- 如果 UnityPackage 导入失败，可以使用 Overlay zip 手动覆盖 Assets/AIImagePipelineKit。
