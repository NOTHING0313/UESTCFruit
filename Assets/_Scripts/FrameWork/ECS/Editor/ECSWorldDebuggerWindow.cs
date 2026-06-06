#if UNITY_EDITOR
using BuffSystem;
using Contracts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ECSFrameWork
{
/// <summary>
/// ECS World 独立调试窗口。
/// 该窗口只通过 World Debug API 读取运行时数据，不直接访问 ECS 内部 Manager / Store 字段。
/// </summary>
public sealed class ECSWorldDebuggerWindow : EditorWindow
{
    private enum DebuggerTab
    {
        Overview = 0,
        Entities = 1,
        Systems = 2,
        ArcheTypes = 3,
        ComponentStores = 4,
        Singletons = 5,
        WorldEvents = 6,
        Commands = 7,
        BuffDebug = 8
    }

    private const double DefaultRefreshInterval = 0.25d;
    private const double DefaultTargetScanInterval = 1.0d;
    private const int EntityPageSize = 64;
    private const int ArcheTypeEntityPreviewCount = 128;
    private const int CollectionPreviewCount = 8;
    private const int MaxObjectDepth = 2;
    private const float SidebarWidth = 260f;
    private const float EntityListWidth = 380f;

    private static readonly string[] TabNames =
    {
        "总览 Overview",
        "实体 Entities",
        "系统 Systems",
        "原型 ArcheTypes",
        "组件仓库 Stores",
        "单例 Singletons",
        "世界事件 Events",
        "命令 Commands",
        "Buff 调试"
    };

    private readonly List<IECSRuntimeDebugSource> _sources = new List<IECSRuntimeDebugSource>(8);
    private readonly List<string> _sourceLabels = new List<string>(8);
    private string[] _sourceLabelArray = Array.Empty<string>();

    private readonly List<Entity> _entities = new List<Entity>(256);
    private readonly List<Entity> _visibleEntities = new List<Entity>(256);
    private readonly List<Entity> _archeTypeEntities = new List<Entity>(128);
    private readonly List<Type> _componentTypes = new List<Type>(32);
    private readonly List<Type> _searchComponentTypes = new List<Type>(32);
    private readonly List<ComponentStoreDebugInfo> _componentStores = new List<ComponentStoreDebugInfo>(32);
    private readonly List<ArcheTypeDebugInfo> _archeTypes = new List<ArcheTypeDebugInfo>(32);
    private readonly List<SystemDebugInfo> _systems = new List<SystemDebugInfo>(32);
    private readonly List<SingletonDebugInfo> _singletons = new List<SingletonDebugInfo>(16);
    private readonly List<WorldEventDebugInfo> _events = new List<WorldEventDebugInfo>(16);
    private readonly List<CommandDebugFrame> _commandDebugFrames = new List<CommandDebugFrame>(64);
    private readonly List<FrameCommandHistoryFrameDebugInfo> _frameCommandHistoryFrames = new List<FrameCommandHistoryFrameDebugInfo>(64);
    private readonly List<BuffViewData> _buffDebugViews = new List<BuffViewData>(16);
    private readonly List<string> _buffDebugLogs = new List<string>(16);
    private readonly List<BuffCommandQueueFieldInfo> _buffCommandQueueFields = new List<BuffCommandQueueFieldInfo>(32);
    private readonly List<BuffCommandQueueStageSnapshot> _buffCommandQueueStages = new List<BuffCommandQueueStageSnapshot>(8);
    private string _buffDebugCopyText = string.Empty;

    /// <summary>组件和复杂字段的折叠状态缓存，使用 Entity/Component/Field 路径作为 key。</summary>
    private readonly Dictionary<string, bool> _foldouts = new Dictionary<string, bool>(128);

    private Vector2 _sidebarScroll;
    private Vector2 _contentScroll;
    private Vector2 _entityListScroll;
    private Vector2 _entityDetailScroll;
    private Vector2 _archeTypeListScroll;
    private Vector2 _archeTypeDetailScroll;
    private Vector2 _commandExecutionScroll;
    private Vector2 _frameCommandHistoryScroll;
    private Vector2 _buffDebugCopyScroll;

    private DebuggerTab _currentTab = DebuggerTab.Overview;
    private WorldDebugSnapshot _snapshot;
    private bool _hasData;
    private bool _autoRefresh = true;
    private bool _autoScanTargets = true;
    private float _refreshInterval = (float)DefaultRefreshInterval;
    private double _nextRefreshTime;
    private double _nextTargetScanTime;
    private int _selectedSourceIndex = -1;
    private int _selectedArcheTypeIndex = -1;
    private int _entityPageIndex;
    private Entity _selectedEntity = Entity.Invalid;
    private string _entitySearch = string.Empty;
    private string _lastEntitySearch = string.Empty;
    private string _systemSearch = string.Empty;
    private string _componentStoreSearch = string.Empty;
    private string _commandSearch = string.Empty;
    private bool _showOnlyFailedCommands;
    private string _lastRefreshError = string.Empty;
    private string _buffConfigIdText = "991001";
    private string _buffStackText = "1";
    private string _buffTickFramesText = "1";
    private string _buffTargetIdText = string.Empty;
    private string _buffTargetVersionText = string.Empty;
    private string _buffSourceIdText = string.Empty;
    private string _buffSourceVersionText = string.Empty;
    private Entity _buffDebugTarget = Entity.Invalid;
    private Entity _buffDebugSource = Entity.Invalid;
    private BuffDebugSnapshot _buffDebugSnapshot;
    private BuffDebugPreflight _buffDebugPreflight;
    private bool _hasBuffDebugSnapshot;
    private bool _hasBuffDebugPreflight;
    private World _cachedBuffSystemWorld;
    private BuffSystemCore _cachedBuffSystemCore;
    private SimulateRunner _buffDebugLogRunner;
    private BuffDebugBinding _buffDebugBinding;

    /// <summary>打开 ECS World Debugger 窗口。</summary>
    [MenuItem("Window/ECSFrameWork/World Debugger")]
    public static void Open()
    {
        ECSWorldDebuggerWindow window = GetWindow<ECSWorldDebuggerWindow>("ECS 调试器");
        window.minSize = new Vector2(1120f, 640f);
        window.Show();
    }

    /// <summary>窗口启用时刷新可用调试目标和当前数据。</summary>
    private void OnEnable()
    {
        RefreshTargets();
        RefreshData();
    }

    /// <summary>绘制 ECS World Debugger 主界面。</summary>
    private void OnGUI()
    {
        DrawTopBar();
        RefreshIfNeeded();

        if (!string.IsNullOrEmpty(_lastRefreshError))
            EditorGUILayout.HelpBox(_lastRefreshError, MessageType.Error);

        IECSRuntimeDebugSource source = GetCurrentSource();
        World world = GetCurrentWorld();

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("World Debugger 读取运行时数据。请进入 Play Mode，并选择已绑定 World 的调试源。", MessageType.Info);

        if (source == null)
        {
            EditorGUILayout.HelpBox("当前场景没有找到 IECSRuntimeDebugSource。请使用 TimeSimulator 或 ECSRuntimeDebugTarget 作为调试源。", MessageType.Info);
            return;
        }

        if (world == null)
        {
            EditorGUILayout.HelpBox("当前调试源没有绑定 World。请确认运行时初始化完成，并已绑定 World / SimulateRunner。", MessageType.Warning);
            return;
        }

        if (!_hasData)
        {
            EditorGUILayout.HelpBox("尚未刷新调试数据。", MessageType.Info);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        DrawSidebar(source);
        DrawContent(world, source);
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>自动刷新开启时让窗口持续重绘。</summary>
    private void Update()
    {
        if ((_autoRefresh || _autoScanTargets) && Application.isPlaying)
            Repaint();
    }

    /// <summary>绘制顶部目标选择、扫描和刷新控制栏。</summary>
    private void DrawTopBar()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField("ECS World 调试器", EditorStyles.boldLabel, GUILayout.Width(180f));

        if (GUILayout.Button("扫描 World", GUILayout.Width(100f)))
        {
            RefreshTargets();
            RefreshData();
        }

        _autoScanTargets = GUILayout.Toggle(_autoScanTargets, "自动扫描", "Button", GUILayout.Width(90f));
        DrawSourcePopup();
        DrawPingButton();

        GUILayout.FlexibleSpace();
        _autoRefresh = GUILayout.Toggle(_autoRefresh, "自动刷新", "Button", GUILayout.Width(110f));
        EditorGUILayout.LabelField("刷新间隔", GUILayout.Width(64f));
        _refreshInterval = EditorGUILayout.Slider(_refreshInterval, 0.05f, 2f, GUILayout.Width(120f));
        EditorGUILayout.LabelField(FormatSeconds(_refreshInterval), GUILayout.Width(70f));

        if (GUILayout.Button("刷新", GUILayout.Width(80f)))
            RefreshData();

        if (GUILayout.Button("Dump 日志", GUILayout.Width(76f)))
        {
            RefreshData();
            if (_hasData)
                Debug.Log($"[ECS World Debugger] {_snapshot}");
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    /// <summary>绘制调试源选择下拉框。</summary>
    private void DrawSourcePopup()
    {
        if (_sourceLabelArray.Length == 0)
        {
            _selectedSourceIndex = -1;
            EditorGUILayout.Popup("调试目标", 0, new[] { "无" }, GUILayout.MinWidth(260f));
            return;
        }

        int safeIndex = Mathf.Clamp(_selectedSourceIndex, 0, _sourceLabelArray.Length - 1);
        int nextIndex = EditorGUILayout.Popup("调试目标", safeIndex, _sourceLabelArray, GUILayout.MinWidth(260f));
        if (nextIndex == _selectedSourceIndex)
            return;

        _selectedSourceIndex = nextIndex;
        _selectedEntity = Entity.Invalid;
        ResetBuffDebugEntities();
        _selectedArcheTypeIndex = -1;
        _entityPageIndex = 0;
        RefreshData();
    }

    /// <summary>绘制定位当前调试源 GameObject 的按钮。</summary>
    private void DrawPingButton()
    {
        IECSRuntimeDebugSource source = GetCurrentSource();
        EditorGUI.BeginDisabledGroup(!(source is UnityEngine.Object));
        if (GUILayout.Button("定位", GUILayout.Width(58f)))
        {
            UnityEngine.Object sourceObject = source as UnityEngine.Object;
            if (sourceObject != null)
            {
                Selection.activeObject = sourceObject;
                EditorGUIUtility.PingObject(sourceObject);
            }
        }
        EditorGUI.EndDisabledGroup();
    }

    /// <summary>绘制左侧导航和核心状态摘要。</summary>
    private void DrawSidebar(IECSRuntimeDebugSource source)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(SidebarWidth));
        _sidebarScroll = EditorGUILayout.BeginScrollView(_sidebarScroll);

        EditorGUILayout.LabelField("运行时 Runtime", EditorStyles.boldLabel);
        DrawReadOnlyText("调试源 Source", source != null ? source.DebugSourceName : "无");
        DrawReadOnlyText("状态 State", _snapshot.currentState.ToString());
        DrawReadOnlyInt("帧 Frame", source != null && source.DebugRunner != null ? source.DebugRunner.CurrentFrameNumber : -1);
        DrawReadOnlyInt("实体 Entities", _snapshot.statistics.aliveEntityCount);
        DrawReadOnlyInt("系统 Systems", _snapshot.systemCount);
        DrawReadOnlyInt("仓库 Stores", _snapshot.componentStoreCount);
        DrawReadOnlyInt("原型 ArcheTypes", _snapshot.archeTypeCount);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("页面 Pages", EditorStyles.boldLabel);
        for (int i = 0; i < TabNames.Length; i++)
        {
            bool selected = (int)_currentTab == i;
            if (GUILayout.Toggle(selected, TabNames[i], "Button"))
                _currentTab = (DebuggerTab)i;
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    /// <summary>绘制右侧主内容区。</summary>
    private void DrawContent(World world, IECSRuntimeDebugSource source)
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        _contentScroll = EditorGUILayout.BeginScrollView(_contentScroll);

        switch (_currentTab)
        {
            case DebuggerTab.Overview:
                DrawOverviewTab(source);
                break;
            case DebuggerTab.Entities:
                DrawEntitiesTab(world);
                break;
            case DebuggerTab.Systems:
                DrawSystemsTab();
                break;
            case DebuggerTab.ArcheTypes:
                DrawArcheTypesTab(world);
                break;
            case DebuggerTab.ComponentStores:
                DrawComponentStoresTab();
                break;
            case DebuggerTab.Singletons:
                DrawSingletonsTab(world);
                break;
            case DebuggerTab.WorldEvents:
                DrawWorldEventsTab();
                break;
            case DebuggerTab.Commands:
                DrawCommandsTab(source);
                break;
            case DebuggerTab.BuffDebug:
                DrawBuffDebugTab(world, source);
                break;
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    /// <summary>根据自动扫描和自动刷新设置更新 Debug 数据。</summary>
    private void RefreshIfNeeded()
    {
        if (!Application.isPlaying)
            return;

        double time = EditorApplication.timeSinceStartup;
        if (_autoScanTargets && time >= _nextTargetScanTime)
        {
            RefreshTargets();
            _nextTargetScanTime = time + DefaultTargetScanInterval;
        }

        if (!_autoRefresh || time < _nextRefreshTime)
            return;

        RefreshData();
        _nextRefreshTime = time + Math.Max(0.05f, _refreshInterval);
    }

    /// <summary>刷新当前场景中可用的 ECS 调试源。</summary>
    private void RefreshTargets()
    {
        UnityEngine.Object previousObject = GetCurrentSource() as UnityEngine.Object;
        _sources.Clear();
        _sourceLabels.Clear();

#if UNITY_2023_1_OR_NEWER
        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true);
#endif
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
                continue;

            IECSRuntimeDebugSource source = behaviour as IECSRuntimeDebugSource;
            if (source == null)
                continue;

            _sources.Add(source);
            _sourceLabels.Add(CreateSourceLabel(source));
        }

        _sourceLabelArray = _sourceLabels.ToArray();

        if (_sources.Count == 0)
        {
            _selectedSourceIndex = -1;
            _hasData = false;
            ClearCachedData();
            return;
        }

        int previousIndex = IndexOfSourceObject(previousObject);
        if (previousIndex >= 0)
            _selectedSourceIndex = previousIndex;
        else if (_selectedSourceIndex < 0 || _selectedSourceIndex >= _sources.Count)
            _selectedSourceIndex = 0;
    }

    /// <summary>刷新当前调试源的 World Debug 数据。</summary>
    private void RefreshData()
    {
        _lastRefreshError = string.Empty;
        ClearCachedData();

        World world = GetCurrentWorld();
        if (world == null)
        {
            _hasData = false;
            return;
        }

        try
        {
            _snapshot = world.GetDebugSnapshot();
            world.FillAliveEntities(_entities);
            world.FillComponentStoreDebugInfos(_componentStores);
            world.FillArcheTypeDebugInfos(_archeTypes);
            world.FillSystemDebugInfos(_systems);
            world.FillSingletonDebugInfos(_singletons);
            world.FillWorldEventDebugInfos(_events);
            RefreshCommandData(GetCurrentSource());
            _hasData = true;

            EntityDebugInfo selectedInfo;
            if (_selectedEntity.IsValid && !world.TryGetEntityDebugInfo(_selectedEntity, out selectedInfo))
                _selectedEntity = Entity.Invalid;

            if (_selectedArcheTypeIndex >= _archeTypes.Count)
                _selectedArcheTypeIndex = -1;
        }
        catch (Exception exception)
        {
            _hasData = false;
            _lastRefreshError = exception.Message;
            Debug.LogException(exception);
        }
    }

    /// <summary>刷新当前调试源关联的命令调试数据。</summary>
    private void RefreshCommandData(IECSRuntimeDebugSource source)
    {
        _commandDebugFrames.Clear();
        _frameCommandHistoryFrames.Clear();

        IECSFrameCommandDebugSource commandSource = ResolveCommandDebugSource(source);
        if (commandSource == null)
            return;

        if (commandSource.DebugFrameCommandApplier != null)
            commandSource.DebugFrameCommandApplier.FillCommandDebugFrames(_commandDebugFrames);

        if (commandSource.DebugFrameCommandBuffer != null)
            commandSource.DebugFrameCommandBuffer.FillFrameCommandHistoryDebugFrames(_frameCommandHistoryFrames);
    }

    /// <summary>清空当前窗口缓存的 Debug 数据。</summary>
    private void ClearCachedData()
    {
        _entities.Clear();
        _visibleEntities.Clear();
        _archeTypeEntities.Clear();
        _componentTypes.Clear();
        _searchComponentTypes.Clear();
        _componentStores.Clear();
        _archeTypes.Clear();
        _systems.Clear();
        _singletons.Clear();
        _events.Clear();
        _commandDebugFrames.Clear();
        _frameCommandHistoryFrames.Clear();
    }

    /// <summary>绘制总览页。</summary>
    private void DrawOverviewTab(IECSRuntimeDebugSource source)
    {
        DrawSectionTitle("总览 Overview");
        EditorGUILayout.BeginHorizontal();
        DrawOverviewCard("世界 World", new[]
        {
            new DebugLine("状态 State", _snapshot.currentState.ToString()),
            new DebugLine("已创建实体 Created", _snapshot.statistics.createdEntityCount.ToString()),
            new DebugLine("存活实体 Alive", _snapshot.statistics.aliveEntityCount.ToString()),
            new DebugLine("空闲实体 Free", _snapshot.statistics.freeEntityCount.ToString()),
            new DebugLine("Entity 容量 Capacity", _snapshot.entityCapacity.ToString()),
            new DebugLine("ArcheType 版本", _snapshot.statistics.archeTypeVersion.ToString())
        });

        DrawOverviewCard("注册表 Registry", new[]
        {
            new DebugLine("组件类型 Component Types", _snapshot.componentTypeCount.ToString()),
            new DebugLine("组件仓库 Component Stores", _snapshot.componentStoreCount.ToString()),
            new DebugLine("原型 ArcheTypes", _snapshot.archeTypeCount.ToString()),
            new DebugLine("查询缓存 Query Cache", _snapshot.queryCacheCount.ToString())
        });

        DrawOverviewCard("运行时缓冲 Runtime Buffers", new[]
        {
            new DebugLine("系统 Systems", _snapshot.systemCount.ToString()),
            new DebugLine("单例 Singletons", _snapshot.singletonCount.ToString()),
            new DebugLine("WorldEvent 类型", _snapshot.worldEventTypeCount.ToString()),
            new DebugLine("WorldEvent 数量", _snapshot.worldEventCount.ToString()),
            new DebugLine("结构命令 Structural Changes", _snapshot.pendingStructuralChangeCount.ToString()),
            new DebugLine("系统命令 System Changes", _snapshot.pendingSystemChangeCount.ToString())
        });
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8f);
        DrawRunnerOverview(source != null ? source.DebugRunner : null);
    }

    /// <summary>绘制 Runner 状态总览。</summary>
    private static void DrawRunnerOverview(SimulateRunner runner)
    {
        DrawSectionTitle("运行器 Runner");
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        if (runner == null)
        {
            EditorGUILayout.HelpBox("当前调试源没有绑定 SimulateRunner。", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        DrawReadOnlyInt("帧计数 Frame Count", runner.FrameCount);
        DrawReadOnlyInt("当前帧 Current Frame", runner.CurrentFrameNumber);
        DrawReadOnlyInt("下一帧 Next Frame", runner.NextFrameNumber);
        DrawReadOnlyText("是否 Tick 中 Is Ticking", runner.IsTicking.ToString());
        DrawReadOnlySeconds("固定帧间隔 Tick Length", runner.TickLength);
        DrawReadOnlySeconds("Tick 计时器 Tick Counter", runner.TickCounter);
        EditorGUILayout.EndVertical();
    }

    /// <summary>绘制实体列表与选中实体详情。</summary>
    private void DrawEntitiesTab(World world)
    {
        DrawSectionTitle($"实体 Entities ({_entities.Count})");
        DrawEntityToolbar(world);

        EditorGUILayout.BeginHorizontal();
        DrawEntityList(world);
        DrawSelectedEntityDetail(world);
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>绘制实体搜索和分页工具栏。</summary>
    private void DrawEntityToolbar(World world)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUI.BeginChangeCheck();
        _entitySearch = EditorGUILayout.TextField("搜索 Entity / 组件", _entitySearch);
        if (EditorGUI.EndChangeCheck() || _entitySearch != _lastEntitySearch)
        {
            _entityPageIndex = 0;
            _lastEntitySearch = _entitySearch;
        }

        BuildVisibleEntities(world);
        int totalPage = Mathf.Max(1, Mathf.CeilToInt(_visibleEntities.Count / (float)EntityPageSize));
        _entityPageIndex = Mathf.Clamp(_entityPageIndex, 0, totalPage - 1);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"匹配 Matched: {_visibleEntities.Count} / {_entities.Count}", GUILayout.Width(190f));
        EditorGUI.BeginDisabledGroup(_entityPageIndex <= 0);
        if (GUILayout.Button("首页", GUILayout.Width(55f))) _entityPageIndex = 0;
        if (GUILayout.Button("上一页", GUILayout.Width(62f))) _entityPageIndex--;
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.LabelField($"页 {_entityPageIndex + 1} / {totalPage}", GUILayout.Width(100f));

        EditorGUI.BeginDisabledGroup(_entityPageIndex >= totalPage - 1);
        if (GUILayout.Button("下一页", GUILayout.Width(62f))) _entityPageIndex++;
        if (GUILayout.Button("末页", GUILayout.Width(55f))) _entityPageIndex = totalPage - 1;
        EditorGUI.EndDisabledGroup();
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    /// <summary>绘制当前页的 Entity 列表。</summary>
    private void DrawEntityList(World world)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(EntityListWidth));
        DrawEntityListHeader();
        _entityListScroll = EditorGUILayout.BeginScrollView(_entityListScroll, GUILayout.MinHeight(360f));

        int start = _entityPageIndex * EntityPageSize;
        int end = Mathf.Min(start + EntityPageSize, _visibleEntities.Count);
        for (int i = start; i < end; i++)
        {
            Entity entity = _visibleEntities[i];
            int componentCount = GetComponentCount(world, entity);
            bool selected = entity == _selectedEntity;
            string label = $"{entity.ID}:{entity.Version}    组件 Components={componentCount}";
            if (GUILayout.Toggle(selected, label, "Button"))
                _selectedEntity = entity;
        }

        if (_visibleEntities.Count == 0)
            EditorGUILayout.HelpBox("没有 Entity 匹配当前搜索。", MessageType.Info);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    /// <summary>绘制实体列表表头。</summary>
    private static void DrawEntityListHeader()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Entity 实体", EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>绘制选中 Entity 的详情与组件值。</summary>
    private void DrawSelectedEntityDetail(World world)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
        _entityDetailScroll = EditorGUILayout.BeginScrollView(_entityDetailScroll, GUILayout.MinHeight(360f));

        if (!_selectedEntity.IsValid)
        {
            EditorGUILayout.HelpBox("请先从左侧列表选择一个 Entity。", MessageType.Info);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            return;
        }

        if (!world.TryGetEntityDebugInfo(_selectedEntity, out EntityDebugInfo info))
        {
            EditorGUILayout.HelpBox($"{_selectedEntity} 已不再存活。", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            return;
        }

        DrawSectionTitle("当前选中 Entity");
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        DrawReadOnlyInt("ID", _selectedEntity.ID);
        DrawReadOnlyInt("Version", _selectedEntity.Version);
        DrawReadOnlyText("存活 Alive", info.isAlive.ToString());
        DrawReadOnlyInt("组件数量 Component Count", info.componentCount);
        DrawSelectableText("Mask", info.componentMask.ToString());
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6f);
        DrawSectionTitle("组件 Components");
        world.FillEntityComponentTypes(_selectedEntity, _componentTypes);
        DrawEntityComponentValueList(world, _selectedEntity, _componentTypes);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    /// <summary>绘制 System 调试页。</summary>
    private void DrawSystemsTab()
    {
        DrawSectionTitle($"系统 Systems ({_systems.Count})");
        _systemSearch = EditorGUILayout.TextField("搜索 Search", _systemSearch);
        DrawSystemHeader();

        for (int i = 0; i < _systems.Count; i++)
        {
            SystemDebugInfo info = _systems[i];
            if (!TextMatches(info.name, _systemSearch) && !TextMatches(ShortTypeName(info.systemType), _systemSearch))
                continue;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField(info.name, GUILayout.MinWidth(180f));
            EditorGUILayout.LabelField(info.sequence.ToString(), GUILayout.Width(110f));
            EditorGUILayout.LabelField(info.enabled.ToString(), GUILayout.Width(70f));
            EditorGUILayout.LabelField(FormatMilliseconds(info.lastTickMilliseconds), GUILayout.Width(90f));
            EditorGUILayout.LabelField(FormatMilliseconds(info.averageTickMilliseconds), GUILayout.Width(90f));
            EditorGUILayout.LabelField(FormatMilliseconds(info.maxTickMilliseconds), GUILayout.Width(90f));
            EditorGUILayout.LabelField(FormatTicks(info.tickCount), GUILayout.Width(90f));
            EditorGUILayout.EndHorizontal();
        }
    }

    /// <summary>绘制 ArcheType 分组页。</summary>
    private void DrawArcheTypesTab(World world)
    {
        DrawSectionTitle($"原型 ArcheTypes ({_archeTypes.Count})");
        EditorGUILayout.BeginHorizontal();
        DrawArcheTypeList();
        DrawSelectedArcheTypeDetail(world);
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>绘制 ArcheType 列表。</summary>
    private void DrawArcheTypeList()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(EntityListWidth));
        _archeTypeListScroll = EditorGUILayout.BeginScrollView(_archeTypeListScroll, GUILayout.MinHeight(360f));
        for (int i = 0; i < _archeTypes.Count; i++)
        {
            ArcheTypeDebugInfo info = _archeTypes[i];
            bool selected = i == _selectedArcheTypeIndex;
            string label = $"{i}. 实体 Entities={info.entityCount}, 组件 Components={info.componentCount}";
            if (GUILayout.Toggle(selected, label, "Button"))
                _selectedArcheTypeIndex = i;
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    /// <summary>绘制选中 ArcheType 的详情。</summary>
    private void DrawSelectedArcheTypeDetail(World world)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
        _archeTypeDetailScroll = EditorGUILayout.BeginScrollView(_archeTypeDetailScroll, GUILayout.MinHeight(360f));

        if (_selectedArcheTypeIndex < 0 || _selectedArcheTypeIndex >= _archeTypes.Count)
        {
            EditorGUILayout.HelpBox("请先从左侧列表选择一个 ArcheType。", MessageType.Info);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            return;
        }

        ArcheTypeDebugInfo info = _archeTypes[_selectedArcheTypeIndex];
        DrawSectionTitle("当前选中 ArcheType");
        DrawReadOnlyInt("Entity 数量", info.entityCount);
        DrawReadOnlyInt("组件数量 Component Count", info.componentCount);
        DrawSelectableText("Mask", info.mask.ToString());

        EditorGUILayout.Space(6f);
        DrawSectionTitle("组件类型 Component Types");
        world.FillComponentTypesByMask(info.mask, _componentTypes);
        DrawTypeList(_componentTypes);

        EditorGUILayout.Space(6f);
        DrawSectionTitle("实体 Entities");
        world.FillEntitiesByArcheType(info.mask, _archeTypeEntities);
        int count = Mathf.Min(_archeTypeEntities.Count, ArcheTypeEntityPreviewCount);
        for (int i = 0; i < count; i++)
            EditorGUILayout.LabelField(_archeTypeEntities[i].ToString());

        if (_archeTypeEntities.Count > ArcheTypeEntityPreviewCount)
            EditorGUILayout.HelpBox($"仅显示前 {ArcheTypeEntityPreviewCount} 个 Entity。", MessageType.Info);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    /// <summary>绘制 ComponentStore 调试页。</summary>
    private void DrawComponentStoresTab()
    {
        DrawSectionTitle($"组件仓库 Component Stores ({_componentStores.Count})");
        _componentStoreSearch = EditorGUILayout.TextField("搜索 Search", _componentStoreSearch);
        DrawComponentStoreHeader();

        for (int i = 0; i < _componentStores.Count; i++)
        {
            ComponentStoreDebugInfo info = _componentStores[i];
            string typeName = ShortTypeName(info.componentType);
            if (!TextMatches(typeName, _componentStoreSearch))
                continue;

            float loadFactor = info.capacity > 0 ? (float)info.count / info.capacity : 0f;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField(typeName, GUILayout.MinWidth(180f));
            EditorGUILayout.LabelField(info.registerID.ToString(), GUILayout.Width(70f));
            EditorGUILayout.LabelField(info.count.ToString(), GUILayout.Width(70f));
            EditorGUILayout.LabelField(info.capacity.ToString(), GUILayout.Width(90f));
            EditorGUILayout.LabelField(info.sparseCapacity.ToString(), GUILayout.Width(110f));
            EditorGUILayout.LabelField(loadFactor.ToString("P0"), GUILayout.Width(90f));
            EditorGUILayout.EndHorizontal();
        }
    }

    /// <summary>绘制 Singleton 调试页。</summary>
    private void DrawSingletonsTab(World world)
    {
        DrawSectionTitle($"单例 Singletons ({_singletons.Count})");
        if (_singletons.Count == 0)
        {
            EditorGUILayout.HelpBox("当前没有注册 SingletonComponent。", MessageType.Info);
            return;
        }

        for (int i = 0; i < _singletons.Count; i++)
        {
            SingletonDebugInfo info = _singletons[i];
            string title = ShortTypeName(info.componentType);
            string key = $"singleton:{title}:{info.entity.ID}:{info.entity.Version}";
            bool expanded = DrawFoldoutBox(key, title);
            if (expanded)
            {
                EditorGUI.indentLevel++;
                DrawReadOnlyText("Entity", info.entity.ToString());
                DrawReadOnlyText("存活 Alive", info.isAlive.ToString());
                if (info.isAlive && info.componentType != null)
                    DrawComponentDebugValue(world, info.entity, info.componentType, key);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
        }
    }

    /// <summary>绘制 WorldEvent 调试页。</summary>
    private void DrawWorldEventsTab()
    {
        DrawSectionTitle($"世界事件 World Events ({_events.Count})");
        DrawWorldEventHeader();
        for (int i = 0; i < _events.Count; i++)
        {
            WorldEventDebugInfo info = _events[i];
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField(ShortTypeName(info.eventType), GUILayout.MinWidth(180f));
            EditorGUILayout.LabelField(info.eventCount.ToString(), GUILayout.Width(80f));
            EditorGUILayout.LabelField(info.oldestFrame.ToString(), GUILayout.Width(110f));
            EditorGUILayout.LabelField(info.newestFrame.ToString(), GUILayout.Width(110f));
            EditorGUILayout.EndHorizontal();
        }
    }

    /// <summary>绘制帧命令调试页。</summary>
    private void DrawCommandsTab(IECSRuntimeDebugSource source)
    {
        DrawSectionTitle("命令 Commands");

        IECSFrameCommandDebugSource commandSource = ResolveCommandDebugSource(source);
        if (commandSource == null)
        {
            EditorGUILayout.HelpBox("当前 World 没有可用的 IECSFrameCommandDebugSource。请通过 TimeSimulator、ECSRuntimeDebugTarget 或当前调试启动器绑定 SimulationFrameCommandBuffer / SimulationFrameCommandApplier。", MessageType.Info);
            return;
        }

        if (!ReferenceEquals(commandSource, source))
            EditorGUILayout.HelpBox($"当前调试源不直接暴露命令数据，正在使用关联命令源：{GetCommandDebugSourceLabel(commandSource)}", MessageType.Info);

        SimulationFrameCommandBuffer commandBuffer = commandSource.DebugFrameCommandBuffer;
        SimulationFrameCommandApplier commandApplier = commandSource.DebugFrameCommandApplier;

        DrawCommandSummary(commandBuffer, commandApplier, source != null ? source.DebugRunner : null);
        DrawCommandToolbar(commandApplier);

        EditorGUILayout.Space(8f);
        DrawCommandExecutionHistory(commandApplier);

        EditorGUILayout.Space(8f);
        DrawFrameCommandHistory(commandBuffer);
    }

    /// <summary>绘制 BuffSystem 压缩 Buff 试点调试页。</summary>
    private void DrawBuffDebugTab(World world, IECSRuntimeDebugSource source)
    {
        DrawSectionTitle("BuffSystem 压缩 Buff 调试面板");
        EditorGUILayout.HelpBox("本页用于验证 configId=991001 是否走 CompressedExpiryFrameList。Entity 由当前 World.CreateEntity() 创建，不是 Unity GameObject。Source 默认等于 Target，Add / Remove / Query 使用同一组 Entity。Add / Remove 是队列命令，必须 Tick 后才会创建 / 移除 runtime；新建 runtime 的 ViewData 可能需要下一帧 Capture 后才可见。", MessageType.Info);

        _buffDebugBinding = ResolveBuffDebugBinding(source, world);
        _buffDebugLogRunner = _buffDebugBinding.runner;
        DrawBuffDebugBindingDiagnostics(_buffDebugBinding);

        if (!_buffDebugBinding.IsUsable)
        {
            EditorGUILayout.HelpBox("绑定失败：未找到同一 SimulationInitializer 下的 World / Runner / BuffSystemCore。请确认场景已启动，并在 ECS Debugger 中选择当前生产场景的调试源。", MessageType.Warning);
            return;
        }

        DrawBuffDebugInputs();
        DrawBuffDebugPreflightDiagnostics(_buffDebugBinding);
        DrawBuffDebugCommandQueueDiagnostics(_buffDebugBinding);
        DrawBuffDebugCompressedRuntimeTrace(_buffDebugBinding);
        DrawBuffDebugEntityControls(_buffDebugBinding);
        DrawBuffDebugActionButtons(_buffDebugBinding);
        DrawBuffDebugResult();
        DrawBuffDebugRuntimeStats();
        DrawBuffDebugViewList();
        DrawBuffDebugLogs();
    }

    private void DrawBuffDebugInputs()
    {
        DrawSectionTitle("基础输入");
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        _buffConfigIdText = EditorGUILayout.TextField("ConfigId", _buffConfigIdText);
        _buffStackText = EditorGUILayout.TextField("Stack", _buffStackText);
        _buffTickFramesText = EditorGUILayout.TextField("Tick 指定帧数", _buffTickFramesText);
        EditorGUILayout.EndVertical();
    }

    private void DrawBuffDebugCompressedRuntimeTrace(BuffDebugBinding binding)
    {
        DrawSectionTitle("Compressed Runtime 生命周期 Trace");
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.HelpBox("该区域只读取当前 World 中的 CompressedParallelBuffRuntimeComponent、BuffSystemCore pending remove 集合和 compressed lookup，不修改 BuffSystem runtime。", MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("执行 compressed 生命周期 Trace"))
            RunBuffCompressedRuntimeLifecycleTrace(binding);

        if (GUILayout.Button("复制 compressed 生命周期日志"))
            CopyBuffCompressedRuntimeTraceToClipboard();
        EditorGUILayout.EndHorizontal();

        BuffCommandQueueStageSnapshot latest = _buffCommandQueueStages.Count > 0 ? _buffCommandQueueStages[_buffCommandQueueStages.Count - 1] : default;
        if (_buffCommandQueueStages.Count == 0)
        {
            EditorGUILayout.LabelField("尚未记录生命周期 Trace。");
        }
        else
        {
            BuffCompressedRuntimeTraceSnapshot trace = latest.compressedTrace;
            DrawReadOnlyText("Latest Stage", latest.stage);
            DrawReadOnlyInt("Frame", latest.frame);
            DrawReadOnlyText("RuntimeExists", trace.runtimeExists.ToString());
            DrawReadOnlyText("RuntimeEntity", FormatEntity(trace.runtimeEntity));
            DrawReadOnlyText("RuntimeEntity alive", trace.runtimeEntityAlive.ToString());
            DrawReadOnlyText("Target", FormatEntity(trace.target));
            DrawReadOnlyText("Target alive", trace.targetAlive.ToString());
            DrawReadOnlyText("Source", FormatEntity(trace.source));
            DrawReadOnlyText("Source alive", trace.sourceAlive.ToString());
            DrawReadOnlyInt("ConfigId", trace.configId);
            DrawReadOnlyInt("LayerCount", trace.layerCount);
            DrawReadOnlyInt("CompressedRuntimeHandle", trace.compressedRuntimeHandle);
            DrawReadOnlyText("PendingRemove", trace.pendingRemove.ToString());
            DrawReadOnlyText("CompressedLookupHit", trace.compressedLookupHit.ToString());
            DrawReadOnlyText("LookupEntity", FormatEntity(trace.lookupEntity));
            DrawReadOnlyText("ExpireSummary", trace.expireSummary);
            EditorGUILayout.HelpBox(BuildBuffCompressedRuntimeLifecycleDiagnosis(), MessageType.Warning);

            if (trace.layers != null && trace.layers.Count > 0)
            {
                EditorGUILayout.LabelField("Layer 详情", EditorStyles.boldLabel);
                for (int i = 0; i < trace.layers.Count; i++)
                {
                    BuffCompressedLayerTrace layer = trace.layers[i];
                    EditorGUILayout.LabelField(
                        $"Layer[{layer.index}]",
                        $"LayerId={layer.layerId}, RuntimeHandle={layer.layerRuntimeHandle}, ExpireFrame={layer.expireFrame}, Remaining={layer.remainingFrames}, Elapsed={layer.elapsedFrames}, Ticks={layer.ticks}, Expired={layer.expired}");
                }
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawBuffDebugPreflightDiagnostics(BuffDebugBinding binding)
    {
        DrawSectionTitle("Definition / Command 诊断");
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("检查 Definition"))
            RefreshBuffDebugPreflight(binding, "检查 Definition", true);

        if (GUILayout.Button("检查 Add 前置条件"))
            RefreshBuffDebugPreflight(binding, "检查 Add 前置条件", true);

        if (GUILayout.Button("生成 Definition 诊断日志"))
            GenerateBuffDebugDefinitionCopyText(binding);

        if (GUILayout.Button("复制 Definition 诊断日志"))
            CopyBuffDebugDefinitionLogToClipboard(binding);
        EditorGUILayout.EndHorizontal();

        if (!_hasBuffDebugPreflight)
        {
            EditorGUILayout.HelpBox("尚未生成 Definition / Command 诊断。点击“检查 Definition”或执行 Add/Tick 后会刷新。", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        DrawReadOnlyText("DefinitionProvider 类型", _buffDebugPreflight.providerType);
        DrawReadOnlyText("DefinitionProvider 来源对象", _buffDebugPreflight.providerSource);
        DrawReadOnlyText("Provider Root Path", _buffDebugPreflight.providerRootPath);
        DrawReadOnlyInt("Loaded definition count", _buffDebugPreflight.loadedDefinitionCount);
        DrawReadOnlyText("Loaded configIds", _buffDebugPreflight.loadedConfigIds);
        DrawReadOnlyText("TryGetDefinition", _buffDebugPreflight.definitionFound.ToString());

        if (_buffDebugPreflight.definitionFound)
        {
            DrawReadOnlyInt("ConfigId", _buffDebugPreflight.definition.ConfigId);
            DrawReadOnlyText("Name", _buffDebugPreflight.definition.Name);
            DrawReadOnlyText("BuffType", _buffDebugPreflight.definition.BuffType.ToString());
            DrawReadOnlyText("TriggerType", _buffDebugPreflight.definition.TriggerType.ToString());
            DrawReadOnlyText("ParallelStorageMode", _buffDebugPreflight.definition.ParallelStorageMode.ToString());
            DrawReadOnlyText("Unlimited", _buffDebugPreflight.definition.Unlimited.ToString());
            DrawReadOnlyInt("MaxStack", _buffDebugPreflight.definition.MaxStack);
            DrawReadOnlyInt("DurationFrames", _buffDebugPreflight.definition.DurationFrames);
            DrawReadOnlyInt("TickIntervalFrames", _buffDebugPreflight.definition.TickIntervalFrames);
            DrawReadOnlyText("StackUpPolicy", _buffDebugPreflight.definition.ParallelStackUpPolicy.ToString());
            DrawReadOnlyText("StackDownPolicy", _buffDebugPreflight.definition.ParallelStackDownPolicy.ToString());
            DrawReadOnlyInt("EffectId", _buffDebugPreflight.definition.EffectId);
        }

        EditorGUILayout.Space(4f);
        DrawReadOnlyText("BuffType == parallel", _buffDebugPreflight.buffTypeParallel.ToString());
        DrawReadOnlyText("Storage == CompressedExpiryFrameList", _buffDebugPreflight.storageCompressed.ToString());
        DrawReadOnlyText("TriggerType == Tick", _buffDebugPreflight.triggerTick.ToString());
        DrawReadOnlyText("Unlimited == false", _buffDebugPreflight.unlimitedFalse.ToString());
        DrawReadOnlyText("MaxStack <= Capacity", _buffDebugPreflight.maxStackWithinCapacity.ToString());
        DrawReadOnlyText("Eligibility", _buffDebugPreflight.eligibility ? "PASS" : "FAIL");

        EditorGUILayout.Space(4f);
        DrawReadOnlyText("Compressed gate", _buffDebugPreflight.compressedGate.ToString());
        DrawReadOnlyText("Whitelist hit", _buffDebugPreflight.whitelistHit.ToString());
        DrawReadOnlyText("Whitelist configIds", _buffDebugPreflight.whitelistConfigIds);
        DrawReadOnlyText("ShouldUseCompressedParallel 预期", _buffDebugPreflight.shouldUseCompressedExpected ? "PASS" : "FAIL");
        DrawReadOnlyText("BuffSystemCore 创建路径推断", _buffDebugPreflight.coreModeHint);

        EditorGUILayout.Space(4f);
        DrawReadOnlyText("Command.IsValid", _buffDebugPreflight.commandIsValid.ToString());
        DrawReadOnlyText("Target alive", _buffDebugPreflight.targetAlive.ToString());
        DrawReadOnlyText("Source alive", _buffDebugPreflight.sourceAlive.ToString());
        DrawReadOnlyText("Target", FormatEntity(_buffDebugPreflight.target));
        DrawReadOnlyText("Source", FormatEntity(_buffDebugPreflight.source));

        EditorGUILayout.Space(4f);
        DrawReadOnlyText("BuffEffectRegistry 存在", _buffDebugPreflight.effectRegistryExists.ToString());
        DrawReadOnlyInt("EffectId", _buffDebugPreflight.effectId);
        DrawReadOnlyText("Effect registered", _buffDebugPreflight.effectRegistered.ToString());

        EditorGUILayout.HelpBox(BuildBuffDebugPreflightDiagnosis(_buffDebugPreflight), GetBuffDebugPreflightMessageType(_buffDebugPreflight));
        EditorGUILayout.EndVertical();
    }

    private void DrawBuffDebugCommandQueueDiagnostics(BuffDebugBinding binding)
    {
        DrawSectionTitle("Command Queue 诊断");
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.HelpBox("当前诊断只通过 Editor 反射读取 BuffSystemCore 私有字段，不修改 runtime。字段名变更可能导致识别失败，但不会影响 BuffSystem 运行。", MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("记录 Add 前队列状态"))
            CaptureAndLogBuffCommandQueueStage(binding, "BeforeAdd", "记录 Add 前队列状态");

        if (GUILayout.Button("添加 Buff 并记录队列状态"))
            AddBuffAndCaptureCommandQueue(binding);

        if (GUILayout.Button("Tick 一帧并记录队列状态"))
            TickAndCaptureCommandQueue(binding, 1);

        if (GUILayout.Button("Tick 两帧并记录队列状态"))
            TickAndCaptureCommandQueue(binding, 2);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("执行 Add 队列链路诊断"))
            RunBuffAddQueueChainDiagnostics(binding);

        if (GUILayout.Button("复制队列诊断日志"))
            CopyBuffCommandQueueDiagnosticsToClipboard();
        EditorGUILayout.EndHorizontal();

        BuffCommandQueueStageSnapshot latest = _buffCommandQueueStages.Count > 0 ? _buffCommandQueueStages[_buffCommandQueueStages.Count - 1] : default;
        if (_buffCommandQueueStages.Count > 0)
        {
            DrawReadOnlyText("Latest Stage", latest.stage);
            DrawReadOnlyInt("Frame", latest.frame);
            DrawReadOnlyInt("AddQueue count", latest.addQueueCount);
            DrawReadOnlyInt("RemoveQueue count", latest.removeQueueCount);
            DrawReadOnlyInt("PendingAdd count", latest.pendingAddCount);
            DrawReadOnlyInt("PendingRemove count", latest.pendingRemoveCount);
            DrawReadOnlyInt("CommandBuffer count", latest.commandBufferCount);
            DrawReadOnlyInt("当前 ConfigId CompressedRuntime count", latest.configCompressedRuntimeCount);
            DrawReadOnlyInt("当前 ConfigId EntityPerStack count", latest.configEntityPerStackRuntimeCount);
            DrawReadOnlyText("TryGetBuff found", latest.tryGetBuffFound.ToString());
            DrawReadOnlyInt("GetBuffs count", latest.getBuffsCount);
            EditorGUILayout.HelpBox(latest.diagnosis, latest.diagnosis.Contains("成功") ? MessageType.Info : MessageType.Warning);
        }
        else
        {
            EditorGUILayout.LabelField("尚未记录队列快照。");
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Command-like 字段", EditorStyles.boldLabel);
        if (_buffCommandQueueFields.Count == 0)
        {
            EditorGUILayout.LabelField("尚未识别字段。");
        }
        else
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField("FieldName", EditorStyles.boldLabel, GUILayout.Width(220f));
            EditorGUILayout.LabelField("FieldType", EditorStyles.boldLabel, GUILayout.Width(260f));
            EditorGUILayout.LabelField("Count", EditorStyles.boldLabel, GUILayout.Width(70f));
            EditorGUILayout.LabelField("Readable", EditorStyles.boldLabel, GUILayout.Width(70f));
            EditorGUILayout.LabelField("ValueSummary", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < _buffCommandQueueFields.Count; i++)
            {
                BuffCommandQueueFieldInfo field = _buffCommandQueueFields[i];
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField(field.fieldName, GUILayout.Width(220f));
                EditorGUILayout.LabelField(field.fieldType, GUILayout.Width(260f));
                EditorGUILayout.LabelField(field.count.ToString(), GUILayout.Width(70f));
                EditorGUILayout.LabelField(field.readable.ToString(), GUILayout.Width(70f));
                EditorGUILayout.LabelField(field.valueSummary);
                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawBuffDebugEntityControls(BuffDebugBinding binding)
    {
        World world = binding.world;
        DrawSectionTitle("调试实体");
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        _buffTargetIdText = EditorGUILayout.TextField("Target ID", _buffTargetIdText);
        _buffTargetVersionText = EditorGUILayout.TextField("Target Version", _buffTargetVersionText);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        _buffSourceIdText = EditorGUILayout.TextField("Source ID", _buffSourceIdText);
        _buffSourceVersionText = EditorGUILayout.TextField("Source Version", _buffSourceVersionText);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("使用 / 创建调试 Entity"))
        {
            EnsureBuffDebugEntities(world);
            SyncBuffEntityFields();
            RefreshBuffDebugSnapshot(world, binding.buffSystem, "使用 / 创建调试 Entity");
        }

        if (GUILayout.Button("使用左侧选中 Entity 作为 Target"))
        {
            if (_selectedEntity.IsValid && world != null && world.IsAlive(_selectedEntity))
            {
                _buffDebugTarget = _selectedEntity;
                _buffDebugSource = _selectedEntity;
                SyncBuffEntityFields();
                RefreshBuffDebugSnapshot(world, binding.buffSystem, "使用选中 Entity");
            }
            else
            {
                AppendBuffDebugLog("使用选中 Entity", false, "左侧 Entities 页存在存活选中 Entity", FormatEntity(_selectedEntity));
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawBuffDebugBindingDiagnostics(BuffDebugBinding binding)
    {
        DrawSectionTitle("绑定诊断");
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        DrawReadOnlyText("当前调试 Source", binding.selectedSourceName);
        DrawReadOnlyText("绑定根对象", binding.rootName);
        DrawReadOnlyText("当前 World 是否有效", (binding.world != null).ToString());
        DrawReadOnlyText("World Ref", FormatReference(binding.world));
        DrawReadOnlyText("当前 Runner 是否有效", (binding.runner != null).ToString());
        DrawReadOnlyText("Runner Ref", FormatReference(binding.runner));
        DrawReadOnlyInt("Runner 当前帧", binding.runner != null ? binding.runner.CurrentFrameNumber : -1);
        DrawReadOnlyText("当前 BuffSystemCore 是否有效", (binding.buffSystem != null).ToString());
        DrawReadOnlyText("BuffSystem Ref", FormatReference(binding.buffSystem));
        DrawReadOnlyText("World 来源对象", binding.worldOwnerName);
        DrawReadOnlyText("Runner 来源对象", binding.runnerOwnerName);
        DrawReadOnlyText("BuffSystem 来源对象", binding.buffSystemOwnerName);
        DrawReadOnlyText("Target 属于当前 World", IsEntityAliveInWorld(binding.world, _buffDebugTarget).ToString());
        DrawReadOnlyText("Source 属于当前 World", IsEntityAliveInWorld(binding.world, _buffDebugSource).ToString());
        DrawReadOnlyText("Add / RuntimeCount 使用同一 World", binding.addWorldEqualsRuntimeWorld.ToString());
        DrawReadOnlyText("Query / Add 使用同一 BuffSystem", binding.queryBuffSystemEqualsAddBuffSystem.ToString());
        DrawReadOnlyText("Tick Runner / RuntimeCount 使用同一 World", binding.tickRunnerWorldEqualsRuntimeWorld.ToString());
        DrawReadOnlyText("绑定一致性", binding.IsUsable ? "PASS" : "FAIL");
        EditorGUILayout.HelpBox(binding.diagnosis, binding.IsUsable ? MessageType.Info : MessageType.Warning);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("检查绑定一致性"))
        {
            _buffDebugBinding = ResolveBuffDebugBinding(GetCurrentSource(), GetCurrentWorld());
            _buffDebugLogRunner = _buffDebugBinding.runner;
            AppendBuffDebugInfoLog("检查绑定一致性", _buffDebugBinding.IsUsable ? "绑定一致性 PASS。" : $"绑定一致性 FAIL：{_buffDebugBinding.diagnosis}");
        }

        if (GUILayout.Button("复制绑定诊断日志"))
            CopyBuffDebugBindingLogToClipboard(_buffDebugBinding);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawBuffDebugActionButtons(BuffDebugBinding binding)
    {
        DrawSectionTitle("Buff 操作");
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("刷新查询结果"))
            RefreshBuffDebugSnapshot(binding.world, binding.buffSystem, "刷新查询结果");

        if (GUILayout.Button("清空日志"))
        {
            _buffDebugLogs.Clear();
            _buffDebugCopyText = string.Empty;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("添加 Buff"))
            QueueBuffDebugAdd(binding.world, binding.buffSystem, ReadBuffStackOrDefault());

        if (GUILayout.Button("添加 3 层 Buff"))
            QueueBuffDebugAdd(binding.world, binding.buffSystem, 3);

        if (GUILayout.Button("移除 Buff"))
            QueueBuffDebugRemove(binding.world, binding.buffSystem, ReadBuffStackOrDefault());
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("添加 Buff 并 Tick 一帧"))
            QueueBuffDebugAddAndTick(binding, ReadBuffStackOrDefault());

        if (GUILayout.Button("添加 3 层 Buff 并 Tick 一帧"))
            QueueBuffDebugAddAndTick(binding, 3);

        if (GUILayout.Button("移除 Buff 并 Tick 一帧"))
            QueueBuffDebugRemoveAndTick(binding, ReadBuffStackOrDefault());
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("添加 Buff 并 Tick 两帧"))
            QueueBuffDebugAddAndTick(binding, ReadBuffStackOrDefault(), 2);

        if (GUILayout.Button("添加 3 层 Buff 并 Tick 两帧"))
            QueueBuffDebugAddAndTick(binding, 3, 2);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Tick 一帧"))
            TickBuffDebugFrames(binding, 1);

        if (GUILayout.Button("Tick 指定帧数"))
            TickBuffDebugFrames(binding, ReadBuffTickFramesOrDefault());
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawBuffDebugResult()
    {
        DrawSectionTitle("查询结果");
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        if (!_hasBuffDebugSnapshot)
        {
            EditorGUILayout.LabelField("尚未刷新查询结果。");
            EditorGUILayout.EndVertical();
            return;
        }

        DrawReadOnlyText("是否找到", _buffDebugSnapshot.found.ToString());
        DrawReadOnlyText("Target", FormatEntity(_buffDebugSnapshot.target));
        DrawReadOnlyText("Source", FormatEntity(_buffDebugSnapshot.source));
        DrawReadOnlyText("ViewData 状态", GetBuffDebugViewDataState(_buffDebugSnapshot));

        if (_buffDebugSnapshot.found)
        {
            DrawReadOnlyInt("ConfigId", _buffDebugSnapshot.view.ConfigId);
            DrawReadOnlyInt("Stack", _buffDebugSnapshot.view.Stack);
            DrawReadOnlyInt("RemainingFrames", _buffDebugSnapshot.view.RemainingFrames);
            DrawReadOnlyInt("RuntimeHandle", _buffDebugSnapshot.view.RuntimeHandle);
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawBuffDebugRuntimeStats()
    {
        DrawSectionTitle("Runtime 类型统计");
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        DrawReadOnlyInt("CompressedRuntime total", _buffDebugSnapshot.compressedRuntimeCount);
        DrawReadOnlyInt("当前 ConfigId CompressedRuntime count", _buffDebugSnapshot.configCompressedRuntimeCount);
        DrawReadOnlyInt("EntityPerStack total", _buffDebugSnapshot.entityPerStackRuntimeCount);
        DrawReadOnlyInt("当前 ConfigId EntityPerStack count", _buffDebugSnapshot.configEntityPerStackRuntimeCount);
        string compressedPathStatus = GetBuffDebugCompressedPathStatus(_buffDebugSnapshot);
        DrawReadOnlyText("991001 压缩路径状态", compressedPathStatus);
        EditorGUILayout.HelpBox(
            BuildBuffDebugDiagnosis(_buffDebugSnapshot),
            compressedPathStatus == "PASS" ? MessageType.Info : MessageType.Warning);
        if (_hasBuffDebugPreflight && compressedPathStatus == "ADD_TICKED_NO_RUNTIME")
            EditorGUILayout.HelpBox(BuildBuffDebugPreflightDiagnosis(_buffDebugPreflight), GetBuffDebugPreflightMessageType(_buffDebugPreflight));
        EditorGUILayout.EndVertical();
    }

    private void DrawBuffDebugViewList()
    {
        DrawSectionTitle("GetBuffs(target) 列表");
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        if (_buffDebugViews.Count == 0)
        {
            EditorGUILayout.LabelField("空");
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField("ConfigId", EditorStyles.boldLabel, GUILayout.Width(80f));
        EditorGUILayout.LabelField("Stack", EditorStyles.boldLabel, GUILayout.Width(70f));
        EditorGUILayout.LabelField("剩余帧数", EditorStyles.boldLabel, GUILayout.Width(130f));
        EditorGUILayout.LabelField("Runtime 句柄", EditorStyles.boldLabel, GUILayout.Width(120f));
        EditorGUILayout.LabelField("Target / Source", EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();

        for (int i = 0; i < _buffDebugViews.Count; i++)
        {
            BuffViewData view = _buffDebugViews[i];
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField(view.ConfigId.ToString(), GUILayout.Width(80f));
            EditorGUILayout.LabelField(view.Stack.ToString(), GUILayout.Width(70f));
            EditorGUILayout.LabelField(view.RemainingFrames.ToString(), GUILayout.Width(130f));
            EditorGUILayout.LabelField(view.RuntimeHandle.ToString(), GUILayout.Width(120f));
            EditorGUILayout.LabelField($"{FormatEntity(view.Target)} / {FormatEntity(view.Source)}");
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawBuffDebugLogs()
    {
        DrawSectionTitle("最近操作日志");
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("生成当前快照日志"))
            GenerateBuffDebugCopyText();

        if (GUILayout.Button("复制日志到剪贴板"))
            CopyBuffDebugLogToClipboard();

        if (GUILayout.Button("清空日志"))
        {
            _buffDebugLogs.Clear();
            _buffDebugCopyText = string.Empty;
        }
        EditorGUILayout.EndHorizontal();

        if (_buffDebugLogs.Count == 0)
            EditorGUILayout.LabelField("空");

        for (int i = 0; i < _buffDebugLogs.Count; i++)
            EditorGUILayout.LabelField(_buffDebugLogs[i]);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("可复制日志", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("下方为纯文本快照，可选中复制，也可以点击“复制日志到剪贴板”。", MessageType.Info);
        _buffDebugCopyScroll = EditorGUILayout.BeginScrollView(_buffDebugCopyScroll, GUILayout.MinHeight(180f), GUILayout.MaxHeight(260f));
        _buffDebugCopyText = EditorGUILayout.TextArea(_buffDebugCopyText, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    /// <summary>绘制命令调试摘要卡片。</summary>
    private void DrawCommandSummary(SimulationFrameCommandBuffer commandBuffer, SimulationFrameCommandApplier commandApplier, SimulateRunner runner)
    {
        int currentFrame = runner != null ? runner.CurrentFrameNumber : 0;
        int nextFrame = runner != null ? runner.NextFrameNumber : 1;
        int pendingFuture = commandBuffer != null ? commandBuffer.CountCommandsFromFrame(nextFrame) : 0;

        EditorGUILayout.BeginHorizontal();
        DrawOverviewCard("帧命令缓冲 Frame Command Buffer", new[]
        {
            new DebugLine("缓存帧 Buffered Frames", commandBuffer != null ? commandBuffer.FrameCount.ToString() : "0"),
            new DebugLine("缓存命令 Buffered Commands", commandBuffer != null ? commandBuffer.CommandCount.ToString() : "0"),
            new DebugLine("当前帧 Current Frame", currentFrame.ToString()),
            new DebugLine("未来命令 Future Commands", pendingFuture.ToString())
        });

        DrawOverviewCard("命令历史 Command History", new[]
        {
            new DebugLine("历史帧 History Frames", commandBuffer != null ? commandBuffer.CommandHistoryFrameCount.ToString() : "0"),
            new DebugLine("历史命令 History Commands", commandBuffer != null ? commandBuffer.CommandHistoryCommandCount.ToString() : "0")
        });

        DrawOverviewCard("调试执行 Debug Execution", new[]
        {
            new DebugLine("已应用时机 Applied Timings", commandApplier != null ? commandApplier.AppliedFrameCount.ToString() : "0"),
            new DebugLine("调试帧 Debug Frames", commandApplier != null ? commandApplier.DebugHistoryFrameCount.ToString() : "0"),
            new DebugLine("调试记录 Debug Records", commandApplier != null ? commandApplier.DebugRecordCount.ToString() : "0")
        });
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>绘制命令搜索与调试历史控制栏。</summary>
    private void DrawCommandToolbar(SimulationFrameCommandApplier commandApplier)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        _commandSearch = EditorGUILayout.TextField("搜索 Search", _commandSearch);
        _showOnlyFailedCommands = GUILayout.Toggle(_showOnlyFailedCommands, "仅失败", "Button", GUILayout.Width(90f));

        EditorGUI.BeginDisabledGroup(commandApplier == null);
        if (GUILayout.Button("清空调试历史", GUILayout.Width(150f)))
        {
            commandApplier.ClearCommandDebugHistory();
            RefreshData();
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>绘制实际执行过的命令历史。</summary>
    private void DrawCommandExecutionHistory(SimulationFrameCommandApplier commandApplier)
    {
        DrawSectionTitle($"DebugCommand 执行历史 ({_commandDebugFrames.Count} frames)");
        if (commandApplier == null)
        {
            EditorGUILayout.HelpBox("当前调试源没有绑定 SimulationFrameCommandApplier。", MessageType.Info);
            return;
        }

        if (_commandDebugFrames.Count == 0)
        {
            EditorGUILayout.HelpBox("暂无命令执行记录。SimulationFrameCommandApplier 应用命令后会显示记录。", MessageType.Info);
            return;
        }

        _commandExecutionScroll = EditorGUILayout.BeginScrollView(_commandExecutionScroll, GUILayout.MinHeight(220f));
        DrawCommandExecutionHeader();
        for (int i = 0; i < _commandDebugFrames.Count; i++)
        {
            CommandDebugFrame frame = _commandDebugFrames[i];
            for (int j = 0; j < frame.records.Length; j++)
            {
                CommandDebugRecord record = frame.records[j];
                if (!CommandRecordMatches(record))
                    continue;

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField(record.frameNumber.ToString(), GUILayout.Width(70f));
                EditorGUILayout.LabelField(record.timing.ToString(), GUILayout.Width(90f));
                EditorGUILayout.LabelField(record.status.ToString(), GUILayout.Width(80f));
                EditorGUILayout.LabelField(record.isReplay ? "Replay" : "Normal", GUILayout.Width(70f));
                EditorGUILayout.LabelField(record.commandTypeName, GUILayout.MinWidth(170f));
                EditorGUILayout.LabelField(record.targetEntity.IsValid ? record.targetEntity.ToString() : "-", GUILayout.Width(180f));
                EditorGUILayout.LabelField(record.summary, GUILayout.MinWidth(260f));
                EditorGUILayout.LabelField(record.message, GUILayout.MinWidth(180f));
                EditorGUILayout.EndHorizontal();
            }
        }
        EditorGUILayout.EndScrollView();
    }

    /// <summary>绘制加入 FrameCommandBuffer 的帧命令历史。</summary>
    private void DrawFrameCommandHistory(SimulationFrameCommandBuffer commandBuffer)
    {
        DrawSectionTitle($"FrameCommand 历史 ({_frameCommandHistoryFrames.Count} frames)");
        if (commandBuffer == null)
        {
            EditorGUILayout.HelpBox("当前调试源没有绑定 SimulationFrameCommandBuffer。", MessageType.Info);
            return;
        }

        if (_frameCommandHistoryFrames.Count == 0)
        {
            EditorGUILayout.HelpBox("暂无帧命令历史。向 SimulationFrameCommandBuffer 添加命令后会显示记录。", MessageType.Info);
            return;
        }

        _frameCommandHistoryScroll = EditorGUILayout.BeginScrollView(_frameCommandHistoryScroll, GUILayout.MinHeight(220f));
        DrawFrameCommandHistoryHeader();
        for (int i = 0; i < _frameCommandHistoryFrames.Count; i++)
        {
            FrameCommandHistoryFrameDebugInfo frame = _frameCommandHistoryFrames[i];
            for (int j = 0; j < frame.commands.Length; j++)
            {
                FrameCommandHistoryRecord record = frame.commands[j];
                if (!FrameCommandHistoryRecordMatches(record))
                    continue;

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField(record.frameNumber.ToString(), GUILayout.Width(70f));
                EditorGUILayout.LabelField(record.timing.ToString(), GUILayout.Width(90f));
                EditorGUILayout.LabelField(record.commandTypeName, GUILayout.MinWidth(170f));
                EditorGUILayout.LabelField(record.targetEntity.IsValid ? record.targetEntity.ToString() : "-", GUILayout.Width(180f));
                EditorGUILayout.LabelField(record.summary, GUILayout.MinWidth(360f));
                EditorGUILayout.EndHorizontal();
            }
        }
        EditorGUILayout.EndScrollView();
    }

    /// <summary>绘制 Entity 持有组件的可展开调试值。</summary>
    private void DrawEntityComponentValueList(World world, Entity entity, List<Type> types)
    {
        if (types == null || types.Count == 0)
        {
            EditorGUILayout.LabelField("无");
            return;
        }

        for (int i = 0; i < types.Count; i++)
        {
            Type componentType = types[i];
            string foldoutKey = GetComponentFoldoutKey(entity, componentType);
            bool expanded = DrawFoldoutBox(foldoutKey, ShortTypeName(componentType));
            if (expanded)
            {
                EditorGUI.indentLevel++;
                DrawComponentDebugValue(world, entity, componentType, foldoutKey);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
        }
    }

    /// <summary>绘制单个组件实例的字段值。</summary>
    private void DrawComponentDebugValue(World world, Entity entity, Type componentType, string rootKey)
    {
        if (world == null || componentType == null)
        {
            EditorGUILayout.HelpBox("组件值不可用。", MessageType.Warning);
            return;
        }

        if (!world.TryGetComponentDebugValue(entity, componentType, out object component))
        {
            EditorGUILayout.HelpBox("组件值读取失败，可能已在上次刷新后被移除。", MessageType.Warning);
            return;
        }

        DrawObjectDebugFields(component, componentType, rootKey, 0);
    }

    /// <summary>使用反射绘制组件实例的字段和只读属性。</summary>
    private void DrawObjectDebugFields(object value, Type valueType, string rootKey, int depth)
    {
        if (valueType == null)
        {
            DrawReadOnlyText("Value", FormatDebugValue(value));
            return;
        }

        FieldInfo[] fields = valueType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        PropertyInfo[] properties = valueType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        int drawnCount = 0;

        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo field = fields[i];
            if (ShouldSkipDebugField(field))
                continue;

            object fieldValue = SafeGetFieldValue(field, value);
            DrawDebugMember(field.Name, fieldValue, field.FieldType, $"{rootKey}.{field.Name}", depth);
            drawnCount++;
        }

        for (int i = 0; i < properties.Length; i++)
        {
            PropertyInfo property = properties[i];
            if (ShouldSkipDebugProperty(property))
                continue;

            object propertyValue = SafeGetPropertyValue(property, value, out string error);
            if (error != null)
                DrawReadOnlyText(property.Name, error);
            else
                DrawDebugMember(property.Name, propertyValue, property.PropertyType, $"{rootKey}.{property.Name}", depth);
            drawnCount++;
        }

        if (drawnCount == 0)
            DrawReadOnlyText("Value", FormatDebugValue(value));
    }

    /// <summary>绘制组件字段或属性，复杂对象在合理深度内允许继续展开。</summary>
    private void DrawDebugMember(string label, object value, Type declaredType, string key, int depth)
    {
        Type runtimeType = value != null ? value.GetType() : declaredType;
        if (value == null || IsSimpleDebugType(runtimeType) || depth >= MaxObjectDepth)
        {
            DrawReadOnlyText(label, FormatDebugValue(value));
            return;
        }

        UnityEngine.Object unityObject = value as UnityEngine.Object;
        if (!ReferenceEquals(unityObject, null))
        {
            if (unityObject == null)
                DrawReadOnlyText(label, "Missing Unity Object");
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField(label, unityObject, runtimeType, true);
                EditorGUI.EndDisabledGroup();
            }
            return;
        }

        IEnumerable enumerable = value as IEnumerable;
        if (enumerable != null && !(value is string))
        {
            DrawEnumerableDebugValue(label, enumerable, key, depth);
            return;
        }

        bool expanded = GetFoldout(key);
        bool nextExpanded = EditorGUILayout.Foldout(expanded, $"{label} ({ShortTypeName(runtimeType)})", true);
        SetFoldout(key, nextExpanded);
        if (!nextExpanded)
            return;

        EditorGUI.indentLevel++;
        DrawObjectDebugFields(value, runtimeType, key, depth + 1);
        EditorGUI.indentLevel--;
    }

    /// <summary>绘制集合类型字段的简要内容。</summary>
    private void DrawEnumerableDebugValue(string label, IEnumerable enumerable, string key, int depth)
    {
        bool expanded = GetFoldout(key);
        bool nextExpanded = EditorGUILayout.Foldout(expanded, label, true);
        SetFoldout(key, nextExpanded);
        if (!nextExpanded)
            return;

        EditorGUI.indentLevel++;
        int index = 0;
        foreach (object item in enumerable)
        {
            if (index >= CollectionPreviewCount)
            {
                EditorGUILayout.LabelField("...", $"Only first {CollectionPreviewCount} items are shown.");
                break;
            }

            DrawDebugMember($"[{index}]", item, item != null ? item.GetType() : typeof(object), $"{key}[{index}]", depth + 1);
            index++;
        }

        if (index == 0)
            EditorGUILayout.LabelField("空");
        EditorGUI.indentLevel--;
    }

    /// <summary>根据当前搜索条件生成可见 Entity 列表。</summary>
    private void BuildVisibleEntities(World world)
    {
        _visibleEntities.Clear();
        for (int i = 0; i < _entities.Count; i++)
        {
            Entity entity = _entities[i];
            if (EntityMatchesSearch(world, entity, _entitySearch))
                _visibleEntities.Add(entity);
        }
    }

    /// <summary>实体搜索匹配，支持 Entity ID / Version / 组件类型名称。</summary>
    private bool EntityMatchesSearch(World world, Entity entity, string search)
    {
        if (string.IsNullOrEmpty(search))
            return true;

        if (entity.ID.ToString().Contains(search) || entity.Version.ToString().Contains(search) || entity.ToString().IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (world == null)
            return false;

        world.FillEntityComponentTypes(entity, _searchComponentTypes);
        for (int i = 0; i < _searchComponentTypes.Count; i++)
        {
            if (TextMatches(ShortTypeName(_searchComponentTypes[i]), search) || TextMatches(_searchComponentTypes[i].FullName, search))
                return true;
        }

        return false;
    }

    /// <summary>绘制通用折叠盒，并返回展开状态；调用方负责 EndVertical。</summary>
    private bool DrawFoldoutBox(string key, string title)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        bool expanded = GetFoldout(key);
        bool nextExpanded = EditorGUILayout.Foldout(expanded, title, true);
        SetFoldout(key, nextExpanded);
        return nextExpanded;
    }

    /// <summary>绘制命令执行表头。</summary>
    private static void DrawCommandExecutionHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField("帧 Frame", EditorStyles.boldLabel, GUILayout.Width(70f));
        EditorGUILayout.LabelField("时机 Timing", EditorStyles.boldLabel, GUILayout.Width(90f));
        EditorGUILayout.LabelField("状态 Status", EditorStyles.boldLabel, GUILayout.Width(80f));
        EditorGUILayout.LabelField("模式 Mode", EditorStyles.boldLabel, GUILayout.Width(70f));
        EditorGUILayout.LabelField("命令 Command", EditorStyles.boldLabel, GUILayout.MinWidth(170f));
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel, GUILayout.Width(180f));
        EditorGUILayout.LabelField("摘要 Summary", EditorStyles.boldLabel, GUILayout.MinWidth(260f));
        EditorGUILayout.LabelField("消息 Message", EditorStyles.boldLabel, GUILayout.MinWidth(180f));
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>绘制帧命令表头。</summary>
    private static void DrawFrameCommandHistoryHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField("帧 Frame", EditorStyles.boldLabel, GUILayout.Width(70f));
        EditorGUILayout.LabelField("时机 Timing", EditorStyles.boldLabel, GUILayout.Width(90f));
        EditorGUILayout.LabelField("命令 Command", EditorStyles.boldLabel, GUILayout.MinWidth(170f));
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel, GUILayout.Width(180f));
        EditorGUILayout.LabelField("摘要 Summary", EditorStyles.boldLabel, GUILayout.MinWidth(360f));
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>绘制系统性能表头。</summary>
    private static void DrawSystemHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField("名称 Name", EditorStyles.boldLabel, GUILayout.MinWidth(180f));
        EditorGUILayout.LabelField("顺序 Sequence", EditorStyles.boldLabel, GUILayout.Width(110f));
        EditorGUILayout.LabelField("启用 Enabled", EditorStyles.boldLabel, GUILayout.Width(90f));
        EditorGUILayout.LabelField("最近 Last", EditorStyles.boldLabel, GUILayout.Width(90f));
        EditorGUILayout.LabelField("平均 Avg", EditorStyles.boldLabel, GUILayout.Width(90f));
        EditorGUILayout.LabelField("最大 Max", EditorStyles.boldLabel, GUILayout.Width(90f));
        EditorGUILayout.LabelField("次数 Ticks", EditorStyles.boldLabel, GUILayout.Width(90f));
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>绘制 ComponentStore 表头。</summary>
    private static void DrawComponentStoreHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField("类型 Type", EditorStyles.boldLabel, GUILayout.MinWidth(180f));
        EditorGUILayout.LabelField("TypeID", EditorStyles.boldLabel, GUILayout.Width(70f));
        EditorGUILayout.LabelField("数量 Count", EditorStyles.boldLabel, GUILayout.Width(80f));
        EditorGUILayout.LabelField("容量 Capacity", EditorStyles.boldLabel, GUILayout.Width(100f));
        EditorGUILayout.LabelField("Sparse", EditorStyles.boldLabel, GUILayout.Width(110f));
        EditorGUILayout.LabelField("负载 Load", EditorStyles.boldLabel, GUILayout.Width(90f));
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>绘制 WorldEvent 表头。</summary>
    private static void DrawWorldEventHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField("类型 Type", EditorStyles.boldLabel, GUILayout.MinWidth(180f));
        EditorGUILayout.LabelField("数量 Count", EditorStyles.boldLabel, GUILayout.Width(80f));
        EditorGUILayout.LabelField("最早帧 Oldest", EditorStyles.boldLabel, GUILayout.Width(120f));
        EditorGUILayout.LabelField("最新帧 Newest", EditorStyles.boldLabel, GUILayout.Width(120f));
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>绘制总览卡片。</summary>
    private static void DrawOverviewCard(string title, DebugLine[] lines)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MinWidth(220f));
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        for (int i = 0; i < lines.Length; i++)
            DrawReadOnlyText(lines[i].label, lines[i].value);
        EditorGUILayout.EndVertical();
    }

    /// <summary>绘制类型列表。</summary>
    private static void DrawTypeList(List<Type> types)
    {
        if (types == null || types.Count == 0)
        {
            EditorGUILayout.LabelField("无");
            return;
        }

        for (int i = 0; i < types.Count; i++)
            EditorGUILayout.LabelField($"- {ShortTypeName(types[i])}");
    }

    /// <summary>绘制段落标题。</summary>
    private static void DrawSectionTitle(string title)
    {
        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    /// <summary>绘制可复制的长文本字段。</summary>
    private static void DrawSelectableText(string label, string value)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(label);
        EditorGUILayout.SelectableLabel(value ?? string.Empty, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>安全读取字段值。</summary>
    private static object SafeGetFieldValue(FieldInfo field, object target)
    {
        try
        {
            return field.GetValue(target);
        }
        catch (Exception exception)
        {
            return $"<read failed: {exception.GetType().Name}>";
        }
    }

    /// <summary>安全读取属性值。</summary>
    private static object SafeGetPropertyValue(PropertyInfo property, object target, out string error)
    {
        try
        {
            error = null;
            return property.GetValue(target, null);
        }
        catch (Exception exception)
        {
            error = $"<read failed: {exception.GetType().Name}>";
            return null;
        }
    }

    /// <summary>判断字段是否应从 Debugger 展示中跳过。</summary>
    private static bool ShouldSkipDebugField(FieldInfo field)
    {
        if (field == null || field.IsStatic)
            return true;

        if (field.IsDefined(typeof(NonSerializedAttribute), true))
            return true;

        return field.Name.IndexOf("k__BackingField", StringComparison.Ordinal) >= 0;
    }

    /// <summary>判断属性是否应从 Debugger 展示中跳过。</summary>
    private static bool ShouldSkipDebugProperty(PropertyInfo property)
    {
        if (property == null || !property.CanRead || property.GetIndexParameters().Length > 0)
            return true;

        MethodInfo getter = property.GetGetMethod(false);
        return getter == null || getter.IsStatic;
    }

    /// <summary>判断类型是否适合以单行文本展示。</summary>
    private static bool IsSimpleDebugType(Type type)
    {
        if (type == null)
            return true;

        return type.IsEnum || type.IsPrimitive || type == typeof(string) || type == typeof(decimal) || type == typeof(Vector2) || type == typeof(Vector2Int) || type == typeof(Vector3) || type == typeof(Vector3Int) || type == typeof(Vector4) || type == typeof(Quaternion) || type == typeof(Color) || type == typeof(Rect);
    }

    /// <summary>格式化字段值，避免复杂对象在 EditorWindow 中显示为难以阅读的类型名。</summary>
    private static string FormatDebugValue(object value)
    {
        if (value == null)
            return "null";

        UnityEngine.Object unityObject = value as UnityEngine.Object;
        if (!ReferenceEquals(unityObject, null))
            return unityObject != null ? $"{unityObject.name} ({unityObject.GetType().Name})" : "Missing Unity Object";

        return value.ToString();
    }

    /// <summary>读取折叠状态，默认折叠以避免详情区过于拥挤。</summary>
    private bool GetFoldout(string key)
    {
        return !string.IsNullOrEmpty(key) && _foldouts.TryGetValue(key, out bool expanded) && expanded;
    }

    /// <summary>写入折叠状态。</summary>
    private void SetFoldout(string key, bool expanded)
    {
        if (string.IsNullOrEmpty(key))
            return;

        _foldouts[key] = expanded;
    }

    /// <summary>生成 Entity Component 折叠状态缓存键。</summary>
    private static string GetComponentFoldoutKey(Entity entity, Type componentType)
    {
        string typeName = componentType != null ? componentType.FullName : "UnknownComponent";
        return $"entity:{entity.ID}:{entity.Version}:{typeName}";
    }

    /// <summary>解析当前 World 可用的命令调试源；当前源没有暴露命令数据时，会尝试寻找同一个 World 关联的命令源。</summary>
    private IECSFrameCommandDebugSource ResolveCommandDebugSource(IECSRuntimeDebugSource source)
    {
        if (source is IECSFrameCommandDebugSource directSource)
            return directSource;

        World world = source != null ? source.DebugWorld : null;
        for (int i = 0; i < _sources.Count; i++)
        {
            IECSRuntimeDebugSource candidate = _sources[i];
            if (!(candidate is IECSFrameCommandDebugSource commandSource))
                continue;

            if (world == null || ReferenceEquals(candidate.DebugWorld, world))
                return commandSource;
        }

        return null;
    }

    private BuffDebugBinding ResolveBuffDebugBinding(IECSRuntimeDebugSource selectedSource, World selectedWorld)
    {
        BuffDebugBinding binding = new BuffDebugBinding
        {
            selectedSourceName = selectedSource != null ? CreateSourceLabel(selectedSource) : "None",
            world = selectedWorld,
            runner = selectedSource != null ? selectedSource.DebugRunner : null,
            rootName = "None",
            worldOwnerName = selectedSource != null ? selectedSource.DebugSourceName : "None",
            runnerOwnerName = selectedSource != null ? selectedSource.DebugSourceName : "None",
            buffSystemOwnerName = "None",
            addWorldEqualsRuntimeWorld = true,
            queryBuffSystemEqualsAddBuffSystem = true
        };

        if (binding.runner != null && binding.world == null)
            binding.world = binding.runner.World;

        BuffDebugBinding best = binding;
        bool foundFullRoot = false;

#if UNITY_2023_1_OR_NEWER
        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true);
#endif
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
                continue;

            bool hasWorld = TryGetFieldValue(behaviour, typeof(World), out object worldValue);
            bool hasRunner = TryGetFieldValue(behaviour, typeof(SimulateRunner), out object runnerValue);
            bool hasBuffSystem = TryGetFieldValue(behaviour, typeof(BuffSystemCore), out object buffSystemValue);

            if (!hasWorld || !hasRunner || !hasBuffSystem)
                continue;

            World candidateWorld = worldValue as World;
            SimulateRunner candidateRunner = runnerValue as SimulateRunner;
            BuffSystemCore candidateBuffSystem = buffSystemValue as BuffSystemCore;
            if (candidateWorld == null || candidateRunner == null || candidateBuffSystem == null)
                continue;

            bool matchesSelectedWorld = selectedWorld == null || ReferenceEquals(candidateWorld, selectedWorld) || ReferenceEquals(candidateRunner.World, selectedWorld);
            if (!matchesSelectedWorld && foundFullRoot)
                continue;

            best.world = candidateWorld;
            best.runner = candidateRunner;
            best.buffSystem = candidateBuffSystem;
            best.rootObject = behaviour;
            best.rootName = $"{behaviour.name} ({behaviour.GetType().Name})";
            best.worldOwnerName = best.rootName;
            best.runnerOwnerName = best.rootName;
            best.buffSystemOwnerName = best.rootName;
            foundFullRoot = true;

            if (matchesSelectedWorld)
                break;
        }

        if (best.buffSystem == null && best.world != null)
            best.buffSystem = ResolveBuffSystemCore(best.world);

        if (best.buffSystem != null && best.buffSystemOwnerName == "None")
            best.buffSystemOwnerName = "Reflection lookup";

        best.tickRunnerWorldEqualsRuntimeWorld = best.runner != null && best.world != null && ReferenceEquals(best.runner.World, best.world);
        best.addWorldEqualsRuntimeWorld = best.world != null;
        best.queryBuffSystemEqualsAddBuffSystem = best.buffSystem != null;
        best.diagnosis = BuildBuffDebugBindingDiagnosis(best);
        return best;
    }

    private static string BuildBuffDebugBindingDiagnosis(BuffDebugBinding binding)
    {
        if (binding.world == null || binding.runner == null || binding.buffSystem == null)
            return "绑定失败：未找到同一 SimulationInitializer 下的 World / Runner / BuffSystemCore。";

        if (!binding.tickRunnerWorldEqualsRuntimeWorld)
            return "绑定失败：Runner.World 与 RuntimeCount 使用的 World 不一致。";

        return "绑定一致：Add / Remove / Query 使用同一个 BuffSystemCore，Tick 使用同一个 Runner，RuntimeCount 使用同一个 World。";
    }

    private BuffSystemCore ResolveBuffSystemCore(World world)
    {
        if (world == null)
            return null;

        if (ReferenceEquals(_cachedBuffSystemWorld, world) && _cachedBuffSystemCore != null)
            return _cachedBuffSystemCore;

#if UNITY_2023_1_OR_NEWER
        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true);
#endif
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
                continue;

            if (!TryGetFieldValue(behaviour, typeof(World), out object worldValue) || !ReferenceEquals(worldValue, world))
                continue;

            if (TryGetFieldValue(behaviour, typeof(BuffSystemCore), out object buffSystemValue) && buffSystemValue is BuffSystemCore buffSystem)
            {
                _cachedBuffSystemWorld = world;
                _cachedBuffSystemCore = buffSystem;
                return buffSystem;
            }
        }

        _cachedBuffSystemWorld = null;
        _cachedBuffSystemCore = null;
        return null;
    }

    private static bool TryGetFieldValue(object target, Type fieldType, out object value)
    {
        value = null;
        if (target == null || fieldType == null)
            return false;

        FieldInfo[] fields = target.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo field = fields[i];
            if (!fieldType.IsAssignableFrom(field.FieldType))
                continue;

            value = SafeGetFieldValue(field, target);
            return value != null && !(value is string);
        }

        return false;
    }

    private void EnsureBuffDebugEntities(World world)
    {
        if (world == null)
            return;

        if (_selectedEntity.IsValid && world.IsAlive(_selectedEntity))
            _buffDebugTarget = _selectedEntity;

        if (!_buffDebugTarget.IsValid || !world.IsAlive(_buffDebugTarget))
            _buffDebugTarget = world.CreateEntity();

        if (!_buffDebugSource.IsValid || !world.IsAlive(_buffDebugSource))
            _buffDebugSource = _buffDebugTarget;
    }

    private void ResetBuffDebugEntities()
    {
        _buffDebugTarget = Entity.Invalid;
        _buffDebugSource = Entity.Invalid;
        _buffTargetIdText = string.Empty;
        _buffTargetVersionText = string.Empty;
        _buffSourceIdText = string.Empty;
        _buffSourceVersionText = string.Empty;
        _hasBuffDebugSnapshot = false;
        _buffDebugViews.Clear();
    }

    private void SyncBuffEntityFields()
    {
        _buffTargetIdText = _buffDebugTarget.IsValid ? _buffDebugTarget.ID.ToString() : string.Empty;
        _buffTargetVersionText = _buffDebugTarget.IsValid ? _buffDebugTarget.Version.ToString() : string.Empty;
        _buffSourceIdText = _buffDebugSource.IsValid ? _buffDebugSource.ID.ToString() : string.Empty;
        _buffSourceVersionText = _buffDebugSource.IsValid ? _buffDebugSource.Version.ToString() : string.Empty;
    }

    private bool TryReadBuffDebugInput(World world, out int configId, out Entity target, out Entity source)
    {
        configId = 0;
        target = Entity.Invalid;
        source = Entity.Invalid;

        if (!int.TryParse(_buffConfigIdText, out configId) || configId <= 0)
        {
            AppendBuffDebugLog("读取 ConfigId", false, "正整数 ConfigId", _buffConfigIdText);
            return false;
        }

        EnsureBuffDebugEntities(world);
        target = ReadEntity(_buffTargetIdText, _buffTargetVersionText, _buffDebugTarget);
        source = ReadEntity(_buffSourceIdText, _buffSourceVersionText, target);

        if (world == null || !target.IsValid || !world.IsAlive(target))
        {
            AppendBuffDebugLog("读取 Target", false, "存活的 target Entity", FormatEntity(target));
            return false;
        }

        if (!source.IsValid || !world.IsAlive(source))
            source = target;

        _buffDebugTarget = target;
        _buffDebugSource = source;
        SyncBuffEntityFields();
        return true;
    }

    private static Entity ReadEntity(string idText, string versionText, Entity fallback)
    {
        if (int.TryParse(idText, out int id) && int.TryParse(versionText, out int version))
            return new Entity(id, version);

        return fallback;
    }

    private int ReadBuffStackOrDefault()
    {
        return int.TryParse(_buffStackText, out int stack) && stack > 0 ? stack : 1;
    }

    private int ReadBuffTickFramesOrDefault()
    {
        return int.TryParse(_buffTickFramesText, out int frames) && frames > 0 ? frames : 1;
    }

    private bool QueueBuffDebugAdd(World world, BuffSystemCore buffSystem, int stack)
    {
        if (buffSystem == null || !TryReadBuffDebugInput(world, out int configId, out Entity target, out Entity source))
            return false;

        CaptureBuffDebugPreflight(world, buffSystem, configId, target, source, stack);
        buffSystem.AddBuff(new AddBuffCommand(target, configId, source, stack));
        AppendBuffDebugInfoLog("添加 Buff", stack == 3 ? "已入队 stack=3，请点击“Tick 一帧”后查看结果。" : "已入队，请点击“Tick 一帧”后查看结果。");
        return true;
    }

    private bool QueueBuffDebugRemove(World world, BuffSystemCore buffSystem, int stack)
    {
        if (buffSystem == null || !TryReadBuffDebugInput(world, out int configId, out Entity target, out Entity source))
            return false;

        CaptureBuffDebugPreflight(world, buffSystem, configId, target, source, stack);
        buffSystem.RemoveBuff(new RemoveBuffCommand(target, configId, source, stack));
        AppendBuffDebugInfoLog("移除 Buff", "已入队，请点击“Tick 一帧”后查看移除结果。");
        return true;
    }

    private void QueueBuffDebugAddAndTick(BuffDebugBinding binding, int stack)
    {
        QueueBuffDebugAddAndTick(binding, stack, 1);
    }

    private void QueueBuffDebugAddAndTick(BuffDebugBinding binding, int stack, int tickFrames)
    {
        if (QueueBuffDebugAdd(binding.world, binding.buffSystem, stack))
            TickBuffDebugFrames(binding, tickFrames, stack == 3 ? 3 : -1);
    }

    private void QueueBuffDebugRemoveAndTick(BuffDebugBinding binding, int stack)
    {
        if (QueueBuffDebugRemove(binding.world, binding.buffSystem, stack))
            TickBuffDebugFrames(binding, 1);
    }

    private void TickBuffDebugFrames(BuffDebugBinding binding, int frameCount)
    {
        TickBuffDebugFrames(binding, frameCount, -1);
    }

    private void TickBuffDebugFrames(BuffDebugBinding binding, int frameCount, int expectedStack)
    {
        SimulateRunner runner = binding.runner;
        if (runner == null)
        {
            AppendBuffDebugLog("Tick", false, "当前 Debug Source 绑定 SimulateRunner", "Runner 为空");
            return;
        }

        int count = frameCount > 0 ? frameCount : 1;
        bool ticked = false;
        for (int i = 0; i < count; i++)
            ticked |= runner.StepNextFrame(false);

        RefreshData();
        AppendBuffDebugInfoLog($"Tick {count}", ticked ? "Runner 已推进固定帧。" : "Runner 未推进固定帧。");
        RefreshBuffDebugSnapshot(binding.world, binding.buffSystem, $"Tick {count}", expectedStack);
    }

    private void RefreshBuffDebugSnapshot(World world, BuffSystemCore buffSystem, string action)
    {
        RefreshBuffDebugSnapshot(world, buffSystem, action, -1);
    }

    private void RefreshBuffDebugPreflight(BuffDebugBinding binding, string action, bool appendLog)
    {
        if (binding.buffSystem == null || !TryReadBuffDebugInput(binding.world, out int configId, out Entity target, out Entity source))
            return;

        CaptureBuffDebugPreflight(binding.world, binding.buffSystem, configId, target, source, ReadBuffStackOrDefault());

        if (appendLog)
            AppendBuffDebugInfoLog(action, BuildBuffDebugPreflightDiagnosis(_buffDebugPreflight));
    }

    private void CaptureBuffDebugPreflight(World world, BuffSystemCore buffSystem, int configId, Entity target, Entity source, int stack)
    {
        _buffDebugPreflight = new BuffDebugPreflight
        {
            configId = configId,
            stack = stack > 0 ? stack : 1,
            target = target,
            source = source.IsValid ? source : target,
            targetAlive = world != null && target.IsValid && world.IsAlive(target),
            sourceAlive = world != null && source.IsValid && world.IsAlive(source)
        };

        _buffDebugPreflight.commandIsValid = new AddBuffCommand(target, configId, source, stack).IsValid;

        IBuffDefinitionProvider provider = GetPrivateFieldValue<IBuffDefinitionProvider>(buffSystem, "_definitionProvider");
        _buffDebugPreflight.providerType = provider != null ? provider.GetType().Name : "null";
        _buffDebugPreflight.providerSource = FormatReference(provider);
        _buffDebugPreflight.providerRootPath = ReadProviderRootPath(provider);
        _buffDebugPreflight.loadedDefinitionCount = ReadProviderDefinitionCount(provider);
        _buffDebugPreflight.loadedConfigIds = ReadProviderLoadedConfigIds(provider);

        if (provider != null)
            _buffDebugPreflight.definitionFound = provider.TryGetDefinition(configId, out _buffDebugPreflight.definition);

        if (_buffDebugPreflight.definitionFound)
        {
            BuffDefinition definition = _buffDebugPreflight.definition;
            _buffDebugPreflight.buffTypeParallel = definition.BuffType == BuffInstanceType.parallel;
            _buffDebugPreflight.storageCompressed = definition.ParallelStorageMode == ParallelBuffStorageMode.CompressedExpiryFrameList;
            _buffDebugPreflight.triggerTick = definition.TriggerType == BuffTriggerType.Tick;
            _buffDebugPreflight.unlimitedFalse = !definition.Unlimited;
            _buffDebugPreflight.maxStackWithinCapacity = definition.MaxStack <= CompressedParallelBuffLayerBuffer.Capacity;
            _buffDebugPreflight.eligibility = _buffDebugPreflight.buffTypeParallel
                && _buffDebugPreflight.storageCompressed
                && _buffDebugPreflight.triggerTick
                && _buffDebugPreflight.unlimitedFalse
                && _buffDebugPreflight.maxStackWithinCapacity;
            _buffDebugPreflight.effectId = definition.EffectId;
        }

        _buffDebugPreflight.compressedGate = GetPrivateFieldValue<bool>(buffSystem, "_enableCompressedParallelRuntime");
        HashSet<int> whitelist = GetPrivateFieldValue<HashSet<int>>(buffSystem, "_compressedParallelWhitelist");
        _buffDebugPreflight.whitelistHit = whitelist != null && whitelist.Contains(configId);
        _buffDebugPreflight.whitelistConfigIds = FormatIntSet(whitelist);
        _buffDebugPreflight.shouldUseCompressedExpected = _buffDebugPreflight.compressedGate
            && _buffDebugPreflight.whitelistHit
            && _buffDebugPreflight.eligibility;
        _buffDebugPreflight.coreModeHint = BuildBuffDebugCoreModeHint(_buffDebugPreflight, whitelist);

        BuffEffectRegistry registry = GetPrivateFieldValue<BuffEffectRegistry>(buffSystem, "_effectRegistry");
        _buffDebugPreflight.effectRegistryExists = registry != null;
        _buffDebugPreflight.effectRegistered = registry != null
            && _buffDebugPreflight.effectId != 0
            && registry.TryGet(_buffDebugPreflight.effectId, out IBuffEffectExecutor _);

        _hasBuffDebugPreflight = true;
    }

    private void RefreshBuffDebugSnapshot(World world, BuffSystemCore buffSystem, string action, int expectedStack)
    {
        if (buffSystem == null || !TryReadBuffDebugInput(world, out int configId, out Entity target, out Entity source))
            return;

        CaptureBuffDebugPreflight(world, buffSystem, configId, target, source, ReadBuffStackOrDefault());
        CaptureBuffDebugSnapshot(world, buffSystem, configId, target, source);
        string status = EvaluateBuffDebugSnapshot(_buffDebugSnapshot, expectedStack, out string expected, out string actual);
        AppendBuffDebugStatusLog(action, status, expected, actual);
    }

    private void CaptureBuffDebugSnapshot(World world, BuffSystemCore buffSystem, int configId, Entity target, Entity source)
    {
        _buffDebugViews.Clear();
        _buffDebugSnapshot = new BuffDebugSnapshot
        {
            configId = configId,
            target = target,
            source = source.IsValid ? source : target,
            targetAlive = world != null && target.IsValid && world.IsAlive(target),
            sourceAlive = world != null && source.IsValid && world.IsAlive(source)
        };

        if (world == null || buffSystem == null || !_buffDebugSnapshot.targetAlive)
        {
            _hasBuffDebugSnapshot = true;
            return;
        }

        _buffDebugSnapshot.found = buffSystem.TryGetBuff(target, configId, _buffDebugSnapshot.source, out _buffDebugSnapshot.view);
        IReadOnlyList<BuffViewData> views = buffSystem.GetBuffs(target);
        if (views != null)
        {
            _buffDebugSnapshot.getBuffsCount = views.Count;
            for (int i = 0; i < views.Count; i++)
            {
                BuffViewData view = views[i];
                _buffDebugViews.Add(view);
                if (view.ConfigId == configId)
                    _buffDebugSnapshot.matchingViewCount++;
            }
        }

        world.ForEach<BuffRuntimeComponent>((Entity entity, ref BuffRuntimeComponent runtime) =>
        {
            _buffDebugSnapshot.entityPerStackRuntimeCount++;
            if (runtime.configId == configId && runtime.target == target)
                _buffDebugSnapshot.configEntityPerStackRuntimeCount++;
        });

        world.ForEach<CompressedParallelBuffRuntimeComponent>((Entity entity, ref CompressedParallelBuffRuntimeComponent runtime) =>
        {
            _buffDebugSnapshot.compressedRuntimeCount++;
            if (runtime.configId == configId && runtime.target == target)
                _buffDebugSnapshot.configCompressedRuntimeCount++;
        });

        _hasBuffDebugSnapshot = true;
    }

    private void CaptureAndLogBuffCommandQueueStage(BuffDebugBinding binding, string stage, string action)
    {
        if (!TryCaptureBuffCommandQueueStage(binding, stage, out BuffCommandQueueStageSnapshot snapshot))
            return;

        AppendBuffCommandQueueStage(snapshot);
        AppendBuffDebugInfoLog(action, snapshot.diagnosis);
    }

    private void AddBuffAndCaptureCommandQueue(BuffDebugBinding binding)
    {
        if (binding.buffSystem == null || !TryReadBuffDebugInput(binding.world, out int configId, out Entity target, out Entity source))
            return;

        int stack = ReadBuffStackOrDefault();
        CaptureBuffDebugPreflight(binding.world, binding.buffSystem, configId, target, source, stack);
        binding.buffSystem.AddBuff(new AddBuffCommand(target, configId, source, stack));
        CaptureAndLogBuffCommandQueueStage(binding, "AfterAdd", "添加 Buff 并记录队列状态");
    }

    private void TickAndCaptureCommandQueue(BuffDebugBinding binding, int frameCount)
    {
        if (binding.runner == null)
        {
            AppendBuffDebugLog("Tick 队列诊断", false, "Runner 有效", "Runner 为空");
            return;
        }

        int count = frameCount > 0 ? frameCount : 1;
        bool ticked = false;
        for (int i = 0; i < count; i++)
            ticked |= binding.runner.StepNextFrame(false);

        RefreshData();
        CaptureAndLogBuffCommandQueueStage(binding, count == 1 ? "AfterTick1" : "AfterTick2", ticked ? $"Tick {count} 并记录队列状态" : $"Tick {count} 未推进");
    }

    private void RunBuffAddQueueChainDiagnostics(BuffDebugBinding binding)
    {
        _buffCommandQueueStages.Clear();

        if (!TryCaptureBuffCommandQueueStage(binding, "BeforeAdd", out BuffCommandQueueStageSnapshot beforeAdd))
            return;

        AppendBuffCommandQueueStage(beforeAdd);

        if (binding.buffSystem == null || !TryReadBuffDebugInput(binding.world, out int configId, out Entity target, out Entity source))
            return;

        int stack = ReadBuffStackOrDefault();
        CaptureBuffDebugPreflight(binding.world, binding.buffSystem, configId, target, source, stack);
        binding.buffSystem.AddBuff(new AddBuffCommand(target, configId, source, stack));

        if (TryCaptureBuffCommandQueueStage(binding, "AfterAdd", out BuffCommandQueueStageSnapshot afterAdd))
            AppendBuffCommandQueueStage(afterAdd);

        if (binding.runner != null)
            binding.runner.StepNextFrame(false);

        RefreshData();
        if (TryCaptureBuffCommandQueueStage(binding, "AfterTick1", out BuffCommandQueueStageSnapshot afterTick1))
            AppendBuffCommandQueueStage(afterTick1);

        if (binding.runner != null)
            binding.runner.StepNextFrame(false);

        RefreshData();
        if (TryCaptureBuffCommandQueueStage(binding, "AfterTick2", out BuffCommandQueueStageSnapshot afterTick2))
            AppendBuffCommandQueueStage(afterTick2);

        string diagnosis = BuildBuffCommandQueueChainDiagnosis();
        AppendBuffDebugInfoLog("执行 Add 队列链路诊断", diagnosis);
        _buffDebugCopyText = BuildBuffCommandQueueDiagnosticsCopyText();
    }

    private void RunBuffCompressedRuntimeLifecycleTrace(BuffDebugBinding binding)
    {
        RunBuffAddQueueChainDiagnostics(binding);
        string diagnosis = BuildBuffCompressedRuntimeLifecycleDiagnosis();
        AppendBuffDebugInfoLog("执行 compressed 生命周期 Trace", diagnosis);
        _buffDebugCopyText = BuildBuffCompressedRuntimeTraceCopyText();
    }

    private bool TryCaptureBuffCommandQueueStage(BuffDebugBinding binding, string stage, out BuffCommandQueueStageSnapshot snapshot)
    {
        snapshot = default;

        if (binding.buffSystem == null || !TryReadBuffDebugInput(binding.world, out int configId, out Entity target, out Entity source))
            return false;

        CaptureBuffDebugPreflight(binding.world, binding.buffSystem, configId, target, source, ReadBuffStackOrDefault());
        CaptureBuffDebugSnapshot(binding.world, binding.buffSystem, configId, target, source);
        CaptureBuffCommandQueueFields(binding.buffSystem);

        snapshot = new BuffCommandQueueStageSnapshot
        {
            stage = stage,
            frame = GetBuffDebugFrameNumber(),
            addQueueCount = CountQueuedCommands(binding.buffSystem, true),
            removeQueueCount = CountQueuedCommands(binding.buffSystem, false),
            pendingAddCount = GetCommandLikeFieldCount("_addRequestEntities"),
            pendingRemoveCount = GetCommandLikeFieldCount("_pendingRemoveRuntimes"),
            commandBufferCount = GetCommandLikeFieldCount("_queuedCommands"),
            compressedRuntimeCount = _buffDebugSnapshot.compressedRuntimeCount,
            configCompressedRuntimeCount = _buffDebugSnapshot.configCompressedRuntimeCount,
            entityPerStackRuntimeCount = _buffDebugSnapshot.entityPerStackRuntimeCount,
            configEntityPerStackRuntimeCount = _buffDebugSnapshot.configEntityPerStackRuntimeCount,
            tryGetBuffFound = _buffDebugSnapshot.found,
            getBuffsCount = _buffDebugSnapshot.getBuffsCount,
            matchingViewCount = _buffDebugSnapshot.matchingViewCount,
            compressedTrace = CaptureBuffCompressedRuntimeTrace(binding.world, binding.buffSystem, configId, target, source, stage)
        };
        snapshot.diagnosis = BuildBuffCommandQueueStageDiagnosis(snapshot);
        return true;
    }

    private BuffCompressedRuntimeTraceSnapshot CaptureBuffCompressedRuntimeTrace(
        World world,
        BuffSystemCore buffSystem,
        int configId,
        Entity target,
        Entity source,
        string stage)
    {
        int frame = GetBuffDebugFrameNumber();
        Entity traceSource = source.IsValid ? source : target;
        BuffCompressedRuntimeTraceSnapshot trace = new BuffCompressedRuntimeTraceSnapshot
        {
            stage = stage,
            frame = frame,
            configId = configId,
            target = target,
            source = traceSource,
            targetAlive = world != null && target.IsValid && world.IsAlive(target),
            sourceAlive = world != null && traceSource.IsValid && world.IsAlive(traceSource),
            runtimeEntity = Entity.Invalid,
            lookupEntity = Entity.Invalid,
            layers = new List<BuffCompressedLayerTrace>(CompressedParallelBuffLayerBuffer.Capacity)
        };

        object pendingRuntimes = GetPrivateFieldValue<object>(buffSystem, "_pendingRemoveRuntimes");
        object pendingSet = GetPrivateFieldValue<object>(buffSystem, "_pendingRemoveRuntimeSet");
        trace.pendingRemoveRuntimeCount = CountCollectionValue(pendingRuntimes);
        trace.pendingRemoveSetCount = CountCollectionValue(pendingSet);

        TryReadCompressedRuntimeLookup(
            buffSystem,
            target,
            trace.source,
            configId,
            out int compressedLookupCount,
            out bool compressedLookupHit,
            out Entity lookupEntity);
        trace.compressedLookupCount = compressedLookupCount;
        trace.compressedLookupHit = compressedLookupHit;
        trace.lookupEntity = lookupEntity;

        if (world == null)
        {
            trace.expireSummary = "World 为空，无法读取 compressed runtime。";
            return trace;
        }

        bool found = false;
        world.ForEach<CompressedParallelBuffRuntimeComponent>((Entity entity, ref CompressedParallelBuffRuntimeComponent runtime) =>
        {
            if (found || runtime.configId != configId || runtime.target != target)
                return;

            found = true;
            trace.runtimeExists = true;
            trace.runtimeEntity = entity;
            trace.runtimeEntityAlive = entity.IsValid && world.IsAlive(entity);
            trace.target = runtime.target;
            trace.source = runtime.source;
            trace.configId = runtime.configId;
            trace.layerCount = runtime.layerCount;
            trace.compressedRuntimeHandle = runtime.compressedRuntimeHandle;
            trace.targetAlive = runtime.target.IsValid && world.IsAlive(runtime.target);
            trace.sourceAlive = runtime.source.IsValid && world.IsAlive(runtime.source);
            trace.pendingRemove = ContainsEntityInEnumerable(pendingSet, entity);

            int minExpireFrame = int.MaxValue;
            int expiredCount = 0;
            int maxLayerCount = Math.Min(runtime.layerCount, CompressedParallelBuffLayerBuffer.Capacity);
            for (int i = 0; i < maxLayerCount; i++)
            {
                CompressedParallelBuffLayer layer = runtime.layers.Get(i);
                bool forever = layer.expireFrame == int.MaxValue;
                bool expired = !forever && frame >= layer.expireFrame;
                if (expired)
                    expiredCount++;

                if (!forever && layer.expireFrame < minExpireFrame)
                    minExpireFrame = layer.expireFrame;

                trace.layers.Add(new BuffCompressedLayerTrace
                {
                    index = i,
                    layerId = layer.layerId,
                    layerRuntimeHandle = layer.layerRuntimeHandle,
                    expireFrame = layer.expireFrame,
                    remainingFrames = forever ? -1 : layer.expireFrame - frame,
                    elapsedFrames = layer.elapsedFrames,
                    ticks = layer.ticks,
                    expired = expired
                });
            }

            trace.minExpireFrame = minExpireFrame == int.MaxValue ? -1 : minExpireFrame;
            trace.expiredLayerCount = expiredCount;
            trace.expireSummary = maxLayerCount == 0
                ? "无 layer。"
                : $"LayerCount={runtime.layerCount}, MinExpireFrame={trace.minExpireFrame}, ExpiredLayers={expiredCount}";
        });

        if (!trace.runtimeExists)
            trace.expireSummary = "当前阶段未找到匹配 ConfigId / Target 的 compressed runtime。";

        return trace;
    }

    private void AppendBuffCommandQueueStage(BuffCommandQueueStageSnapshot snapshot)
    {
        _buffCommandQueueStages.Add(snapshot);

        while (_buffCommandQueueStages.Count > 8)
            _buffCommandQueueStages.RemoveAt(0);
    }

    private void CaptureBuffCommandQueueFields(BuffSystemCore buffSystem)
    {
        _buffCommandQueueFields.Clear();

        if (buffSystem == null)
            return;

        FieldInfo[] fields = buffSystem.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo field = fields[i];
            if (!IsCommandLikeFieldName(field.Name))
                continue;

            BuffCommandQueueFieldInfo info = new BuffCommandQueueFieldInfo
            {
                fieldName = field.Name,
                fieldType = field.FieldType.Name
            };

            try
            {
                object value = field.GetValue(buffSystem);
                info.readable = true;
                info.count = CountCollectionValue(value);
                info.valueSummary = BuildCommandLikeFieldSummary(field.Name, value);
            }
            catch (Exception ex)
            {
                info.readable = false;
                info.count = -1;
                info.valueSummary = ex.GetType().Name;
            }

            _buffCommandQueueFields.Add(info);
        }
    }

    private static bool IsCommandLikeFieldName(string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName))
            return false;

        return fieldName.IndexOf("Add", StringComparison.OrdinalIgnoreCase) >= 0
            || fieldName.IndexOf("Remove", StringComparison.OrdinalIgnoreCase) >= 0
            || fieldName.IndexOf("Command", StringComparison.OrdinalIgnoreCase) >= 0
            || fieldName.IndexOf("Queue", StringComparison.OrdinalIgnoreCase) >= 0
            || fieldName.IndexOf("Pending", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int CountCollectionValue(object value)
    {
        if (value == null)
            return 0;

        if (value is ICollection collection)
            return collection.Count;

        if (value is IDictionary dictionary)
            return dictionary.Count;

        if (value is IEnumerable enumerable && !(value is string))
        {
            int count = 0;
            foreach (object _ in enumerable)
            {
                count++;
                if (count > 999)
                    break;
            }

            return count;
        }

        return -1;
    }

    private static string BuildCommandLikeFieldSummary(string fieldName, object value)
    {
        if (value == null)
            return "null";

        if (fieldName == "_queuedCommands")
            return $"Add={CountQueuedCommandsInValue(value, true)}, Remove={CountQueuedCommandsInValue(value, false)}";

        int count = CountCollectionValue(value);
        return count >= 0 ? $"Count={count}" : value.ToString();
    }

    private static int CountQueuedCommands(BuffSystemCore buffSystem, bool isAdd)
    {
        object queuedCommands = GetPrivateFieldValue<object>(buffSystem, "_queuedCommands");
        return CountQueuedCommandsInValue(queuedCommands, isAdd);
    }

    private static int CountQueuedCommandsInValue(object queuedCommands, bool isAdd)
    {
        if (!(queuedCommands is IEnumerable enumerable))
            return 0;

        int count = 0;
        foreach (object command in enumerable)
        {
            FieldInfo isAddField = command.GetType().GetField("isAdd", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (isAddField != null && isAddField.GetValue(command) is bool commandIsAdd && commandIsAdd == isAdd)
                count++;
        }

        return count;
    }

    private static bool ContainsEntityInEnumerable(object value, Entity entity)
    {
        if (value == null || !entity.IsValid || !(value is IEnumerable enumerable))
            return false;

        foreach (object item in enumerable)
        {
            if (item is Entity candidate && candidate == entity)
                return true;

            if (TryReadEntityMember(item, "runtimeEntity", out Entity runtimeEntity) && runtimeEntity == entity)
                return true;
        }

        return false;
    }

    private static bool TryReadCompressedRuntimeLookup(
        BuffSystemCore buffSystem,
        Entity target,
        Entity source,
        int configId,
        out int lookupCount,
        out bool hit,
        out Entity lookupEntity)
    {
        lookupCount = 0;
        hit = false;
        lookupEntity = Entity.Invalid;

        object lookup = GetPrivateFieldValue<object>(buffSystem, "_compressedRuntimeEntityByKey");
        lookupCount = CountCollectionValue(lookup);
        if (!(lookup is IEnumerable enumerable))
            return false;

        foreach (object entry in enumerable)
        {
            if (!TryReadKeyValuePair(entry, out object key, out object value))
                continue;

            if (!TryReadRuntimeKey(key, out Entity keyTarget, out Entity keySource, out int keyConfigId))
                continue;

            if (keyConfigId != configId || keyTarget != target || keySource != source)
                continue;

            hit = true;
            if (value is Entity entityValue)
                lookupEntity = entityValue;
            return true;
        }

        return true;
    }

    private static bool TryReadKeyValuePair(object entry, out object key, out object value)
    {
        key = null;
        value = null;

        if (entry == null)
            return false;

        Type type = entry.GetType();
        PropertyInfo keyProperty = type.GetProperty("Key", BindingFlags.Instance | BindingFlags.Public);
        PropertyInfo valueProperty = type.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
        if (keyProperty == null || valueProperty == null)
            return false;

        key = keyProperty.GetValue(entry);
        value = valueProperty.GetValue(entry);
        return true;
    }

    private static bool TryReadRuntimeKey(object key, out Entity target, out Entity source, out int configId)
    {
        target = Entity.Invalid;
        source = Entity.Invalid;
        configId = 0;

        return TryReadEntityMember(key, "target", out target)
            && TryReadEntityMember(key, "source", out source)
            && TryReadIntMember(key, "configId", out configId);
    }

    private static bool TryReadEntityMember(object instance, string memberName, out Entity entity)
    {
        entity = Entity.Invalid;

        if (instance == null)
            return false;

        Type type = instance.GetType();
        FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && field.GetValue(instance) is Entity fieldEntity)
        {
            entity = fieldEntity;
            return true;
        }

        PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.GetValue(instance) is Entity propertyEntity)
        {
            entity = propertyEntity;
            return true;
        }

        return false;
    }

    private static bool TryReadIntMember(object instance, string memberName, out int value)
    {
        value = 0;

        if (instance == null)
            return false;

        Type type = instance.GetType();
        FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && field.GetValue(instance) is int fieldValue)
        {
            value = fieldValue;
            return true;
        }

        PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.GetValue(instance) is int propertyValue)
        {
            value = propertyValue;
            return true;
        }

        return false;
    }

    private int GetCommandLikeFieldCount(string fieldName)
    {
        for (int i = 0; i < _buffCommandQueueFields.Count; i++)
        {
            if (_buffCommandQueueFields[i].fieldName == fieldName)
                return _buffCommandQueueFields[i].count;
        }

        return 0;
    }

    private static string BuildBuffCommandQueueStageDiagnosis(BuffCommandQueueStageSnapshot snapshot)
    {
        if (snapshot.configCompressedRuntimeCount == 1 && snapshot.configEntityPerStackRuntimeCount == 0)
            return "Add 队列消费成功，compressed path 生效。";

        if (snapshot.configEntityPerStackRuntimeCount > 0)
            return "Add 队列已产生 Runtime，但 fallback 到 EntityPerStack。";

        if (snapshot.addQueueCount > 0)
            return "Add 命令仍在 BuffSystemCore 队列中，等待 Tick 消费。";

        if (snapshot.stage.IndexOf("Tick", StringComparison.OrdinalIgnoreCase) >= 0 && snapshot.addQueueCount == 0 && snapshot.configCompressedRuntimeCount == 0 && snapshot.configEntityPerStackRuntimeCount == 0)
            return "Add 队列为空但未创建 Runtime。若 Add 后队列曾增加，说明命令已被消费但 ApplyAddCommand 未创建 runtime。";

        return "当前阶段未发现目标 ConfigId Runtime。";
    }

    private string BuildBuffCommandQueueChainDiagnosis()
    {
        BuffCommandQueueStageSnapshot beforeAdd = FindCommandQueueStage("BeforeAdd");
        BuffCommandQueueStageSnapshot afterAdd = FindCommandQueueStage("AfterAdd");
        BuffCommandQueueStageSnapshot afterTick1 = FindCommandQueueStage("AfterTick1");
        BuffCommandQueueStageSnapshot afterTick2 = FindCommandQueueStage("AfterTick2");

        bool hasBeforeAdd = !string.IsNullOrEmpty(beforeAdd.stage);
        bool hasAfterAdd = !string.IsNullOrEmpty(afterAdd.stage);
        bool hasAfterTick1 = !string.IsNullOrEmpty(afterTick1.stage);
        bool hasAfterTick2 = !string.IsNullOrEmpty(afterTick2.stage);

        if (!hasBeforeAdd && !hasAfterAdd && !hasAfterTick1 && !hasAfterTick2)
            return "尚未执行 Add 队列链路诊断。";

        if (!hasBeforeAdd || !hasAfterAdd || !hasAfterTick1 || !hasAfterTick2)
            return "队列链路阶段不完整，请点击“执行 Add 队列链路诊断”生成 BeforeAdd / AfterAdd / AfterTick1 / AfterTick2 完整快照。";

        if (afterTick1.configCompressedRuntimeCount == 1 && afterTick2.configCompressedRuntimeCount == 0 && afterTick2.configEntityPerStackRuntimeCount == 0)
            return "compressed runtime 已创建，但下一帧被移除 / 销毁 / 过滤，请查看生命周期 Trace。";

        if (afterTick1.configCompressedRuntimeCount == 1 || afterTick2.configCompressedRuntimeCount == 1)
            return "Tick 后 compressed=1：Add 队列消费成功，compressed path 生效。";

        if (afterTick1.configEntityPerStackRuntimeCount > 0 || afterTick2.configEntityPerStackRuntimeCount > 0)
            return "Tick 后 entityPerStack>0：Add 队列消费成功，但 fallback 到 EntityPerStack。";

        if (afterAdd.addQueueCount <= beforeAdd.addQueueCount)
            return "Add 后 AddQueue 没增加：AddBuff 未真正入队，请检查 AddBuff 调用路径或队列字段识别。";

        if (afterTick1.addQueueCount >= afterAdd.addQueueCount && afterTick2.addQueueCount >= afterAdd.addQueueCount)
            return "Add 后 AddQueue 增加，但 Tick 后 AddQueue 不减少：BuffSystemCore.Tick 未消费 Add 队列，请检查 BuffSystemBridge 是否实际 Tick / ConsumeQueuedCommands 是否执行。";

        if (afterAdd.addQueueCount > beforeAdd.addQueueCount && (afterTick1.addQueueCount < afterAdd.addQueueCount || afterTick2.addQueueCount < afterAdd.addQueueCount))
            return "Add 后 AddQueue 增加，Tick 后 AddQueue 减少，但 runtime 仍为 0：Add 命令已被消费，但 ApplyAddCommand 未创建 runtime，继续检查 ApplyAddCommand 内部早退条件。";

        return "队列链路状态未能归类，请复制 CommandQueues 日志进一步分析。";
    }

    private string BuildBuffCompressedRuntimeLifecycleDiagnosis()
    {
        BuffCommandQueueStageSnapshot afterTick1 = FindCommandQueueStage("AfterTick1");
        BuffCommandQueueStageSnapshot afterTick2 = FindCommandQueueStage("AfterTick2");
        bool hasAfterTick1 = !string.IsNullOrEmpty(afterTick1.stage);
        bool hasAfterTick2 = !string.IsNullOrEmpty(afterTick2.stage);

        if (!hasAfterTick1 || !hasAfterTick2)
            return "生命周期 Trace 阶段不完整，请点击“执行 compressed 生命周期 Trace”。";

        BuffCompressedRuntimeTraceSnapshot tick1 = afterTick1.compressedTrace;
        BuffCompressedRuntimeTraceSnapshot tick2 = afterTick2.compressedTrace;

        if (!tick1.runtimeExists)
            return "AfterTick1 未捕获 compressed runtime，请先确认 Add 队列是否创建 runtime。";

        if (tick1.runtimeExists && tick2.runtimeExists)
            return "AfterTick1 与 AfterTick2 均存在 compressed runtime，生命周期 Trace 未发现下一帧消失。";

        if (tick1.runtimeExists && !tick2.runtimeExists)
        {
            if (tick1.layerCount <= 0)
                return "Runtime 在 Tick1 创建成功，但 layerCount <= 0：compressed add 写入 layer 失败或同帧被清空。";

            if (!tick1.targetAlive)
                return "Runtime 在 Tick1 创建成功，但 targetAlive=false：Tick2 可能按 target dead 清理 runtime。";

            if (tick1.minExpireFrame >= 0 && tick1.minExpireFrame <= tick1.frame)
                return "Runtime 在 Tick1 创建成功，但 expireFrame <= currentFrame：layer 已到期，下一帧会自然过期并进入清理。";

            if (tick1.pendingRemove)
                return "Runtime 在 Tick1 创建后已进入 pending remove：下一帧可能被 DestroyPendingRemoveRuntimes 清理。";

            return "Runtime 在 Tick1 创建成功且 Tick1 快照未见明显异常，但 Tick2 后消失：需要继续 trace TickCompressedParallelRuntimes / DestroyPendingRemoveRuntimes 清理路径。";
        }

        return "生命周期 Trace 状态未能归类，请复制 compressed 生命周期日志进一步分析。";
    }

    private BuffCommandQueueStageSnapshot FindCommandQueueStage(string stage)
    {
        for (int i = 0; i < _buffCommandQueueStages.Count; i++)
        {
            if (_buffCommandQueueStages[i].stage == stage)
                return _buffCommandQueueStages[i];
        }

        return default;
    }

    private string GetBuffDebugCompressedPathStatus(BuffDebugSnapshot snapshot)
    {
        if (snapshot.configCompressedRuntimeCount == 1 && snapshot.configEntityPerStackRuntimeCount == 0)
            return "PASS";

        if (snapshot.configCompressedRuntimeCount == 0 && snapshot.configEntityPerStackRuntimeCount > 0)
            return "FALLBACK_ENTITY_PER_STACK";

        if (snapshot.configCompressedRuntimeCount == 0 && snapshot.configEntityPerStackRuntimeCount == 0)
        {
            if (HasRecentBuffDebugTickAfterQueuedCommand())
                return "ADD_TICKED_NO_RUNTIME";

            return HasRecentBuffDebugQueuedCommand() ? "QUEUED_NOT_CONSUMED" : "NOT_READY";
        }

        return "FAIL";
    }

    private string GetBuffDebugViewDataState(BuffDebugSnapshot snapshot)
    {
        if (snapshot.found)
            return "ViewData 可见";

        if (snapshot.configCompressedRuntimeCount == 1 && snapshot.configEntityPerStackRuntimeCount == 0)
            return "等待下一帧 Capture";

        if (snapshot.configCompressedRuntimeCount == 0 && snapshot.configEntityPerStackRuntimeCount == 0 && HasRecentBuffDebugQueuedCommand())
        {
            if (HasRecentBuffDebugTickAfterQueuedCommand())
                return "Add 已 Tick，但未创建 Runtime";

            return "命令已入队，尚未 Tick 消费";
        }

        if (snapshot.configCompressedRuntimeCount == 0 && snapshot.configEntityPerStackRuntimeCount > 0)
            return "当前 fallback 到 EntityPerStack";

        return "不可见";
    }

    private string BuildBuffDebugDiagnosis(BuffDebugSnapshot snapshot)
    {
        if (!snapshot.targetAlive)
            return "Target Entity 不存活，请先使用 / 创建调试 Entity。";

        if (snapshot.configCompressedRuntimeCount == 1 && snapshot.configEntityPerStackRuntimeCount == 0)
        {
            return snapshot.found
                ? "压缩路径生效，ViewData 已可见。"
                : "压缩 Runtime 已创建，ViewData 需下一帧 Capture 后可见，请再 Tick 一帧。";
        }

        if (snapshot.configCompressedRuntimeCount == 0 && snapshot.configEntityPerStackRuntimeCount > 0)
            return "当前 fallback 到 EntityPerStack。请检查 whitelist / ParallelStorageMode / TriggerType / MaxStack / production factory。";

        if (snapshot.configCompressedRuntimeCount == 0 && snapshot.configEntityPerStackRuntimeCount == 0 && HasRecentBuffDebugTickAfterQueuedCommand())
            return "Add 已 Tick，但未创建 runtime。请检查 BuffSystem 是否被 Runner 驱动、definition 是否加载、target/source 是否属于当前 World、BuffSystemCore / Runner / World 是否绑定一致。";

        if (snapshot.configCompressedRuntimeCount == 0 && snapshot.configEntityPerStackRuntimeCount == 0 && HasRecentBuffDebugQueuedCommand())
            return "命令已入队，尚未 Tick 消费。请 Tick 一帧后再次查看 Runtime / ViewData 状态。";

        return "当前未发现该 ConfigId 的 Runtime 或 ViewData。";
    }

    private bool HasRecentBuffDebugQueuedCommand()
    {
        for (int i = 0; i < _buffDebugLogs.Count; i++)
        {
            string log = _buffDebugLogs[i];
            if (!string.IsNullOrEmpty(log) && log.Contains("已入队"))
                return true;
        }

        return false;
    }

    private static MessageType GetBuffDebugPreflightMessageType(BuffDebugPreflight preflight)
    {
        return preflight.definitionFound
            && preflight.commandIsValid
            && preflight.targetAlive
            && preflight.sourceAlive
            && preflight.effectRegistered
            && preflight.shouldUseCompressedExpected
            ? MessageType.Info
            : MessageType.Warning;
    }

    private static string BuildBuffDebugPreflightDiagnosis(BuffDebugPreflight preflight)
    {
        if (!preflight.definitionFound)
            return "找不到当前 ConfigId 的 BuffDefinition。请检查 BuffConfigDataLoader Root Path、Resources 路径、asset ID、loader 初始化时机，以及 production BuffSystemCore 持有的 provider 是否为当前 loader。";

        if (!preflight.commandIsValid)
            return "AddBuffCommand 无效。请检查 ConfigId 是否大于 0、Stack 是否大于 0、Target 是否有效。";

        if (!preflight.targetAlive)
            return "Target 不属于当前 World 或已死亡，ApplyAddCommand 会早退。";

        if (!preflight.sourceAlive)
            return "Source 不属于当前 World 或已死亡。当前 Add 可入队，但 source 不一致会影响后续 Remove / Query。";

        if (!preflight.effectRegistryExists)
            return "BuffEffectRegistry 不存在。Runtime 可能可创建，但 Effect 无法执行，请检查 production 初始化注入。";

        if (!preflight.effectRegistered)
            return $"EffectId={preflight.effectId} 未注册。请检查 BuffEffectRegistryBootstrap 是否注册 DebugNoOpTickEffect。";

        if (!preflight.eligibility)
            return "Definition 存在，但 Compressed eligibility 未通过。请检查 BuffType / ParallelStorageMode / TriggerType / Unlimited / MaxStack。";

        if (!preflight.compressedGate)
            return "Compressed gate 为 false。当前 public constructor 或非 production factory 路径不会启用 compressed。";

        if (!preflight.whitelistHit)
            return "当前 ConfigId 未命中 compressed whitelist，因此会 fallback 到 EntityPerStack。";

        if (!preflight.shouldUseCompressedExpected)
            return "ShouldUseCompressedParallel 预期为 false。请检查 gate && whitelist && eligibility 三重门禁。";

        return "Definition / Command / Effect / Eligibility / whitelist 均通过。如果 Tick 后仍未创建 Runtime，继续检查 BuffSystemCore.Tick 是否进入 ConsumeQueuedCommands 或 ApplyAddCommand 其他早退条件。";
    }

    private static string BuildBuffDebugCoreModeHint(BuffDebugPreflight preflight, HashSet<int> whitelist)
    {
        if (!preflight.compressedGate)
            return "gate=false";

        if (whitelist == null || whitelist.Count == 0)
            return "gate=true, whitelist empty";

        bool hasValidationIds = false;
        foreach (int id in whitelist)
        {
            if (id >= 9301 && id <= 9315)
            {
                hasValidationIds = true;
                break;
            }
        }

        if (hasValidationIds && whitelist.Contains(991001))
            return "gate=true, mixed validation + production whitelist";

        if (hasValidationIds)
            return "gate=true, validation factory whitelist";

        return "gate=true, production whitelist inferred";
    }

    private static T GetPrivateFieldValue<T>(object target, string fieldName)
    {
        if (target == null || string.IsNullOrEmpty(fieldName))
            return default;

        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            return default;

        object value = field.GetValue(target);
        return value is T typedValue ? typedValue : default;
    }

    private static string ReadProviderRootPath(IBuffDefinitionProvider provider)
    {
        if (provider == null)
            return "null";

        string rootPath = GetPrivateFieldValue<string>(provider, "BUFF_CONFIG_DATA_ROOT_PATH");
        return string.IsNullOrEmpty(rootPath) ? "unavailable" : rootPath;
    }

    private static int ReadProviderDefinitionCount(IBuffDefinitionProvider provider)
    {
        if (provider == null)
            return 0;

        PropertyInfo property = provider.GetType().GetProperty("DefinitionCount", BindingFlags.Instance | BindingFlags.Public);
        if (property != null && property.GetValue(provider) is int count)
            return count;

        object definitions = GetPrivateFieldValue<object>(provider, "_definitions");
        if (definitions is IDictionary dictionary)
            return dictionary.Count;

        object registry = GetPrivateFieldValue<object>(provider, "_definitionRegistry");
        object registryDefinitions = GetPrivateFieldValue<object>(registry, "_definitions");
        return registryDefinitions is IDictionary registryDictionary ? registryDictionary.Count : 0;
    }

    private static string ReadProviderLoadedConfigIds(IBuffDefinitionProvider provider)
    {
        if (provider == null)
            return "null";

        object indexToBuffId = GetPrivateFieldValue<object>(provider, "_indexToBuffId");
        if (indexToBuffId is IEnumerable ids)
            return FormatEnumerableIds(ids);

        object definitions = GetPrivateFieldValue<object>(provider, "_definitions");
        if (definitions is IDictionary dictionary)
            return FormatEnumerableIds(dictionary.Keys);

        object registry = GetPrivateFieldValue<object>(provider, "_definitionRegistry");
        object registryDefinitions = GetPrivateFieldValue<object>(registry, "_definitions");
        return registryDefinitions is IDictionary registryDictionary ? FormatEnumerableIds(registryDictionary.Keys) : "unavailable";
    }

    private static string FormatIntSet(HashSet<int> ids)
    {
        return ids == null ? "null" : FormatEnumerableIds(ids);
    }

    private static string FormatEnumerableIds(IEnumerable ids)
    {
        if (ids == null)
            return "null";

        StringBuilder builder = new StringBuilder();
        foreach (object id in ids)
        {
            if (builder.Length > 0)
                builder.Append(", ");

            builder.Append(id);
        }

        return builder.Length > 0 ? builder.ToString() : "empty";
    }

    private bool HasRecentBuffDebugTickAfterQueuedCommand()
    {
        bool sawTick = false;
        for (int i = 0; i < _buffDebugLogs.Count; i++)
        {
            string log = _buffDebugLogs[i];
            if (string.IsNullOrEmpty(log))
                continue;

            if (log.Contains("Runner 已推进固定帧"))
                sawTick = true;

            if (sawTick && log.Contains("已入队"))
                return true;
        }

        return false;
    }

    private static string EvaluateBuffDebugSnapshot(BuffDebugSnapshot snapshot, int expectedStack, out string expected, out string actual)
    {
        actual = BuildBuffDebugActualSummary(snapshot);

        if (!snapshot.targetAlive)
        {
            expected = "target Entity 存活";
            return "FAIL";
        }

        if (!snapshot.found)
        {
            if (snapshot.configCompressedRuntimeCount == 1 && snapshot.configEntityPerStackRuntimeCount == 0)
            {
                expected = "压缩 Runtime 已创建，ViewData 需下一帧 Capture 后可见，请再 Tick 一帧";
                return "NOT_READY";
            }

            if (snapshot.configCompressedRuntimeCount == 0 && snapshot.configEntityPerStackRuntimeCount > 0)
            {
                expected = "当前 ConfigId 应走 CompressedRuntime，不应 fallback 到 EntityPerStack";
                return "FAIL";
            }

            expected = "不可见时没有当前 ConfigId runtime";
            return snapshot.configCompressedRuntimeCount == 0
                && snapshot.configEntityPerStackRuntimeCount == 0
                && snapshot.matchingViewCount == 0
                ? "NOT_READY"
                : "FAIL";
        }

        expected = expectedStack > 0
            ? $"一个 aggregate ViewData + Stack={expectedStack} + CompressedRuntime=1 + EntityPerStack=0"
            : "一个 aggregate ViewData + CompressedRuntime=1 + EntityPerStack=0";
        return snapshot.matchingViewCount == 1
            && snapshot.configCompressedRuntimeCount == 1
            && snapshot.configEntityPerStackRuntimeCount == 0
            && (expectedStack <= 0 || snapshot.view.Stack == expectedStack)
            ? "PASS"
            : "FAIL";
    }

    private void AppendBuffDebugInfoLog(string action, string message)
    {
        int frame = GetBuffDebugFrameNumber();
        _buffDebugLogs.Insert(0, $"[F{frame}] {action}: INFO {message}");

        while (_buffDebugLogs.Count > 10)
            _buffDebugLogs.RemoveAt(_buffDebugLogs.Count - 1);
    }

    private void AppendBuffDebugStatusLog(string action, string status, string expected, string actual)
    {
        int frame = GetBuffDebugFrameNumber();
        _buffDebugLogs.Insert(0, $"[F{frame}] {action}: {status} expected={expected} actual={actual}");

        while (_buffDebugLogs.Count > 10)
            _buffDebugLogs.RemoveAt(_buffDebugLogs.Count - 1);
    }

    private void AppendBuffDebugLog(string action, bool pass, string expected, string actual)
    {
        int frame = GetBuffDebugFrameNumber();
        string status = pass ? "PASS" : "FAIL";
        _buffDebugLogs.Insert(0, $"[F{frame}] {action}: {status} expected={expected} actual={actual}");

        while (_buffDebugLogs.Count > 10)
            _buffDebugLogs.RemoveAt(_buffDebugLogs.Count - 1);
    }

    private static string BuildBuffDebugActualSummary(BuffDebugSnapshot snapshot)
    {
        return $"found={snapshot.found}, stack={(snapshot.found ? snapshot.view.Stack : 0)}, compressed={snapshot.configCompressedRuntimeCount}, entityPerStack={snapshot.configEntityPerStackRuntimeCount}, views={snapshot.matchingViewCount}";
    }

    private int GetBuffDebugFrameNumber()
    {
        return _buffDebugLogRunner != null ? _buffDebugLogRunner.CurrentFrameNumber : -1;
    }

    private void GenerateBuffDebugCopyText()
    {
        _buffDebugCopyText = BuildBuffDebugCopyText();
        AppendBuffDebugInfoLog("生成当前快照日志", "已生成可复制调试日志。");
    }

    private void CopyBuffDebugLogToClipboard()
    {
        if (string.IsNullOrEmpty(_buffDebugCopyText))
            _buffDebugCopyText = BuildBuffDebugCopyText();

        EditorGUIUtility.systemCopyBuffer = _buffDebugCopyText;
        AppendBuffDebugInfoLog("复制日志到剪贴板", "已复制调试日志到剪贴板。");
    }

    private void CopyBuffDebugBindingLogToClipboard(BuffDebugBinding binding)
    {
        string text = BuildBuffDebugBindingCopyText(binding);
        EditorGUIUtility.systemCopyBuffer = text;
        AppendBuffDebugInfoLog("复制绑定诊断日志", "已复制绑定诊断日志到剪贴板。");
    }

    private string BuildBuffDebugCopyText()
    {
        StringBuilder builder = new StringBuilder(2048);
        int frame = GetBuffDebugFrameNumber();

        builder.AppendLine("========== BuffSystem Debug Snapshot ==========");
        builder.AppendLine($"Frame: {frame}");
        builder.AppendLine($"ConfigId: {_buffDebugSnapshot.configId}");
        builder.AppendLine($"Target: {FormatEntity(_buffDebugSnapshot.target)}");
        builder.AppendLine($"Source: {FormatEntity(_buffDebugSnapshot.source)}");
        builder.AppendLine();

        builder.AppendLine("[Binding]");
        AppendBuffDebugBindingText(builder, _buffDebugBinding);
        builder.AppendLine();

        AppendBuffDebugPreflightText(builder, _buffDebugPreflight, _hasBuffDebugPreflight);
        builder.AppendLine();

        AppendBuffCommandQueueDiagnosticsText(builder);
        builder.AppendLine();

        AppendBuffCompressedRuntimeTraceText(builder);
        builder.AppendLine();

        builder.AppendLine("[Runtime]");
        builder.AppendLine($"CompressedRuntime total: {_buffDebugSnapshot.compressedRuntimeCount}");
        builder.AppendLine($"Current ConfigId CompressedRuntime count: {_buffDebugSnapshot.configCompressedRuntimeCount}");
        builder.AppendLine($"EntityPerStackRuntime total: {_buffDebugSnapshot.entityPerStackRuntimeCount}");
        builder.AppendLine($"Current ConfigId EntityPerStackRuntime count: {_buffDebugSnapshot.configEntityPerStackRuntimeCount}");
        builder.AppendLine($"Compressed Path: {GetBuffDebugCompressedPathStatus(_buffDebugSnapshot)}");
        builder.AppendLine();

        builder.AppendLine("[ViewData]");
        builder.AppendLine($"TryGetBuff found: {_buffDebugSnapshot.found}");
        builder.AppendLine($"Stack: {(_buffDebugSnapshot.found ? _buffDebugSnapshot.view.Stack : 0)}");
        builder.AppendLine($"RemainingFrames: {(_buffDebugSnapshot.found ? _buffDebugSnapshot.view.RemainingFrames : 0)}");
        builder.AppendLine($"RuntimeHandle: {(_buffDebugSnapshot.found ? _buffDebugSnapshot.view.RuntimeHandle : 0)}");
        builder.AppendLine($"GetBuffs count: {_buffDebugSnapshot.getBuffsCount}");
        builder.AppendLine($"Current ConfigId View count: {_buffDebugSnapshot.matchingViewCount}");
        builder.AppendLine($"ViewData State: {GetBuffDebugViewDataState(_buffDebugSnapshot)}");
        builder.AppendLine();

        builder.AppendLine("[GetBuffs]");
        if (_buffDebugViews.Count == 0)
        {
            builder.AppendLine("* empty");
        }
        else
        {
            for (int i = 0; i < _buffDebugViews.Count; i++)
            {
                BuffViewData view = _buffDebugViews[i];
                builder.AppendLine($"* ConfigId={view.ConfigId}, Stack={view.Stack}, RemainingFrames={view.RemainingFrames}, RuntimeHandle={view.RuntimeHandle}, Target={FormatEntity(view.Target)}, Source={FormatEntity(view.Source)}");
            }
        }
        builder.AppendLine();

        builder.AppendLine("[Last Operations]");
        if (_buffDebugLogs.Count == 0)
        {
            builder.AppendLine("* empty");
        }
        else
        {
            for (int i = 0; i < _buffDebugLogs.Count; i++)
                builder.AppendLine($"* {_buffDebugLogs[i]}");
        }
        builder.AppendLine();

        builder.AppendLine("[Diagnosis]");
        builder.AppendLine(BuildBuffDebugDiagnosis(_buffDebugSnapshot));
        return builder.ToString();
    }

    private string BuildBuffDebugBindingCopyText(BuffDebugBinding binding)
    {
        StringBuilder builder = new StringBuilder(1024);
        builder.AppendLine("========== BuffSystem Binding Diagnostics ==========");
        AppendBuffDebugBindingText(builder, binding);
        return builder.ToString();
    }

    private void GenerateBuffDebugDefinitionCopyText(BuffDebugBinding binding)
    {
        RefreshBuffDebugPreflight(binding, "生成 Definition 诊断日志", false);
        _buffDebugCopyText = BuildBuffDebugDefinitionCopyText();
        AppendBuffDebugInfoLog("生成 Definition 诊断日志", "已生成 Definition / Command / Eligibility 诊断日志。");
    }

    private void CopyBuffDebugDefinitionLogToClipboard(BuffDebugBinding binding)
    {
        RefreshBuffDebugPreflight(binding, "复制 Definition 诊断日志", false);
        string text = BuildBuffDebugDefinitionCopyText();
        _buffDebugCopyText = text;
        EditorGUIUtility.systemCopyBuffer = text;
        AppendBuffDebugInfoLog("复制 Definition 诊断日志", "已复制 Definition / Command / Eligibility 诊断日志到剪贴板。");
    }

    private string BuildBuffDebugDefinitionCopyText()
    {
        StringBuilder builder = new StringBuilder(1536);
        builder.AppendLine("========== BuffSystem Definition Diagnostics ==========");
        AppendBuffDebugPreflightText(builder, _buffDebugPreflight, _hasBuffDebugPreflight);
        return builder.ToString();
    }

    private void CopyBuffCommandQueueDiagnosticsToClipboard()
    {
        string text = BuildBuffCommandQueueDiagnosticsCopyText();
        _buffDebugCopyText = text;
        EditorGUIUtility.systemCopyBuffer = text;
        AppendBuffDebugInfoLog("复制队列诊断日志", "已复制 Command Queue 诊断日志到剪贴板。");
    }

    private string BuildBuffCommandQueueDiagnosticsCopyText()
    {
        StringBuilder builder = new StringBuilder(2048);
        builder.AppendLine("========== BuffSystem Command Queue Diagnostics ==========");
        AppendBuffCommandQueueDiagnosticsText(builder);
        return builder.ToString();
    }

    private void CopyBuffCompressedRuntimeTraceToClipboard()
    {
        string text = BuildBuffCompressedRuntimeTraceCopyText();
        _buffDebugCopyText = text;
        EditorGUIUtility.systemCopyBuffer = text;
        AppendBuffDebugInfoLog("复制 compressed 生命周期日志", "已复制 compressed runtime 生命周期 Trace 到剪贴板。");
    }

    private string BuildBuffCompressedRuntimeTraceCopyText()
    {
        StringBuilder builder = new StringBuilder(4096);
        builder.AppendLine("========== BuffSystem Compressed Runtime Lifecycle Trace ==========");
        AppendBuffCompressedRuntimeTraceText(builder);
        return builder.ToString();
    }

    private void AppendBuffCommandQueueDiagnosticsText(StringBuilder builder)
    {
        builder.AppendLine("[CommandQueues]");
        if (_buffCommandQueueStages.Count == 0)
        {
            builder.AppendLine("* no command queue stages captured");
        }
        else
        {
            for (int i = 0; i < _buffCommandQueueStages.Count; i++)
            {
                BuffCommandQueueStageSnapshot stage = _buffCommandQueueStages[i];
                builder.AppendLine($"Stage: {stage.stage}");
                builder.AppendLine($"Frame: {stage.frame}");
                builder.AppendLine($"AddQueueCount: {stage.addQueueCount}");
                builder.AppendLine($"RemoveQueueCount: {stage.removeQueueCount}");
                builder.AppendLine($"PendingAddCount: {stage.pendingAddCount}");
                builder.AppendLine($"PendingRemoveCount: {stage.pendingRemoveCount}");
                builder.AppendLine($"CommandBufferCount: {stage.commandBufferCount}");
                builder.AppendLine($"RuntimeCompressed: {stage.configCompressedRuntimeCount}");
                builder.AppendLine($"RuntimeEntityPerStack: {stage.configEntityPerStackRuntimeCount}");
                builder.AppendLine($"CompressedRuntimeTotal: {stage.compressedRuntimeCount}");
                builder.AppendLine($"EntityPerStackRuntimeTotal: {stage.entityPerStackRuntimeCount}");
                builder.AppendLine($"TryGetBuffFound: {stage.tryGetBuffFound}");
                builder.AppendLine($"GetBuffsCount: {stage.getBuffsCount}");
                builder.AppendLine($"CurrentConfigViewCount: {stage.matchingViewCount}");
                builder.AppendLine($"Diagnosis: {stage.diagnosis}");
                builder.AppendLine();
            }
        }

        builder.AppendLine("[AllCommandLikeFields]");
        if (_buffCommandQueueFields.Count == 0)
        {
            builder.AppendLine("* no command-like fields captured");
        }
        else
        {
            for (int i = 0; i < _buffCommandQueueFields.Count; i++)
            {
                BuffCommandQueueFieldInfo field = _buffCommandQueueFields[i];
                builder.AppendLine($"* FieldName={field.fieldName}, FieldType={field.fieldType}, Count={field.count}, Readable={field.readable}, ValueSummary={field.valueSummary}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("[ChainDiagnosis]");
        builder.AppendLine(BuildBuffCommandQueueChainDiagnosis());
    }

    private void AppendBuffCompressedRuntimeTraceText(StringBuilder builder)
    {
        builder.AppendLine("[CompressedRuntimeTrace]");
        if (_buffCommandQueueStages.Count == 0)
        {
            builder.AppendLine("* no compressed runtime trace stages captured");
        }
        else
        {
            for (int i = 0; i < _buffCommandQueueStages.Count; i++)
            {
                BuffCommandQueueStageSnapshot stage = _buffCommandQueueStages[i];
                BuffCompressedRuntimeTraceSnapshot trace = stage.compressedTrace;
                builder.AppendLine($"Stage: {stage.stage}");
                builder.AppendLine($"Frame: {stage.frame}");
                builder.AppendLine($"RuntimeExists: {trace.runtimeExists}");
                builder.AppendLine($"RuntimeEntity: {FormatEntity(trace.runtimeEntity)}");
                builder.AppendLine($"RuntimeEntityAlive: {trace.runtimeEntityAlive}");
                builder.AppendLine($"Target: {FormatEntity(trace.target)}");
                builder.AppendLine($"TargetAlive: {trace.targetAlive}");
                builder.AppendLine($"Source: {FormatEntity(trace.source)}");
                builder.AppendLine($"SourceAlive: {trace.sourceAlive}");
                builder.AppendLine($"ConfigId: {trace.configId}");
                builder.AppendLine($"LayerCount: {trace.layerCount}");
                builder.AppendLine($"CompressedRuntimeHandle: {trace.compressedRuntimeHandle}");
                builder.AppendLine($"PendingRemove: {trace.pendingRemove}");
                builder.AppendLine($"CompressedLookupHit: {trace.compressedLookupHit}");
                builder.AppendLine($"LookupEntity: {FormatEntity(trace.lookupEntity)}");
                builder.AppendLine($"ExpireSummary: {trace.expireSummary}");

                if (trace.layers != null)
                {
                    for (int layerIndex = 0; layerIndex < trace.layers.Count; layerIndex++)
                    {
                        BuffCompressedLayerTrace layer = trace.layers[layerIndex];
                        builder.AppendLine($"Layer[{layer.index}]:");
                        builder.AppendLine($"  LayerId: {layer.layerId}");
                        builder.AppendLine($"  LayerRuntimeHandle: {layer.layerRuntimeHandle}");
                        builder.AppendLine($"  ExpireFrame: {layer.expireFrame}");
                        builder.AppendLine($"  RemainingFrames: {layer.remainingFrames}");
                        builder.AppendLine($"  ElapsedFrames: {layer.elapsedFrames}");
                        builder.AppendLine($"  Ticks: {layer.ticks}");
                        builder.AppendLine($"  Expired: {layer.expired}");
                    }
                }

                builder.AppendLine();
            }
        }

        BuffCompressedRuntimeTraceSnapshot latestTrace = _buffCommandQueueStages.Count > 0
            ? _buffCommandQueueStages[_buffCommandQueueStages.Count - 1].compressedTrace
            : default;

        builder.AppendLine("[PendingRemove]");
        builder.AppendLine($"PendingRemoveRuntimeCount: {latestTrace.pendingRemoveRuntimeCount}");
        builder.AppendLine($"PendingRemoveSetCount: {latestTrace.pendingRemoveSetCount}");
        builder.AppendLine($"ContainsCurrentRuntime: {latestTrace.pendingRemove}");
        builder.AppendLine();

        builder.AppendLine("[CompressedLookup]");
        builder.AppendLine($"LookupCount: {latestTrace.compressedLookupCount}");
        builder.AppendLine($"HitCurrentKey: {latestTrace.compressedLookupHit}");
        builder.AppendLine($"LookupEntity: {FormatEntity(latestTrace.lookupEntity)}");
        builder.AppendLine();

        builder.AppendLine("[LifecycleDiagnosis]");
        builder.AppendLine(BuildBuffCompressedRuntimeLifecycleDiagnosis());
    }

    private void AppendBuffDebugBindingText(StringBuilder builder, BuffDebugBinding binding)
    {
        builder.AppendLine($"SnapshotFrame: {GetBuffDebugFrameNumber()}");
        builder.AppendLine($"SelectedSource: {binding.selectedSourceName}");
        builder.AppendLine($"BindingRoot: {binding.rootName}");
        builder.AppendLine($"WorldRef: {FormatReference(binding.world)}");
        builder.AppendLine($"RunnerRef: {FormatReference(binding.runner)}");
        builder.AppendLine($"BuffSystemRef: {FormatReference(binding.buffSystem)}");
        builder.AppendLine($"WorldOwner: {binding.worldOwnerName}");
        builder.AppendLine($"RunnerOwner: {binding.runnerOwnerName}");
        builder.AppendLine($"BuffSystemOwner: {binding.buffSystemOwnerName}");
        builder.AppendLine($"Target: {FormatEntity(_buffDebugTarget)}");
        builder.AppendLine($"Source: {FormatEntity(_buffDebugSource)}");
        builder.AppendLine($"TargetInWorld: {IsEntityAliveInWorld(binding.world, _buffDebugTarget)}");
        builder.AppendLine($"SourceInWorld: {IsEntityAliveInWorld(binding.world, _buffDebugSource)}");
        builder.AppendLine($"AddWorld == RuntimeCountWorld: {binding.addWorldEqualsRuntimeWorld}");
        builder.AppendLine($"QueryBuffSystem == AddBuffSystem: {binding.queryBuffSystemEqualsAddBuffSystem}");
        builder.AppendLine($"TickRunnerWorld == RuntimeCountWorld: {binding.tickRunnerWorldEqualsRuntimeWorld}");
        builder.AppendLine($"BindingState: {(binding.IsUsable ? "PASS" : "FAIL")}");
        builder.AppendLine($"Diagnosis: {binding.diagnosis}");
    }

    private static void AppendBuffDebugPreflightText(StringBuilder builder, BuffDebugPreflight preflight, bool hasPreflight)
    {
        builder.AppendLine("[Definition]");
        if (!hasPreflight)
        {
            builder.AppendLine("State: not captured");
            return;
        }

        builder.AppendLine($"ProviderType: {preflight.providerType}");
        builder.AppendLine($"ProviderSource: {preflight.providerSource}");
        builder.AppendLine($"ProviderRootPath: {preflight.providerRootPath}");
        builder.AppendLine($"LoadedDefinitionCount: {preflight.loadedDefinitionCount}");
        builder.AppendLine($"LoadedConfigIds: {preflight.loadedConfigIds}");
        builder.AppendLine($"TryGetDefinition: {preflight.definitionFound}");

        if (preflight.definitionFound)
        {
            builder.AppendLine($"Definition.ConfigId: {preflight.definition.ConfigId}");
            builder.AppendLine($"Definition.Name: {preflight.definition.Name}");
            builder.AppendLine($"Definition.BuffType: {preflight.definition.BuffType}");
            builder.AppendLine($"Definition.TriggerType: {preflight.definition.TriggerType}");
            builder.AppendLine($"Definition.ParallelStorageMode: {preflight.definition.ParallelStorageMode}");
            builder.AppendLine($"Definition.Unlimited: {preflight.definition.Unlimited}");
            builder.AppendLine($"Definition.MaxStack: {preflight.definition.MaxStack}");
            builder.AppendLine($"Definition.DurationFrames: {preflight.definition.DurationFrames}");
            builder.AppendLine($"Definition.TickIntervalFrames: {preflight.definition.TickIntervalFrames}");
            builder.AppendLine($"Definition.StackUpPolicy: {preflight.definition.ParallelStackUpPolicy}");
            builder.AppendLine($"Definition.StackDownPolicy: {preflight.definition.ParallelStackDownPolicy}");
            builder.AppendLine($"Definition.EffectId: {preflight.definition.EffectId}");
        }

        builder.AppendLine();
        builder.AppendLine("[Eligibility]");
        builder.AppendLine($"BuffTypeParallel: {preflight.buffTypeParallel}");
        builder.AppendLine($"StorageCompressed: {preflight.storageCompressed}");
        builder.AppendLine($"TriggerTick: {preflight.triggerTick}");
        builder.AppendLine($"UnlimitedFalse: {preflight.unlimitedFalse}");
        builder.AppendLine($"MaxStackWithinCapacity: {preflight.maxStackWithinCapacity}");
        builder.AppendLine($"Eligibility: {preflight.eligibility}");
        builder.AppendLine($"CompressedGate: {preflight.compressedGate}");
        builder.AppendLine($"WhitelistHit: {preflight.whitelistHit}");
        builder.AppendLine($"WhitelistConfigIds: {preflight.whitelistConfigIds}");
        builder.AppendLine($"ShouldUseCompressedParallelExpected: {preflight.shouldUseCompressedExpected}");
        builder.AppendLine($"CoreModeHint: {preflight.coreModeHint}");

        builder.AppendLine();
        builder.AppendLine("[Command]");
        builder.AppendLine($"ConfigId: {preflight.configId}");
        builder.AppendLine($"Stack: {preflight.stack}");
        builder.AppendLine($"Target: {FormatEntity(preflight.target)}");
        builder.AppendLine($"Source: {FormatEntity(preflight.source)}");
        builder.AppendLine($"CommandIsValid: {preflight.commandIsValid}");
        builder.AppendLine($"TargetAlive: {preflight.targetAlive}");
        builder.AppendLine($"SourceAlive: {preflight.sourceAlive}");

        builder.AppendLine();
        builder.AppendLine("[Effect]");
        builder.AppendLine($"EffectRegistryExists: {preflight.effectRegistryExists}");
        builder.AppendLine($"EffectId: {preflight.effectId}");
        builder.AppendLine($"EffectRegistered: {preflight.effectRegistered}");

        builder.AppendLine();
        builder.AppendLine("[Diagnosis]");
        builder.AppendLine(BuildBuffDebugPreflightDiagnosis(preflight));
    }

    /// <summary>获取命令调试源的显示名称。</summary>
    private static string GetCommandDebugSourceLabel(IECSFrameCommandDebugSource commandSource)
    {
        if (commandSource is IECSRuntimeDebugSource runtimeSource)
            return CreateSourceLabel(runtimeSource);

        return commandSource != null ? commandSource.GetType().Name : "None";
    }

    private IECSRuntimeDebugSource GetCurrentSource()
    {
        if (_selectedSourceIndex < 0 || _selectedSourceIndex >= _sources.Count)
            return null;

        IECSRuntimeDebugSource source = _sources[_selectedSourceIndex];
        UnityEngine.Object sourceObject = source as UnityEngine.Object;
        if (sourceObject == null)
            return null;

        return source;
    }

    /// <summary>获取当前选中调试源绑定的 World。</summary>
    private World GetCurrentWorld()
    {
        IECSRuntimeDebugSource source = GetCurrentSource();
        return source != null ? source.DebugWorld : null;
    }

    /// <summary>查找指定调试源对象当前所在下标，用于自动扫描后保留选择。</summary>
    private int IndexOfSourceObject(UnityEngine.Object sourceObject)
    {
        if (sourceObject == null)
            return -1;

        for (int i = 0; i < _sources.Count; i++)
        {
            if (ReferenceEquals(_sources[i] as UnityEngine.Object, sourceObject))
                return i;
        }

        return -1;
    }

    /// <summary>生成调试源显示名称。</summary>
    private static string CreateSourceLabel(IECSRuntimeDebugSource source)
    {
        if (source == null)
            return "Missing Source";

        string sourceName = string.IsNullOrEmpty(source.DebugSourceName) ? "Unnamed" : source.DebugSourceName;
        string typeName = source.GetType().Name;
        World world = source.DebugWorld;
        string state = world != null ? world.CurrentState.ToString() : "Unbound";
        return $"{sourceName} ({typeName}, {state})";
    }

    /// <summary>读取实体组件数量。</summary>
    private static int GetComponentCount(World world, Entity entity)
    {
        if (world == null)
            return 0;

        if (!world.TryGetEntityDebugInfo(entity, out EntityDebugInfo info))
            return 0;

        return info.componentCount;
    }

    /// <summary>判断命令执行记录是否符合当前搜索条件。</summary>
    private bool CommandRecordMatches(CommandDebugRecord record)
    {
        if (_showOnlyFailedCommands && record.status != CommandExecuteStatus.Failed)
            return false;

        if (string.IsNullOrEmpty(_commandSearch))
            return true;

        return TextMatches(record.commandTypeName, _commandSearch)
            || TextMatches(record.summary, _commandSearch)
            || TextMatches(record.message, _commandSearch)
            || TextMatches(record.targetEntity.ToString(), _commandSearch)
            || TextMatches(record.timing.ToString(), _commandSearch)
            || TextMatches(record.status.ToString(), _commandSearch)
            || record.frameNumber.ToString().Contains(_commandSearch);
    }

    /// <summary>判断帧命令记录是否符合当前搜索条件。</summary>
    private bool FrameCommandHistoryRecordMatches(FrameCommandHistoryRecord record)
    {
        if (_showOnlyFailedCommands)
            return false;

        if (string.IsNullOrEmpty(_commandSearch))
            return true;

        return TextMatches(record.commandTypeName, _commandSearch)
            || TextMatches(record.summary, _commandSearch)
            || TextMatches(record.targetEntity.ToString(), _commandSearch)
            || TextMatches(record.timing.ToString(), _commandSearch)
            || record.frameNumber.ToString().Contains(_commandSearch);
    }

    /// <summary>文本搜索匹配。</summary>
    private static bool TextMatches(string text, string search)
    {
        if (string.IsNullOrEmpty(search))
            return true;

        if (string.IsNullOrEmpty(text))
            return false;

        return text.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void DrawReadOnlyText(string label, string value)
    {
        EditorGUILayout.LabelField(label, value ?? string.Empty);
    }

    private static void DrawReadOnlyInt(string label, int value)
    {
        EditorGUILayout.LabelField(label, value.ToString());
    }

    private static void DrawReadOnlyFloat(string label, float value)
    {
        EditorGUILayout.LabelField(label, value.ToString("F4"));
    }

    private static void DrawReadOnlySeconds(string label, float value)
    {
        EditorGUILayout.LabelField(label, FormatSeconds(value));
    }

    private static string FormatMilliseconds(double value)
    {
        return $"{value:F4} ms";
    }

    private static string FormatSeconds(float value)
    {
        return $"{value:F3} s";
    }

    private static string FormatTicks(int value)
    {
        return $"{value} ticks";
    }

    private static string FormatEntity(Entity entity)
    {
        return entity.IsValid ? $"{entity.ID}/{entity.Version}" : "Invalid";
    }

    private static bool IsEntityAliveInWorld(World world, Entity entity)
    {
        return world != null && entity.IsValid && world.IsAlive(entity);
    }

    private static string FormatReference(object value)
    {
        return value != null ? $"{value.GetType().Name}@{value.GetHashCode()}" : "null";
    }

    private static string ShortTypeName(Type type)
    {
        return type != null ? type.Name : "None";
    }

    private struct BuffDebugBinding
    {
        public string selectedSourceName;
        public string rootName;
        public string worldOwnerName;
        public string runnerOwnerName;
        public string buffSystemOwnerName;
        public MonoBehaviour rootObject;
        public World world;
        public SimulateRunner runner;
        public BuffSystemCore buffSystem;
        public bool addWorldEqualsRuntimeWorld;
        public bool queryBuffSystemEqualsAddBuffSystem;
        public bool tickRunnerWorldEqualsRuntimeWorld;
        public string diagnosis;

        public bool IsUsable => world != null
            && runner != null
            && buffSystem != null
            && addWorldEqualsRuntimeWorld
            && queryBuffSystemEqualsAddBuffSystem
            && tickRunnerWorldEqualsRuntimeWorld;
    }

    private struct BuffDebugSnapshot
    {
        public int configId;
        public Entity target;
        public Entity source;
        public bool targetAlive;
        public bool sourceAlive;
        public bool found;
        public BuffViewData view;
        public int getBuffsCount;
        public int matchingViewCount;
        public int entityPerStackRuntimeCount;
        public int compressedRuntimeCount;
        public int configEntityPerStackRuntimeCount;
        public int configCompressedRuntimeCount;
    }

    private struct BuffDebugPreflight
    {
        public int configId;
        public int stack;
        public Entity target;
        public Entity source;
        public bool targetAlive;
        public bool sourceAlive;
        public string providerType;
        public string providerSource;
        public string providerRootPath;
        public int loadedDefinitionCount;
        public string loadedConfigIds;
        public bool definitionFound;
        public BuffDefinition definition;
        public bool buffTypeParallel;
        public bool storageCompressed;
        public bool triggerTick;
        public bool unlimitedFalse;
        public bool maxStackWithinCapacity;
        public bool eligibility;
        public bool compressedGate;
        public bool whitelistHit;
        public string whitelistConfigIds;
        public bool shouldUseCompressedExpected;
        public string coreModeHint;
        public bool commandIsValid;
        public bool effectRegistryExists;
        public int effectId;
        public bool effectRegistered;
    }

    private struct BuffCommandQueueFieldInfo
    {
        public string fieldName;
        public string fieldType;
        public int count;
        public bool readable;
        public string valueSummary;
    }

    private struct BuffCommandQueueStageSnapshot
    {
        public string stage;
        public int frame;
        public int addQueueCount;
        public int removeQueueCount;
        public int pendingAddCount;
        public int pendingRemoveCount;
        public int commandBufferCount;
        public int compressedRuntimeCount;
        public int configCompressedRuntimeCount;
        public int entityPerStackRuntimeCount;
        public int configEntityPerStackRuntimeCount;
        public bool tryGetBuffFound;
        public int getBuffsCount;
        public int matchingViewCount;
        public string diagnosis;
        public BuffCompressedRuntimeTraceSnapshot compressedTrace;
    }

    private struct BuffCompressedRuntimeTraceSnapshot
    {
        public string stage;
        public int frame;
        public bool runtimeExists;
        public Entity runtimeEntity;
        public bool runtimeEntityAlive;
        public Entity target;
        public bool targetAlive;
        public Entity source;
        public bool sourceAlive;
        public int configId;
        public int layerCount;
        public int compressedRuntimeHandle;
        public bool pendingRemove;
        public int pendingRemoveRuntimeCount;
        public int pendingRemoveSetCount;
        public bool compressedLookupHit;
        public int compressedLookupCount;
        public Entity lookupEntity;
        public int minExpireFrame;
        public int expiredLayerCount;
        public string expireSummary;
        public List<BuffCompressedLayerTrace> layers;
    }

    private struct BuffCompressedLayerTrace
    {
        public int index;
        public int layerId;
        public int layerRuntimeHandle;
        public int expireFrame;
        public int remainingFrames;
        public int elapsedFrames;
        public int ticks;
        public bool expired;
    }

    private readonly struct DebugLine
    {
        public readonly string label;
        public readonly string value;

        public DebugLine(string label, string value)
        {
            this.label = label;
            this.value = value;
        }
    }
}
}
#endif
