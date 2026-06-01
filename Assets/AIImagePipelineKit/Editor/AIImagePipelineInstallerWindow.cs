#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AIImagePipelineKit.Editor
{
    /// <summary>
    /// AI 图片流水线安装与项目配置窗口。
    /// </summary>
    public sealed class AIImagePipelineInstallerWindow : EditorWindow
    {
        private const string PackageRoot = "Assets/AIImagePipelineKit";
        private const string TemplateRoot = PackageRoot + "/Editor/Templates";
        private const string ServerTemplateRoot = TemplateRoot + "/ImageMcpServer";
        private const string AgentsTemplatePath = TemplateRoot + "/AGENTS.ImageAssets.md.txt";

        private const string OutputRootPrefsKey = AIImageImportPostprocessor.OutputRootPrefsKey;
        private const string McpServerPathPrefsKey = "AIImagePipeline.McpServerRelativePath";
        private const string DefaultOutputFolderPrefsKey = "AIImagePipeline.DefaultOutputFolder";
        private const string ProxyUrlPrefsKey = "AIImagePipeline.OpenAIProxyUrl";
        private const string ProviderPrefsKey = "AIImagePipeline.Provider";
        private const string OpenAIModelPrefsKey = "AIImagePipeline.OpenAIModel";
        private const string VolcengineModelPrefsKey = "AIImagePipeline.VolcengineModel";
        private const string VolcengineBaseUrlPrefsKey = "AIImagePipeline.VolcengineBaseUrl";
        private const string PreviewSizePrefsKey = "AIImagePipeline.DefaultPreviewSize";
        private const string PreviewQualityPrefsKey = "AIImagePipeline.DefaultPreviewQuality";
        private const string UnityMcpHostPrefsKey = "AIImagePipeline.UnityMcpHost";
        private const string UnityMcpPortPrefsKey = "AIImagePipeline.UnityMcpPort";
        private const string UnityMcpRequestTimeoutPrefsKey = "AIImagePipeline.UnityMcpRequestTimeout";
        private const string UnityMcpServerIdPrefsKey = "AIImagePipeline.UnityMcpServerId";
        private const string UnityMcpInstallSourcePrefsKey = "AIImagePipeline.UnityMcpInstallSource";

        private const string DefaultOutputRoot = "Assets/Arts/AI_Generate";
        private const string DefaultMcpServerPath = "Tools/image-mcp-server";
        private const string DefaultOutputFolder = "Test";
        private const string DefaultProxyUrl = "";
        private const string DefaultProvider = "openai";
        private const string DefaultOpenAIModel = "gpt-image-2";
        private const string DefaultVolcengineModel = "doubao-seedream-5-0-260128";
        private const string DefaultVolcengineBaseUrl = "https://ark.cn-beijing.volces.com/api/v3";
        private const string DefaultPreviewSize = "1024x1024";
        private const string DefaultPreviewQuality = "low";
        private const string DefaultUnityMcpHost = "127.0.0.1";
        private const int DefaultUnityMcpPort = 8090;
        private const int DefaultUnityMcpRequestTimeout = 30;
        private const string DefaultUnityMcpServerId = "mcp_unity";
        private const string DefaultUnityMcpInstallSource = "https://github.com/CoderGamester/mcp-unity.git";

        private string _outputRootAssetPath;
        private string _mcpServerRelativePath;
        private string _defaultOutputFolder;
        private string _proxyUrl;
        private string _provider;
        private string _openAIModel;
        private string _volcengineModel;
        private string _volcengineBaseUrl;
        private string _previewSize;
        private string _previewQuality;
        private string _unityMcpHost;
        private int _unityMcpPort;
        private int _unityMcpRequestTimeout;
        private string _unityMcpServerId;
        private string _unityMcpInstallSource;
        private AddRequest _unityMcpAddRequest;

        private Vector2 _scroll;
        private Vector2 _logScroll;
        private bool _showProjectDefaults;
        private bool _showAdvancedTools;
        private bool _showUnityMcpTools;
        private bool _showRecentLogs = true;

        private readonly StringBuilder _recentLogBuilder = new StringBuilder();

        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        [MenuItem("Tools/AI Image Pipeline/Setup Window")]
        public static void Open()
        {
            AIImagePipelineInstallerWindow window = GetWindow<AIImagePipelineInstallerWindow>("AI Image Pipeline");
            window.minSize = new Vector2(760, 620);
            window.Show();
        }

        private void OnEnable()
        {
            LoadPrefs();
        }

        private void OnDisable()
        {
            EditorApplication.update -= WatchUnityMcpPackageInstall;
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawHeader();
            DrawProjectSummary();
            DrawMainActions();
            DrawProjectDefaults();
            DrawAdvancedTools();
            DrawRecentLogs();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("AI Image Pipeline", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("面向当前 Unity 项目的 AI 图片资源流水线。通常只需要依次执行：初始化/更新 → 构建 Image MCP Server → 一键配置 Unity MCP → 启用到 Codex。不会保存任何 API Key。", MessageType.Info);
        }

        private void DrawProjectSummary()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Project Summary", EditorStyles.boldLabel);

            DrawReadonly("Project Root", ToForwardSlash(ProjectRoot));
            DrawReadonly("Output Root", NormalizeAssetPath(_outputRootAssetPath));
            DrawReadonly("MCP Server", NormalizeRelativePath(_mcpServerRelativePath));
            DrawReadonly("Provider / Model", $"{_provider.Trim()} / {_openAIModel.Trim()}");
            DrawReadonly("OpenAI Proxy", string.IsNullOrWhiteSpace(_proxyUrl) ? "Not set" : _proxyUrl.Trim());
            DrawReadonly("Codex Active Project", GetActiveProjectDescription());
            DrawReadonly("Unity MCP", GetUnityMcpSummary());

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            DrawStateBadge("MCP", Directory.Exists(GetMcpServerAbsolutePath()) ? "Installed" : "Missing", Directory.Exists(GetMcpServerAbsolutePath()));
            DrawStateBadge("Build", File.Exists(GetMcpIndexPath()) ? "Built" : "Missing", File.Exists(GetMcpIndexPath()));
            DrawStateBadge("AGENTS", IsAgentsMerged() ? "Merged" : "Missing", IsAgentsMerged());
            DrawStateBadge("Codex", IsCurrentProjectActiveInCodex() ? "Active" : "Not Active", IsCurrentProjectActiveInCodex());
            UnityMcpServerLocation unityMcp = LocateUnityMcpServer();
            DrawStateBadge("Unity MCP", unityMcp.EntryFound ? "Ready" : unityMcp.PackageFound ? "Build Required" : "Missing", unityMcp.EntryFound);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawMainActions()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Main Workflow", EditorStyles.boldLabel);

            if (GUILayout.Button("1. 初始化 / 更新当前项目", GUILayout.Height(34)))
                InitializeOrUpdateProject();

            if (GUILayout.Button("2. 构建 MCP Server", GUILayout.Height(34)))
                BuildMcpServerSmart();

            if (GUILayout.Button("3. 一键配置 Unity MCP", GUILayout.Height(34)))
                OneClickSetupUnityMcp();

            if (GUILayout.Button("4. 启用当前项目到 Codex", GUILayout.Height(34)))
                ActivateCurrentProjectInCodex();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("本地检查 / Test Setup", GUILayout.Height(26)))
                RunLocalSetupCheck(showDialog: true);

            if (GUILayout.Button("打开 MCP Server 文件夹", GUILayout.Height(26)))
                OpenMcpServerFolder();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("首次使用推荐：1 → 2 → 3 → 4。构建会打开独立 PowerShell 窗口执行 npm install 与 npm run build，Unity 不会被阻塞。Unity MCP 一键配置会尝试安装/定位/构建/写入 Codex 配置，但不会修改 Scene / Prefab / ScriptableObject。", MessageType.None);
            EditorGUILayout.EndVertical();
        }

        private void DrawProjectDefaults()
        {
            EditorGUILayout.Space(8);
            _showProjectDefaults = EditorGUILayout.Foldout(_showProjectDefaults, "Project Defaults / 默认参数", true);
            if (!_showProjectDefaults) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawOutputRootField();
            _defaultOutputFolder = EditorGUILayout.TextField("Default outputFolder", _defaultOutputFolder);
            _mcpServerRelativePath = EditorGUILayout.TextField("Server install path", _mcpServerRelativePath);
            _provider = EditorGUILayout.TextField("Default provider", _provider);
            _openAIModel = EditorGUILayout.TextField("OpenAI image model", _openAIModel);
            _proxyUrl = EditorGUILayout.TextField("OpenAI proxy URL", _proxyUrl);
            _volcengineModel = EditorGUILayout.TextField("Volcengine model", _volcengineModel);
            _volcengineBaseUrl = EditorGUILayout.TextField("Volcengine base URL", _volcengineBaseUrl);
            _previewSize = EditorGUILayout.TextField("Default preview size", _previewSize);
            _previewQuality = EditorGUILayout.TextField("Default preview quality", _previewQuality);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Unity MCP", EditorStyles.boldLabel);
            _unityMcpHost = EditorGUILayout.TextField("Unity MCP host", _unityMcpHost);
            _unityMcpPort = EditorGUILayout.IntField("Unity MCP port", _unityMcpPort);
            _unityMcpRequestTimeout = EditorGUILayout.IntField("Unity MCP timeout", _unityMcpRequestTimeout);
            _unityMcpServerId = EditorGUILayout.TextField("Codex server id", _unityMcpServerId);
            _unityMcpInstallSource = EditorGUILayout.TextField("Unity MCP install source", _unityMcpInstallSource);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("保存默认参数", GUILayout.Height(24)))
            {
                SavePrefs();
                AddLog("Project defaults saved.");
            }

            if (GUILayout.Button("恢复默认值", GUILayout.Height(24)))
            {
                ResetPrefsToDefaults();
                AddLog("Project defaults reset.");
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawAdvancedTools()
        {
            EditorGUILayout.Space(8);
            _showAdvancedTools = EditorGUILayout.Foldout(_showAdvancedTools, "Advanced Tools / 高级工具", true);
            if (!_showAdvancedTools) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.HelpBox("以下按钮用于排查或局部更新。日常使用通常不需要单独点击。", MessageType.None);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy/Update MCP Server")) CopyMcpServerTemplates();
            if (GUILayout.Button("Merge AGENTS.md")) MergeAgents();
            if (GUILayout.Button("Generate Codex Config")) GenerateCodexConfigFragment();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Force Clean Rebuild")) RunNpmInstallAndBuildInPowerShell(cleanInstall: true);
            if (GUILayout.Button("Normal Rebuild")) RunNpmInstallAndBuildInPowerShell(cleanInstall: false);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open Output Folder")) OpenOutputFolder();
            if (GUILayout.Button("Open Generated Config Folder")) OpenGeneratedConfigFolder();
            if (GUILayout.Button("Open User Codex Config")) OpenUserCodexConfig();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Repair User Codex Config")) ActivateCurrentProjectInCodex();
            if (GUILayout.Button("Preview Codex Config")) PreviewCodexConfig();
            if (GUILayout.Button("Reimport Generated Sprites")) AIImageImportPostprocessor.ReimportGeneratedSpritesMenu();
            EditorGUILayout.EndHorizontal();

            DrawUnityMcpTools();

            EditorGUILayout.EndVertical();
        }

        private void DrawUnityMcpTools()
        {
            EditorGUILayout.Space(8);
            _showUnityMcpTools = EditorGUILayout.Foldout(_showUnityMcpTools, "Unity MCP Tools / Unity MCP 工具", true);
            if (!_showUnityMcpTools) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            UnityMcpServerLocation location = LocateUnityMcpServer();
            EditorGUILayout.LabelField("Unity MCP Status", EditorStyles.boldLabel);
            DrawReadonly("Package", location.PackageFound ? ToForwardSlash(location.PackageRoot) : "Missing");
            DrawReadonly("Entry", location.EntryFound ? ToForwardSlash(location.EntryPath) : location.PackageFound ? "Build required: Server~/build/index.js missing" : "Not found");
            DrawReadonly("Endpoint", $"{_unityMcpHost}:{_unityMcpPort}, timeout={_unityMcpRequestTimeout}s");
            DrawReadonly("Install Source", string.IsNullOrWhiteSpace(_unityMcpInstallSource) ? DefaultUnityMcpInstallSource : _unityMcpInstallSource.Trim());

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("One-Click Setup")) OneClickSetupUnityMcp();
            if (GUILayout.Button("Install / Update")) InstallOrUpdateUnityMcpPackage();
            if (GUILayout.Button("Build Server")) BuildUnityMcpServer();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Configure Codex")) ConfigureUnityMcp();
            if (GUILayout.Button("Test Port")) ShowUnityMcpPortTest();
            if (GUILayout.Button("Open Server Window")) OpenUnityMcpServerWindow();
            if (GUILayout.Button("Open Package Folder")) OpenUnityMcpPackageFolder(location);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("One-Click Setup 会按顺序尝试：安装 Unity MCP → 构建 Server~ → 写入 Codex 配置 → 测试端口。Package 安装和 npm 构建可能触发 Unity 编译或打开 PowerShell，完成后如状态仍未刷新，请重新点击 One-Click Setup。", MessageType.None);
            EditorGUILayout.EndVertical();
        }

        private void DrawRecentLogs()
        {
            EditorGUILayout.Space(8);
            _showRecentLogs = EditorGUILayout.Foldout(_showRecentLogs, "Recent Actions / 最近操作", true);
            if (!_showRecentLogs) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.MinHeight(86), GUILayout.MaxHeight(160));
            EditorGUILayout.SelectableLabel(_recentLogBuilder.Length > 0 ? _recentLogBuilder.ToString() : "No recent actions.", EditorStyles.textArea, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear Logs", GUILayout.Width(100)))
                _recentLogBuilder.Length = 0;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawOutputRootField()
        {
            EditorGUILayout.BeginHorizontal();
            _outputRootAssetPath = EditorGUILayout.TextField("Image output root", _outputRootAssetPath);
            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                string selected = EditorUtility.OpenFolderPanel("Select output folder under Assets", AbsoluteFromProjectRelative(_outputRootAssetPath), string.Empty);
                if (!string.IsNullOrEmpty(selected))
                {
                    string relative = TryMakeProjectRelative(selected);
                    if (string.IsNullOrEmpty(relative) || !relative.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                        EditorUtility.DisplayDialog("Invalid Folder", "输出目录必须位于当前 Unity 项目的 Assets 目录下。", "OK");
                    else
                        _outputRootAssetPath = relative;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawReadonly(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(label);
            EditorGUILayout.SelectableLabel(value, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawStateBadge(string label, string state, bool good)
        {
            GUIStyle style = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                richText = true
            };

            string mark = good ? "<color=#3fb950>●</color>" : "<color=#d29922>●</color>";
            GUILayout.Label($"{mark} <b>{label}</b>\n{state}", style, GUILayout.Height(44));
        }

        private void InitializeOrUpdateProject()
        {
            SavePrefs();
            EnsureOutputFolder();
            CopyMcpServerTemplates();
            MergeAgents();
            GenerateCodexConfigFragment();
            AssetDatabase.Refresh();
            AddLog("Project setup applied. Next step: build MCP Server.");
            EditorUtility.DisplayDialog("Project Setup Completed", "当前项目初始化/更新完成。下一步建议点击：构建 MCP Server。", "OK");
        }

        private void BuildMcpServerSmart()
        {
            bool cleanInstall = ShouldUseCleanBuild();
            RunNpmInstallAndBuildInPowerShell(cleanInstall);
            AddLog(cleanInstall ? "Started clean MCP build in PowerShell." : "Started MCP build in PowerShell.");
        }

        private void ActivateCurrentProjectInCodex()
        {
            MergeConfigIntoUserCodexConfig(confirm: true);
        }

        private bool RunLocalSetupCheck(bool showDialog)
        {
            StringBuilder report = new StringBuilder();
            bool ok = true;

            ok &= AppendCheck(report, "Output folder", Directory.Exists(AbsoluteFromProjectRelative(_outputRootAssetPath)), NormalizeAssetPath(_outputRootAssetPath));
            ok &= AppendCheck(report, "MCP server folder", Directory.Exists(GetMcpServerAbsolutePath()), NormalizeRelativePath(_mcpServerRelativePath));
            ok &= AppendCheck(report, "MCP dist/index.js", File.Exists(GetMcpIndexPath()), ToForwardSlash(GetMcpIndexPath()));
            ok &= AppendCheck(report, "AGENTS.md merged", IsAgentsMerged(), ToForwardSlash(Path.Combine(ProjectRoot, "AGENTS.md")));
            ok &= AppendCheck(report, "Codex fragment", File.Exists(GetCodexFragmentPath()), ToForwardSlash(GetCodexFragmentPath()));
            ok &= AppendCheck(report, "User Codex config active", IsCurrentProjectActiveInCodex(), ToForwardSlash(GetUserCodexConfigPath()));
            UnityMcpServerLocation unityMcp = LocateUnityMcpServer();
            ok &= AppendCheck(report, "Unity MCP package", unityMcp.PackageFound, unityMcp.PackageRoot ?? "Not found");
            ok &= AppendCheck(report, "Unity MCP server entry", unityMcp.EntryFound, unityMcp.EntryPath ?? "Run Force Install Server / npm build first");
            ok &= AppendCheck(report, "Unity MCP port", TestUnityMcpPort(), $"{_unityMcpHost}:{_unityMcpPort}");
            ok &= AppendCheck(report, "OPENAI_API_KEY", !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY", EnvironmentVariableTarget.User)) || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY")), "Environment variable");

            string message = report.ToString();
            AddLog("Local setup check finished." + Environment.NewLine + message.TrimEnd());

            if (showDialog)
                EditorUtility.DisplayDialog(ok ? "Setup Check Passed" : "Setup Check Needs Attention", message, "OK");

            return ok;
        }

        private static bool AppendCheck(StringBuilder builder, string name, bool passed, string detail)
        {
            builder.AppendLine($"{(passed ? "[OK]" : "[!!]")} {name}: {detail}");
            return passed;
        }

        private bool ShouldUseCleanBuild()
        {
            string serverRoot = GetMcpServerAbsolutePath();
            string nodeModules = Path.Combine(serverRoot, "node_modules");
            return !Directory.Exists(nodeModules) || !File.Exists(GetMcpIndexPath());
        }

        private void LoadPrefs()
        {
            _outputRootAssetPath = EditorPrefs.GetString(OutputRootPrefsKey, DefaultOutputRoot);
            _mcpServerRelativePath = EditorPrefs.GetString(McpServerPathPrefsKey, DefaultMcpServerPath);
            _defaultOutputFolder = EditorPrefs.GetString(DefaultOutputFolderPrefsKey, DefaultOutputFolder);
            _proxyUrl = EditorPrefs.GetString(ProxyUrlPrefsKey, DefaultProxyUrl);
            _provider = EditorPrefs.GetString(ProviderPrefsKey, DefaultProvider);
            _openAIModel = EditorPrefs.GetString(OpenAIModelPrefsKey, DefaultOpenAIModel);
            _volcengineModel = EditorPrefs.GetString(VolcengineModelPrefsKey, DefaultVolcengineModel);
            _volcengineBaseUrl = EditorPrefs.GetString(VolcengineBaseUrlPrefsKey, DefaultVolcengineBaseUrl);
            _previewSize = EditorPrefs.GetString(PreviewSizePrefsKey, DefaultPreviewSize);
            _previewQuality = EditorPrefs.GetString(PreviewQualityPrefsKey, DefaultPreviewQuality);
            _unityMcpHost = EditorPrefs.GetString(UnityMcpHostPrefsKey, DefaultUnityMcpHost);
            _unityMcpPort = EditorPrefs.GetInt(UnityMcpPortPrefsKey, DefaultUnityMcpPort);
            _unityMcpRequestTimeout = EditorPrefs.GetInt(UnityMcpRequestTimeoutPrefsKey, DefaultUnityMcpRequestTimeout);
            _unityMcpServerId = EditorPrefs.GetString(UnityMcpServerIdPrefsKey, DefaultUnityMcpServerId);
            _unityMcpInstallSource = EditorPrefs.GetString(UnityMcpInstallSourcePrefsKey, DefaultUnityMcpInstallSource);
        }

        private void SavePrefs()
        {
            _outputRootAssetPath = NormalizeAssetPath(_outputRootAssetPath);
            _mcpServerRelativePath = NormalizeRelativePath(_mcpServerRelativePath);
            _defaultOutputFolder = NormalizeRelativePath(_defaultOutputFolder);
            _unityMcpHost = string.IsNullOrWhiteSpace(_unityMcpHost) ? DefaultUnityMcpHost : _unityMcpHost.Trim();
            _unityMcpPort = Mathf.Clamp(_unityMcpPort, 1, 65535);
            _unityMcpRequestTimeout = Mathf.Max(1, _unityMcpRequestTimeout);
            _unityMcpServerId = string.IsNullOrWhiteSpace(_unityMcpServerId) ? DefaultUnityMcpServerId : SanitizeTomlIdentifier(_unityMcpServerId);
            _unityMcpInstallSource = string.IsNullOrWhiteSpace(_unityMcpInstallSource) ? DefaultUnityMcpInstallSource : _unityMcpInstallSource.Trim();

            EditorPrefs.SetString(OutputRootPrefsKey, _outputRootAssetPath);
            EditorPrefs.SetString(McpServerPathPrefsKey, _mcpServerRelativePath);
            EditorPrefs.SetString(DefaultOutputFolderPrefsKey, _defaultOutputFolder);
            EditorPrefs.SetString(ProxyUrlPrefsKey, _proxyUrl.Trim());
            EditorPrefs.SetString(ProviderPrefsKey, _provider.Trim());
            EditorPrefs.SetString(OpenAIModelPrefsKey, _openAIModel.Trim());
            EditorPrefs.SetString(VolcengineModelPrefsKey, _volcengineModel.Trim());
            EditorPrefs.SetString(VolcengineBaseUrlPrefsKey, _volcengineBaseUrl.Trim());
            EditorPrefs.SetString(PreviewSizePrefsKey, _previewSize.Trim());
            EditorPrefs.SetString(PreviewQualityPrefsKey, _previewQuality.Trim());
            EditorPrefs.SetString(UnityMcpHostPrefsKey, _unityMcpHost);
            EditorPrefs.SetInt(UnityMcpPortPrefsKey, _unityMcpPort);
            EditorPrefs.SetInt(UnityMcpRequestTimeoutPrefsKey, _unityMcpRequestTimeout);
            EditorPrefs.SetString(UnityMcpServerIdPrefsKey, _unityMcpServerId);
            EditorPrefs.SetString(UnityMcpInstallSourcePrefsKey, _unityMcpInstallSource);
        }

        private void ResetPrefsToDefaults()
        {
            _outputRootAssetPath = DefaultOutputRoot;
            _mcpServerRelativePath = DefaultMcpServerPath;
            _defaultOutputFolder = DefaultOutputFolder;
            _proxyUrl = DefaultProxyUrl;
            _provider = DefaultProvider;
            _openAIModel = DefaultOpenAIModel;
            _volcengineModel = DefaultVolcengineModel;
            _volcengineBaseUrl = DefaultVolcengineBaseUrl;
            _previewSize = DefaultPreviewSize;
            _previewQuality = DefaultPreviewQuality;
            _unityMcpHost = DefaultUnityMcpHost;
            _unityMcpPort = DefaultUnityMcpPort;
            _unityMcpRequestTimeout = DefaultUnityMcpRequestTimeout;
            _unityMcpServerId = DefaultUnityMcpServerId;
            _unityMcpInstallSource = DefaultUnityMcpInstallSource;
            SavePrefs();
        }

        private void EnsureOutputFolder()
        {
            string absolute = AbsoluteFromProjectRelative(_outputRootAssetPath);
            Directory.CreateDirectory(absolute);
            AssetDatabase.Refresh();
        }

        private void CopyMcpServerTemplates()
        {
            SavePrefs();

            string source = AbsoluteFromAssetPath(ServerTemplateRoot);
            string target = GetMcpServerAbsolutePath();

            if (!Directory.Exists(source))
            {
                EditorUtility.DisplayDialog("Template Missing", $"MCP server template not found:\n{source}", "OK");
                return;
            }

            CopyDirectory(source, target);
            DeleteUnsafeNpmLockFiles(target);
            WriteLocalNpmConfig(target);
            AddLog($"MCP server copied to: {ToForwardSlash(target)}");
            Debug.Log($"AI Image MCP server copied to: {ToForwardSlash(target)}");
        }

        private void MergeAgents()
        {
            SavePrefs();

            string templatePath = AbsoluteFromAssetPath(AgentsTemplatePath);
            string template = File.Exists(templatePath) ? File.ReadAllText(templatePath, Encoding.UTF8) : CreateFallbackAgentsTemplate();
            string block = BuildAgentsBlock(template);
            string agentsPath = Path.Combine(ProjectRoot, "AGENTS.md");
            string existing = File.Exists(agentsPath) ? File.ReadAllText(agentsPath, Encoding.UTF8) : string.Empty;

            const string begin = "<!-- AI_IMAGE_PIPELINE_BEGIN -->";
            const string end = "<!-- AI_IMAGE_PIPELINE_END -->";
            string wrapped = begin + Environment.NewLine + block.Trim() + Environment.NewLine + end;

            string merged;
            if (existing.Contains(begin) && existing.Contains(end))
            {
                merged = Regex.Replace(existing, Regex.Escape(begin) + ".*?" + Regex.Escape(end), wrapped, RegexOptions.Singleline);
            }
            else
            {
                merged = string.IsNullOrWhiteSpace(existing) ? wrapped + Environment.NewLine : existing.TrimEnd() + Environment.NewLine + Environment.NewLine + wrapped + Environment.NewLine;
            }

            File.WriteAllText(agentsPath, merged, Encoding.UTF8);
            AddLog($"AGENTS.md merged: {ToForwardSlash(agentsPath)}");
            Debug.Log($"AGENTS.md merged: {ToForwardSlash(agentsPath)}");
        }

        private void GenerateCodexConfigFragment()
        {
            SavePrefs();

            string codexFolder = Path.Combine(ProjectRoot, "Codex");
            Directory.CreateDirectory(codexFolder);

            string configPath = GetCodexFragmentPath();
            File.WriteAllText(configPath, BuildCodexConfig(), Encoding.UTF8);
            AddLog($"Codex config fragment generated: {ToForwardSlash(configPath)}");
            Debug.Log($"Codex config fragment generated: {ToForwardSlash(configPath)}");
        }

        private void MergeConfigIntoUserCodexConfig(bool confirm)
        {
            GenerateCodexConfigFragment();

            if (confirm && !EditorUtility.DisplayDialog("Enable Current Project", "即将修改用户级 ~/.codex/config.toml，并将 image_assets 指向当前 Unity 项目。旧的 image_assets 配置块会被替换，API Key 不会写入文件。继续？", "Enable", "Cancel"))
                return;

            string userConfigPath = GetUserCodexConfigPath();
            Directory.CreateDirectory(Path.GetDirectoryName(userConfigPath));

            string existing = File.Exists(userConfigPath) ? File.ReadAllText(userConfigPath, Encoding.UTF8) : string.Empty;
            string fragment = BuildCodexConfig();

            const string begin = "# AI_IMAGE_PIPELINE_CODEX_BEGIN";
            const string end = "# AI_IMAGE_PIPELINE_CODEX_END";
            string wrapped = begin + Environment.NewLine + fragment.Trim() + Environment.NewLine + end;

            string cleaned = RemoveExistingImageAssetsCodexBlocks(existing);
            string merged = string.IsNullOrWhiteSpace(cleaned)
                ? wrapped + Environment.NewLine
                : cleaned.TrimEnd() + Environment.NewLine + Environment.NewLine + wrapped + Environment.NewLine;

            string backupPath = userConfigPath + ".bak_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            if (File.Exists(userConfigPath))
                File.Copy(userConfigPath, backupPath, true);

            File.WriteAllText(userConfigPath, merged, Encoding.UTF8);
            AddLog($"User Codex config updated. Active project: {ToForwardSlash(ProjectRoot)}");
            Debug.Log($"User Codex config merged: {ToForwardSlash(userConfigPath)}");
            if (File.Exists(backupPath))
                Debug.Log($"Previous Codex config backup: {ToForwardSlash(backupPath)}");

            EditorUtility.DisplayDialog("Codex Config Updated", "当前项目已启用到 Codex。请重启 Codex，让 MCP 配置重新加载。", "OK");
        }

        /// <summary>
        /// 移除旧版或重复的 image_assets 配置块，避免 TOML duplicate key。
        /// </summary>
        private static string RemoveExistingImageAssetsCodexBlocks(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            const string begin = "# AI_IMAGE_PIPELINE_CODEX_BEGIN";
            const string end = "# AI_IMAGE_PIPELINE_CODEX_END";

            string withoutManagedBlocks = Regex.Replace(
                text,
                Regex.Escape(begin) + ".*?" + Regex.Escape(end) + @"\s*",
                string.Empty,
                RegexOptions.Singleline);

            string withoutRawImageAssetsTables = Regex.Replace(
                withoutManagedBlocks,
                @"(?ms)^\s*\[mcp_servers\.image_assets(?:\]|[.][^\]]*\])\s*\r?\n.*?(?=^\s*\[|\z)",
                string.Empty);

            return withoutRawImageAssetsTables.TrimEnd();
        }

        private void RunNpmInstallAndBuildInPowerShell(bool cleanInstall)
        {
            CopyMcpServerTemplates();

            string serverRoot = GetMcpServerAbsolutePath();
            if (!Directory.Exists(serverRoot))
            {
                EditorUtility.DisplayDialog("Server Missing", "MCP server folder does not exist. Copy templates first.", "OK");
                return;
            }

            WriteLocalNpmConfig(serverRoot);
            string scriptPath = Path.Combine(serverRoot, cleanInstall ? "clean-install-and-build.ps1" : "install-and-build.ps1");
            WriteNpmInstallScript(scriptPath, serverRoot, cleanInstall);

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-ExecutionPolicy Bypass -File \"" + scriptPath + "\"",
                    WorkingDirectory = serverRoot,
                    UseShellExecute = true,
                    CreateNoWindow = false
                };

                Process.Start(startInfo);
                AddLog($"PowerShell build started: {ToForwardSlash(scriptPath)}");
                Debug.Log($"Started MCP Server install/build in PowerShell: {ToForwardSlash(scriptPath)}");
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("PowerShell Failed", exception.Message, "OK");
            }
        }

        private void OpenMcpServerFolder()
        {
            string serverRoot = GetMcpServerAbsolutePath();
            Directory.CreateDirectory(serverRoot);
            EditorUtility.RevealInFinder(serverRoot);
        }

        private void OpenOutputFolder()
        {
            EnsureOutputFolder();
            EditorUtility.RevealInFinder(AbsoluteFromProjectRelative(_outputRootAssetPath));
        }

        private void OpenGeneratedConfigFolder()
        {
            string codexFolder = Path.Combine(ProjectRoot, "Codex");
            Directory.CreateDirectory(codexFolder);
            EditorUtility.RevealInFinder(codexFolder);
        }

        private void OpenUserCodexConfig()
        {
            string userConfigPath = GetUserCodexConfigPath();
            Directory.CreateDirectory(Path.GetDirectoryName(userConfigPath));
            if (!File.Exists(userConfigPath))
                File.WriteAllText(userConfigPath, string.Empty, Encoding.UTF8);
            EditorUtility.RevealInFinder(userConfigPath);
        }

        /// <summary>
        /// 生成独立 PowerShell 安装脚本，避免 Unity 主线程等待 npm，并强制使用本地 npm 配置。
        /// </summary>
        private void WriteNpmInstallScript(string scriptPath, string serverRoot, bool cleanInstall)
        {
            string normalizedServerRoot = ToForwardSlash(serverRoot);
            StringBuilder script = new StringBuilder();
            script.AppendLine("$ErrorActionPreference = 'Stop'");
            script.AppendLine("Write-Host 'AI Image Pipeline MCP Server Install' -ForegroundColor Cyan");
            script.AppendLine($"Write-Host 'Server Path: {EscapePowerShellSingleQuoted(normalizedServerRoot)}' -ForegroundColor Gray");
            script.AppendLine($"Set-Location '{EscapePowerShellSingleQuoted(normalizedServerRoot)}'");
            script.AppendLine("");
            script.AppendLine("Write-Host 'Node version:' -ForegroundColor Cyan");
            script.AppendLine("node -v");
            script.AppendLine("Write-Host 'npm version:' -ForegroundColor Cyan");
            script.AppendLine("npm -v");
            script.AppendLine("");

            if (cleanInstall)
            {
                script.AppendLine("Write-Host 'Cleaning node_modules and package-lock.json...' -ForegroundColor Yellow");
                script.AppendLine("Remove-Item -Recurse -Force node_modules -ErrorAction SilentlyContinue");
                script.AppendLine("Remove-Item -Force package-lock.json -ErrorAction SilentlyContinue");
                script.AppendLine("");
            }

            script.AppendLine("Write-Host 'Writing local .npmrc...' -ForegroundColor Cyan");
            script.AppendLine("@'");
            script.AppendLine("registry=https://registry.npmmirror.com/");
            script.AppendLine("audit=false");
            script.AppendLine("fund=false");
            script.AppendLine("fetch-retries=5");
            script.AppendLine("fetch-retry-mintimeout=20000");
            script.AppendLine("fetch-retry-maxtimeout=120000");
            script.AppendLine("'@ | Set-Content -Path '.npmrc' -Encoding UTF8");
            script.AppendLine("");
            script.AppendLine("Write-Host 'Installing dependencies...' -ForegroundColor Cyan");
            script.AppendLine("npm install --registry=https://registry.npmmirror.com/ --no-audit --no-fund --fetch-retries=5 --fetch-retry-mintimeout=20000 --fetch-retry-maxtimeout=120000");
            script.AppendLine("");
            script.AppendLine("Write-Host 'Building MCP server...' -ForegroundColor Cyan");
            script.AppendLine("npm run build");
            script.AppendLine("");
            script.AppendLine("if (Test-Path './dist/index.js') {");
            script.AppendLine("    Write-Host 'Build succeeded. dist/index.js exists.' -ForegroundColor Green");
            script.AppendLine("} else {");
            script.AppendLine("    throw 'Build failed: dist/index.js not found.'");
            script.AppendLine("}");
            script.AppendLine("");
            script.AppendLine("Write-Host ''");
            script.AppendLine("Read-Host 'Press Enter to close this window'");

            File.WriteAllText(scriptPath, script.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// 写入项目局部 npm 配置，避免继承用户级 .npmrc 中不可访问的 registry。
        /// </summary>
        private static void WriteLocalNpmConfig(string serverRoot)
        {
            Directory.CreateDirectory(serverRoot);
            string npmrcPath = Path.Combine(serverRoot, ".npmrc");
            string content = "registry=https://registry.npmmirror.com/" + Environment.NewLine +
                             "audit=false" + Environment.NewLine +
                             "fund=false" + Environment.NewLine +
                             "fetch-retries=5" + Environment.NewLine +
                             "fetch-retry-mintimeout=20000" + Environment.NewLine +
                             "fetch-retry-maxtimeout=120000" + Environment.NewLine;
            File.WriteAllText(npmrcPath, content, Encoding.UTF8);
        }

        private static void DeleteUnsafeNpmLockFiles(string serverRoot)
        {
            string lockPath = Path.Combine(serverRoot, "package-lock.json");
            if (File.Exists(lockPath))
                File.Delete(lockPath);
        }

        /// <summary>
        /// 一键执行 Unity MCP 安装、构建、Codex 配置与端口检查。
        /// </summary>
        private void OneClickSetupUnityMcp()
        {
            SavePrefs();
            SetUnityMcpEnvironmentVariables();

            UnityMcpServerLocation location = LocateUnityMcpServer();
            if (!location.PackageFound)
            {
                if (!EditorUtility.DisplayDialog("Install Unity MCP", "当前项目未找到 Unity MCP。将通过 Unity Package Manager 安装配置的 Git 包。安装可能触发 Unity 重新编译。继续？", "Install", "Cancel"))
                    return;

                StartUnityMcpPackageInstall(showCompletionDialog: true);
                return;
            }

            if (!location.EntryFound)
            {
                if (location.BuildRequired)
                {
                    if (!EditorUtility.DisplayDialog("Build Unity MCP Server", "Unity MCP 包已找到，但 Server~/build/index.js 不存在。将打开 PowerShell 执行 npm install 与 npm run build。构建完成后请重新点击 One-Click Setup。继续？", "Build", "Cancel"))
                        return;

                    RunUnityMcpServerBuildInPowerShell(location, cleanInstall: false);
                    return;
                }

                EditorUtility.DisplayDialog("Unity MCP Not Ready", "Unity MCP 包已找到，但未发现 Server~/package.json 或 Server~/build/index.js。请检查包结构是否完整。", "OK");
                return;
            }

            MergeUnityMcpConfigIntoUserCodexConfig(location);
            bool portReachable = TestUnityMcpPort();
            AddLog($"Unity MCP one-click setup completed. Port reachable: {portReachable}");
            EditorUtility.DisplayDialog(
                "Unity MCP Setup Completed",
                portReachable
                    ? "Unity MCP 已写入 Codex 配置，端口可连接。请重启 Codex 后测试 mcp_unity。"
                    : "Unity MCP 已写入 Codex 配置，但端口暂不可连接。请在 Unity MCP Server Window 中点击 Start Server，然后重启 Codex。",
                "OK");
        }

        /// <summary>
        /// 通过 Unity Package Manager 安装或更新 Unity MCP 包。
        /// </summary>
        private void InstallOrUpdateUnityMcpPackage()
        {
            SavePrefs();

            if (!EditorUtility.DisplayDialog("Install / Update Unity MCP", $"将通过 Unity Package Manager 添加：\n{_unityMcpInstallSource}\n\n如果项目已安装，Unity 会按 Package Manager 规则更新或复用该依赖。继续？", "Install / Update", "Cancel"))
                return;

            StartUnityMcpPackageInstall(showCompletionDialog: true);
        }

        private void StartUnityMcpPackageInstall(bool showCompletionDialog)
        {
            if (_unityMcpAddRequest != null && !_unityMcpAddRequest.IsCompleted)
            {
                EditorUtility.DisplayDialog("Unity MCP Install Running", "Unity MCP 安装请求正在执行，请等待 Package Manager 完成。", "OK");
                return;
            }

            try
            {
                _unityMcpAddRequest = Client.Add(_unityMcpInstallSource);
                EditorApplication.update -= WatchUnityMcpPackageInstall;
                EditorApplication.update += WatchUnityMcpPackageInstall;
                AddLog("Unity MCP package install started: " + _unityMcpInstallSource);
            }
            catch (Exception exception)
            {
                _unityMcpAddRequest = null;
                EditorUtility.DisplayDialog("Unity MCP Install Failed", exception.Message, "OK");
                AddLog("Unity MCP package install failed to start: " + exception.Message);
            }
        }

        private void WatchUnityMcpPackageInstall()
        {
            if (_unityMcpAddRequest == null || !_unityMcpAddRequest.IsCompleted)
                return;

            EditorApplication.update -= WatchUnityMcpPackageInstall;

            if (_unityMcpAddRequest.Status == StatusCode.Success)
            {
                string packageId = _unityMcpAddRequest.Result != null ? _unityMcpAddRequest.Result.packageId : _unityMcpInstallSource;
                AddLog("Unity MCP package installed: " + packageId);
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Unity MCP Installed", "Unity MCP 包安装请求已完成。如果 Unity 正在重新编译，请等待完成后重新打开本窗口并点击 One-Click Setup 继续构建与配置。", "OK");
            }
            else
            {
                string message = _unityMcpAddRequest.Error != null ? _unityMcpAddRequest.Error.message : "Unknown Package Manager error.";
                AddLog("Unity MCP package install failed: " + message);
                EditorUtility.DisplayDialog("Unity MCP Install Failed", message, "OK");
            }

            _unityMcpAddRequest = null;
        }

        /// <summary>
        /// 构建 Unity MCP 的 Server~ Node 项目。
        /// </summary>
        private void BuildUnityMcpServer()
        {
            SavePrefs();
            UnityMcpServerLocation location = LocateUnityMcpServer();
            if (!location.PackageFound)
            {
                EditorUtility.DisplayDialog("Unity MCP Missing", "未找到 Unity MCP 包。请先点击 Install / Update。", "OK");
                return;
            }

            if (!location.BuildRequired && location.EntryFound)
            {
                if (!EditorUtility.DisplayDialog("Rebuild Unity MCP Server", "Unity MCP Server 入口已存在。是否仍然重新执行 npm install 与 npm run build？", "Rebuild", "Cancel"))
                    return;
            }

            RunUnityMcpServerBuildInPowerShell(location, cleanInstall: !Directory.Exists(Path.Combine(location.ServerRoot, "node_modules")));
        }

        private void RunUnityMcpServerBuildInPowerShell(UnityMcpServerLocation location, bool cleanInstall)
        {
            if (location == null || string.IsNullOrWhiteSpace(location.ServerRoot) || !Directory.Exists(location.ServerRoot))
            {
                EditorUtility.DisplayDialog("Unity MCP Server Missing", "Unity MCP Server~ 目录不存在。", "OK");
                return;
            }

            WriteLocalNpmConfig(location.ServerRoot);
            string scriptPath = Path.Combine(location.ServerRoot, cleanInstall ? "ai-pipeline-clean-build-unity-mcp.ps1" : "ai-pipeline-build-unity-mcp.ps1");
            WriteNpmInstallScript(scriptPath, location.ServerRoot, cleanInstall);

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-ExecutionPolicy Bypass -File \"" + scriptPath + "\"",
                    WorkingDirectory = location.ServerRoot,
                    UseShellExecute = true,
                    CreateNoWindow = false
                };

                Process.Start(startInfo);
                AddLog($"Unity MCP server build started: {ToForwardSlash(scriptPath)}");
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Unity MCP Build Failed", exception.Message, "OK");
                AddLog("Unity MCP build failed to start: " + exception.Message);
            }
        }

        private void OpenUnityMcpServerWindow()
        {
            if (!EditorApplication.ExecuteMenuItem("Tools/MCP Unity/Server Window"))
            {
                EditorUtility.DisplayDialog("Open Unity MCP Server", "未能自动打开 Unity MCP Server Window。请手动查找菜单：Tools > MCP Unity > Server Window，然后点击 Start Server。", "OK");
            }
        }

        private void ConfigureUnityMcp()
        {
            SavePrefs();
            SetUnityMcpEnvironmentVariables();

            UnityMcpServerLocation location = LocateUnityMcpServer();
            if (!location.EntryFound)
            {
                string message = location.PackageFound
                    ? "Unity MCP 包已找到，但 Server~/build/index.js 不存在。请先在 Unity MCP Server Window 中执行 Force Install Server，或手动 npm install / npm run build。"
                    : "未找到 Unity MCP 包。请先通过 Package Manager 安装 Unity MCP。";

                AddLog("Unity MCP configure blocked: " + message);
                EditorUtility.DisplayDialog("Unity MCP Not Ready", message, "OK");
                return;
            }

            MergeUnityMcpConfigIntoUserCodexConfig(location);
            AddLog($"Unity MCP configured: {ToForwardSlash(location.EntryPath)}");
            EditorUtility.DisplayDialog("Unity MCP Configured", "Unity MCP 已写入 Codex 用户配置。请重启 Codex，并确保 Unity MCP Server Window 已启动。", "OK");
        }

        private void PreviewCodexConfig()
        {
            SavePrefs();
            string codexFolder = Path.Combine(ProjectRoot, "Codex");
            Directory.CreateDirectory(codexFolder);

            StringBuilder preview = new StringBuilder();
            preview.AppendLine("# AI Image Pipeline generated Codex preview");
            preview.AppendLine("# This file is only a preview and is not loaded by Codex automatically.");
            preview.AppendLine();
            preview.AppendLine("# >>> AI_PIPELINE:IMAGE_ASSETS");
            preview.AppendLine(BuildCodexConfig().TrimEnd());
            preview.AppendLine("# <<< AI_PIPELINE:IMAGE_ASSETS");
            preview.AppendLine();

            UnityMcpServerLocation location = LocateUnityMcpServer();
            if (location.EntryFound)
            {
                preview.AppendLine(BuildUnityMcpManagedBlock(location).TrimEnd());
            }
            else
            {
                preview.AppendLine("# Unity MCP entry not found. Configure Unity MCP after Server~/build/index.js exists.");
            }

            string previewPath = Path.Combine(codexFolder, "config.ai_pipeline.preview.toml");
            File.WriteAllText(previewPath, preview.ToString(), Encoding.UTF8);
            AddLog($"Codex config preview written: {ToForwardSlash(previewPath)}");
            EditorUtility.RevealInFinder(previewPath);
        }

        private void ShowUnityMcpPortTest()
        {
            SavePrefs();
            bool ok = TestUnityMcpPort();
            string message = ok
                ? $"Unity MCP endpoint is reachable: {_unityMcpHost}:{_unityMcpPort}"
                : $"Unity MCP endpoint is not reachable: {_unityMcpHost}:{_unityMcpPort}\n请确认 Unity MCP Server Window 已点击 Start Server。";

            AddLog("Unity MCP port test: " + message);
            EditorUtility.DisplayDialog(ok ? "Unity MCP Port OK" : "Unity MCP Port Not Reachable", message, "OK");
        }

        private bool TestUnityMcpPort()
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    IAsyncResult result = client.BeginConnect(_unityMcpHost, _unityMcpPort, null, null);
                    bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(1200));
                    if (!success)
                        return false;

                    client.EndConnect(result);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private void OpenUnityMcpPackageFolder(UnityMcpServerLocation location)
        {
            if (!location.PackageFound)
            {
                EditorUtility.DisplayDialog("Unity MCP Missing", "未找到 Unity MCP 包目录。", "OK");
                return;
            }

            EditorUtility.RevealInFinder(location.PackageRoot);
        }

        private void SetUnityMcpEnvironmentVariables()
        {
            Environment.SetEnvironmentVariable("UNITY_HOST", _unityMcpHost, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable("UNITY_PORT", _unityMcpPort.ToString(), EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable("UNITY_REQUEST_TIMEOUT", _unityMcpRequestTimeout.ToString(), EnvironmentVariableTarget.User);
        }

        private void MergeUnityMcpConfigIntoUserCodexConfig(UnityMcpServerLocation location)
        {
            string userConfigPath = GetUserCodexConfigPath();
            Directory.CreateDirectory(Path.GetDirectoryName(userConfigPath));

            string existing = File.Exists(userConfigPath) ? File.ReadAllText(userConfigPath, Encoding.UTF8) : string.Empty;
            string cleaned = RemoveExistingUnityMcpCodexBlocks(existing, _unityMcpServerId);
            string block = BuildUnityMcpManagedBlock(location);
            string merged = string.IsNullOrWhiteSpace(cleaned)
                ? block + Environment.NewLine
                : cleaned.TrimEnd() + Environment.NewLine + Environment.NewLine + block + Environment.NewLine;

            string backupPath = userConfigPath + ".bak_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            if (File.Exists(userConfigPath))
                File.Copy(userConfigPath, backupPath, true);

            File.WriteAllText(userConfigPath, merged, Encoding.UTF8);
            Debug.Log($"Unity MCP Codex config merged: {ToForwardSlash(userConfigPath)}");
            if (File.Exists(backupPath))
                Debug.Log($"Previous Codex config backup: {ToForwardSlash(backupPath)}");
        }

        private string BuildUnityMcpManagedBlock(UnityMcpServerLocation location)
        {
            const string begin = "# >>> AI_PIPELINE:UNITY_MCP";
            const string end = "# <<< AI_PIPELINE:UNITY_MCP";
            return begin + Environment.NewLine + BuildUnityMcpCodexConfig(location).TrimEnd() + Environment.NewLine + end;
        }

        private string BuildUnityMcpCodexConfig(UnityMcpServerLocation location)
        {
            return $"[mcp_servers.{_unityMcpServerId}]" + Environment.NewLine +
                   "enabled = true" + Environment.NewLine +
                   "command = \"node\"" + Environment.NewLine +
                   $"args = [\"{TomlPath(location.EntryPath)}\"]" + Environment.NewLine +
                   $"cwd = \"{TomlPath(ProjectRoot)}\"" + Environment.NewLine +
                   "startup_timeout_sec = 45" + Environment.NewLine +
                   "tool_timeout_sec = 180" + Environment.NewLine +
                   "env = { " +
                   $"UNITY_HOST = \"{EscapeToml(_unityMcpHost)}\", " +
                   $"UNITY_PORT = \"{_unityMcpPort}\", " +
                   $"UNITY_REQUEST_TIMEOUT = \"{_unityMcpRequestTimeout}\"" +
                   " }" + Environment.NewLine;
        }

        private static string RemoveExistingUnityMcpCodexBlocks(string text, string serverId)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            const string begin = "# >>> AI_PIPELINE:UNITY_MCP";
            const string end = "# <<< AI_PIPELINE:UNITY_MCP";

            string withoutManagedBlocks = Regex.Replace(
                text,
                Regex.Escape(begin) + ".*?" + Regex.Escape(end) + @"\s*",
                string.Empty,
                RegexOptions.Singleline);

            string escapedServerId = Regex.Escape(serverId);
            string withoutRawUnityMcpTable = Regex.Replace(
                withoutManagedBlocks,
                @"(?ms)^\s*\[mcp_servers\." + escapedServerId + @"(?:\]|[.][^\]]*\])\s*\r?\n.*?(?=^\s*\[|\z)",
                string.Empty);

            return withoutRawUnityMcpTable.TrimEnd();
        }

        private UnityMcpServerLocation LocateUnityMcpServer()
        {
            string[] packageCandidates =
            {
                Path.Combine(ProjectRoot, "Packages", "mcp-unity"),
                Path.Combine(ProjectRoot, "Packages", "com.gamelovers.mcp-unity")
            };

            foreach (string candidate in packageCandidates)
            {
                UnityMcpServerLocation location = BuildUnityMcpLocation(candidate);
                if (location.PackageFound)
                    return location;
            }

            string packageCache = Path.Combine(ProjectRoot, "Library", "PackageCache");
            if (Directory.Exists(packageCache))
            {
                foreach (string directory in Directory.GetDirectories(packageCache))
                {
                    string name = Path.GetFileName(directory).ToLowerInvariant();
                    if (!name.Contains("mcp") || !name.Contains("unity"))
                        continue;

                    UnityMcpServerLocation location = BuildUnityMcpLocation(directory);
                    if (location.PackageFound)
                        return location;
                }
            }

            return UnityMcpServerLocation.Missing();
        }

        private static UnityMcpServerLocation BuildUnityMcpLocation(string packageRoot)
        {
            string serverRoot = Path.Combine(packageRoot, "Server~");
            string entryPath = Path.Combine(serverRoot, "build", "index.js");
            string packageJsonPath = Path.Combine(serverRoot, "package.json");

            bool packageFound = Directory.Exists(packageRoot) && Directory.Exists(serverRoot);
            bool entryFound = File.Exists(entryPath);
            bool buildRequired = packageFound && !entryFound && File.Exists(packageJsonPath);

            return new UnityMcpServerLocation(packageFound, packageRoot, serverRoot, entryFound ? entryPath : null, buildRequired);
        }

        private string GetUnityMcpSummary()
        {
            UnityMcpServerLocation location = LocateUnityMcpServer();
            if (location.EntryFound)
                return $"Ready: {ToForwardSlash(location.EntryPath)}";
            if (location.PackageFound)
                return "Package found, build required";
            return "Missing";
        }

        private string BuildAgentsBlock(string template)
        {
            return template
                .Replace("{{PROJECT_ROOT}}", ToForwardSlash(ProjectRoot))
                .Replace("{{OUTPUT_ROOT_ASSET_PATH}}", NormalizeAssetPath(_outputRootAssetPath))
                .Replace("{{DEFAULT_OUTPUT_FOLDER}}", NormalizeRelativePath(_defaultOutputFolder))
                .Replace("{{IMAGE_PROVIDER}}", _provider.Trim())
                .Replace("{{OPENAI_IMAGE_MODEL}}", _openAIModel.Trim())
                .Replace("{{DEFAULT_PREVIEW_SIZE}}", _previewSize.Trim())
                .Replace("{{DEFAULT_PREVIEW_QUALITY}}", _previewQuality.Trim());
        }

        private string BuildCodexConfig()
        {
            string serverRoot = GetMcpServerAbsolutePath();
            string outputRoot = AbsoluteFromProjectRelative(_outputRootAssetPath);
            string indexPath = GetMcpIndexPath();

            return "[mcp_servers.image_assets]" + Environment.NewLine +
                   "command = \"node\"" + Environment.NewLine +
                   $"args = [\"{TomlPath(indexPath)}\"]" + Environment.NewLine +
                   $"cwd = \"{TomlPath(serverRoot)}\"" + Environment.NewLine + Environment.NewLine +
                   "env = { " +
                   $"IMAGE_MCP_PROJECT_ROOT = \"{TomlPath(ProjectRoot)}\", " +
                   $"IMAGE_MCP_OUTPUT_ROOT = \"{TomlPath(outputRoot)}\", " +
                   $"IMAGE_PROVIDER = \"{EscapeToml(_provider.Trim())}\", " +
                   $"OPENAI_IMAGE_MODEL = \"{EscapeToml(_openAIModel.Trim())}\", " +
                   $"OPENAI_PROXY_URL = \"{EscapeToml(_proxyUrl.Trim())}\", " +
                   $"VOLCENGINE_IMAGE_MODEL = \"{EscapeToml(_volcengineModel.Trim())}\", " +
                   $"VOLCENGINE_BASE_URL = \"{EscapeToml(_volcengineBaseUrl.Trim())}\"" +
                   " }" + Environment.NewLine + Environment.NewLine +
                   "env_vars = [\"OPENAI_API_KEY\", \"ARK_API_KEY\"]" + Environment.NewLine +
                   "startup_timeout_sec = 30" + Environment.NewLine +
                   "tool_timeout_sec = 300" + Environment.NewLine +
                   "enabled = true" + Environment.NewLine;
        }

        private static string CreateFallbackAgentsTemplate()
        {
            return "# AI Image Asset Pipeline Rules\n\nUse image_assets.start_image_generation_job and get_image_generation_job. Default outputFolder: `{{DEFAULT_OUTPUT_FOLDER}}`. Do not use synchronous image generation unless explicitly requested.\n";
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                string fileName = Path.GetFileName(file);
                if (fileName.Equals("package-lock.json", StringComparison.OrdinalIgnoreCase)) continue;
                if (fileName.Equals(".npmrc", StringComparison.OrdinalIgnoreCase)) continue;
                string targetFile = Path.Combine(targetDir, fileName);
                File.Copy(file, targetFile, true);
            }

            foreach (string directory in Directory.GetDirectories(sourceDir))
            {
                string targetSubDir = Path.Combine(targetDir, Path.GetFileName(directory));
                CopyDirectory(directory, targetSubDir);
            }
        }

        private bool IsAgentsMerged()
        {
            string path = Path.Combine(ProjectRoot, "AGENTS.md");
            if (!File.Exists(path)) return false;
            string text = File.ReadAllText(path, Encoding.UTF8);
            return text.Contains("AI_IMAGE_PIPELINE_BEGIN") && text.Contains("AI_IMAGE_PIPELINE_END");
        }

        private bool IsCurrentProjectActiveInCodex()
        {
            string path = GetUserCodexConfigPath();
            if (!File.Exists(path)) return false;
            string text = File.ReadAllText(path, Encoding.UTF8).Replace('\\', '/');
            return text.Contains("IMAGE_MCP_PROJECT_ROOT") && text.Contains(ToForwardSlash(ProjectRoot));
        }

        private string GetActiveProjectDescription()
        {
            string path = GetUserCodexConfigPath();
            if (!File.Exists(path)) return "No user config";

            string text = File.ReadAllText(path, Encoding.UTF8);
            Match match = Regex.Match(text, "IMAGE_MCP_PROJECT_ROOT\\s*=\\s*\"([^\"]+)\"");
            if (!match.Success) return "Not configured";

            string activeProject = match.Groups[1].Value.Replace('\\', '/');
            bool current = string.Equals(activeProject.TrimEnd('/'), ToForwardSlash(ProjectRoot).TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
            return current ? "Current project" : activeProject;
        }

        private string GetMcpServerAbsolutePath()
        {
            return AbsoluteFromProjectRelative(_mcpServerRelativePath);
        }

        private string GetMcpIndexPath()
        {
            return Path.Combine(GetMcpServerAbsolutePath(), "dist", "index.js");
        }

        private string GetCodexFragmentPath()
        {
            return Path.Combine(ProjectRoot, "Codex", "config.image_assets.generated.toml");
        }

        private static string GetUserCodexConfigPath()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, ".codex", "config.toml");
        }

        private static string SanitizeTomlIdentifier(string value)
        {
            string raw = string.IsNullOrWhiteSpace(value) ? DefaultUnityMcpServerId : value.Trim();
            StringBuilder builder = new StringBuilder(raw.Length);
            foreach (char ch in raw)
            {
                if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-')
                    builder.Append(ch);
            }

            return builder.Length > 0 ? builder.ToString() : DefaultUnityMcpServerId;
        }

        private sealed class UnityMcpServerLocation
        {
            public bool PackageFound { get; }
            public string PackageRoot { get; }
            public string ServerRoot { get; }
            public string EntryPath { get; }
            public bool EntryFound => !string.IsNullOrWhiteSpace(EntryPath) && File.Exists(EntryPath);
            public bool BuildRequired { get; }

            public UnityMcpServerLocation(bool packageFound, string packageRoot, string serverRoot, string entryPath, bool buildRequired)
            {
                PackageFound = packageFound;
                PackageRoot = packageRoot;
                ServerRoot = serverRoot;
                EntryPath = entryPath;
                BuildRequired = buildRequired;
            }

            public static UnityMcpServerLocation Missing()
            {
                return new UnityMcpServerLocation(false, null, null, null, false);
            }
        }

        private void AddLog(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            _recentLogBuilder.AppendLine(line);
            Debug.Log(line);
            Repaint();
        }

        private static string AbsoluteFromAssetPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(ProjectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string AbsoluteFromProjectRelative(string relativePath)
        {
            string normalized = NormalizeRelativePath(relativePath);
            return Path.GetFullPath(Path.Combine(ProjectRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string TryMakeProjectRelative(string absolutePath)
        {
            string normalizedAbsolute = Path.GetFullPath(absolutePath).Replace('\\', '/').TrimEnd('/');
            string normalizedRoot = Path.GetFullPath(ProjectRoot).Replace('\\', '/').TrimEnd('/') + "/";
            if (!normalizedAbsolute.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)) return null;
            return normalizedAbsolute.Substring(normalizedRoot.Length).Replace('\\', '/');
        }

        private static string NormalizeAssetPath(string path)
        {
            return NormalizeRelativePath(path);
        }

        private static string NormalizeRelativePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim().Trim('/');
        }

        private static string ToForwardSlash(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }

        private static string TomlPath(string path)
        {
            return EscapeToml(ToForwardSlash(Path.GetFullPath(path)));
        }

        private static string EscapeToml(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string EscapePowerShellSingleQuoted(string value)
        {
            return (value ?? string.Empty).Replace("'", "''");
        }
    }
}
#endif
