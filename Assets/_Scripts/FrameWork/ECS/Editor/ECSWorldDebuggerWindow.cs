#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
        Commands = 7
    }

    private const double DefaultRefreshInterval = 0.25d;
    private const double DefaultTargetScanInterval = 1.0d;
    private const int EntityPageSize = 64;
    private const int ArcheTypeEntityPreviewCount = 128;
    private const int CollectionPreviewCount = 8;
    private const int MaxObjectDepth = 2;
    private const float SidebarWidth = 220f;
    private const float EntityListWidth = 380f;

    private static readonly string[] TabNames =
    {
        "Overview",
        "Entities",
        "Systems",
        "ArcheTypes",
        "Component Stores",
        "Singletons",
        "World Events",
        "Commands"
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

    /// <summary>打开 ECS World Debugger 窗口。</summary>
    [MenuItem("Window/ECSFrameWork/World Debugger")]
    public static void Open()
    {
        ECSWorldDebuggerWindow window = GetWindow<ECSWorldDebuggerWindow>("ECS Debugger");
        window.minSize = new Vector2(1040f, 600f);
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
            EditorGUILayout.HelpBox("World Debugger reads runtime data. Enter Play Mode and select a bound debug source to inspect ECS state.", MessageType.Info);

        if (source == null)
        {
            EditorGUILayout.HelpBox("No IECSRuntimeDebugSource found in the current scene. Use TimeSimulator or ECSRuntimeDebugTarget as debug source.", MessageType.Info);
            return;
        }

        if (world == null)
        {
            EditorGUILayout.HelpBox("Selected debug source has no bound World. Make sure the runtime bootstrap has initialized and bound World / SimulateRunner.", MessageType.Warning);
            return;
        }

        if (!_hasData)
        {
            EditorGUILayout.HelpBox("No debug data has been refreshed yet.", MessageType.Info);
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

        EditorGUILayout.LabelField("ECS World Debugger", EditorStyles.boldLabel, GUILayout.Width(160f));

        if (GUILayout.Button("Scan Worlds", GUILayout.Width(100f)))
        {
            RefreshTargets();
            RefreshData();
        }

        _autoScanTargets = GUILayout.Toggle(_autoScanTargets, "Auto Scan", "Button", GUILayout.Width(90f));
        DrawSourcePopup();
        DrawPingButton();

        GUILayout.FlexibleSpace();
        _autoRefresh = GUILayout.Toggle(_autoRefresh, "Auto Refresh", "Button", GUILayout.Width(110f));
        EditorGUILayout.LabelField("Interval", GUILayout.Width(48f));
        _refreshInterval = EditorGUILayout.Slider(_refreshInterval, 0.05f, 2f, GUILayout.Width(120f));
        EditorGUILayout.LabelField(FormatSeconds(_refreshInterval), GUILayout.Width(70f));

        if (GUILayout.Button("Refresh", GUILayout.Width(80f)))
            RefreshData();

        if (GUILayout.Button("Dump", GUILayout.Width(64f)))
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
            EditorGUILayout.Popup("Target", 0, new[] { "None" }, GUILayout.MinWidth(240f));
            return;
        }

        int safeIndex = Mathf.Clamp(_selectedSourceIndex, 0, _sourceLabelArray.Length - 1);
        int nextIndex = EditorGUILayout.Popup("Target", safeIndex, _sourceLabelArray, GUILayout.MinWidth(240f));
        if (nextIndex == _selectedSourceIndex)
            return;

        _selectedSourceIndex = nextIndex;
        _selectedEntity = Entity.Invalid;
        _selectedArcheTypeIndex = -1;
        _entityPageIndex = 0;
        RefreshData();
    }

    /// <summary>绘制定位当前调试源 GameObject 的按钮。</summary>
    private void DrawPingButton()
    {
        IECSRuntimeDebugSource source = GetCurrentSource();
        EditorGUI.BeginDisabledGroup(!(source is UnityEngine.Object));
        if (GUILayout.Button("Ping", GUILayout.Width(58f)))
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

        EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
        DrawReadOnlyText("Source", source != null ? source.DebugSourceName : "None");
        DrawReadOnlyText("State", _snapshot.currentState.ToString());
        DrawReadOnlyInt("Frame", source != null && source.DebugRunner != null ? source.DebugRunner.CurrentFrameNumber : -1);
        DrawReadOnlyInt("Entities", _snapshot.statistics.aliveEntityCount);
        DrawReadOnlyInt("Systems", _snapshot.systemCount);
        DrawReadOnlyInt("Stores", _snapshot.componentStoreCount);
        DrawReadOnlyInt("ArcheTypes", _snapshot.archeTypeCount);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Pages", EditorStyles.boldLabel);
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
        DrawSectionTitle("Overview");
        EditorGUILayout.BeginHorizontal();
        DrawOverviewCard("World", new[]
        {
            new DebugLine("State", _snapshot.currentState.ToString()),
            new DebugLine("Created Entities", _snapshot.statistics.createdEntityCount.ToString()),
            new DebugLine("Alive Entities", _snapshot.statistics.aliveEntityCount.ToString()),
            new DebugLine("Free Entities", _snapshot.statistics.freeEntityCount.ToString()),
            new DebugLine("Entity Capacity", _snapshot.entityCapacity.ToString()),
            new DebugLine("ArcheType Version", _snapshot.statistics.archeTypeVersion.ToString())
        });

        DrawOverviewCard("Registry", new[]
        {
            new DebugLine("Component Types", _snapshot.componentTypeCount.ToString()),
            new DebugLine("Component Stores", _snapshot.componentStoreCount.ToString()),
            new DebugLine("ArcheTypes", _snapshot.archeTypeCount.ToString()),
            new DebugLine("Query Cache", _snapshot.queryCacheCount.ToString())
        });

        DrawOverviewCard("Runtime Buffers", new[]
        {
            new DebugLine("Systems", _snapshot.systemCount.ToString()),
            new DebugLine("Singletons", _snapshot.singletonCount.ToString()),
            new DebugLine("WorldEvent Types", _snapshot.worldEventTypeCount.ToString()),
            new DebugLine("WorldEvents", _snapshot.worldEventCount.ToString()),
            new DebugLine("Structural Changes", _snapshot.pendingStructuralChangeCount.ToString()),
            new DebugLine("System Changes", _snapshot.pendingSystemChangeCount.ToString())
        });
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8f);
        DrawRunnerOverview(source != null ? source.DebugRunner : null);
    }

    /// <summary>绘制 Runner 状态总览。</summary>
    private static void DrawRunnerOverview(SimulateRunner runner)
    {
        DrawSectionTitle("Runner");
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        if (runner == null)
        {
            EditorGUILayout.HelpBox("No SimulateRunner is bound to this debug source.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        DrawReadOnlyInt("Frame Count", runner.FrameCount);
        DrawReadOnlyInt("Current Frame", runner.CurrentFrameNumber);
        DrawReadOnlyInt("Next Frame", runner.NextFrameNumber);
        DrawReadOnlyText("Is Ticking", runner.IsTicking.ToString());
        DrawReadOnlySeconds("Tick Length", runner.TickLength);
        DrawReadOnlySeconds("Tick Counter", runner.TickCounter);
        EditorGUILayout.EndVertical();
    }

    /// <summary>绘制实体列表与选中实体详情。</summary>
    private void DrawEntitiesTab(World world)
    {
        DrawSectionTitle($"Entities ({_entities.Count})");
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
        _entitySearch = EditorGUILayout.TextField("Search Entity / Component", _entitySearch);
        if (EditorGUI.EndChangeCheck() || _entitySearch != _lastEntitySearch)
        {
            _entityPageIndex = 0;
            _lastEntitySearch = _entitySearch;
        }

        BuildVisibleEntities(world);
        int totalPage = Mathf.Max(1, Mathf.CeilToInt(_visibleEntities.Count / (float)EntityPageSize));
        _entityPageIndex = Mathf.Clamp(_entityPageIndex, 0, totalPage - 1);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Matched: {_visibleEntities.Count} / {_entities.Count}", GUILayout.Width(160f));
        EditorGUI.BeginDisabledGroup(_entityPageIndex <= 0);
        if (GUILayout.Button("First", GUILayout.Width(55f))) _entityPageIndex = 0;
        if (GUILayout.Button("Prev", GUILayout.Width(55f))) _entityPageIndex--;
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.LabelField($"Page {_entityPageIndex + 1} / {totalPage}", GUILayout.Width(100f));

        EditorGUI.BeginDisabledGroup(_entityPageIndex >= totalPage - 1);
        if (GUILayout.Button("Next", GUILayout.Width(55f))) _entityPageIndex++;
        if (GUILayout.Button("Last", GUILayout.Width(55f))) _entityPageIndex = totalPage - 1;
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
            string label = $"{entity.ID}:{entity.Version}    Components={componentCount}";
            if (GUILayout.Toggle(selected, label, "Button"))
                _selectedEntity = entity;
        }

        if (_visibleEntities.Count == 0)
            EditorGUILayout.HelpBox("No Entity matched current search.", MessageType.Info);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    /// <summary>绘制实体列表表头。</summary>
    private static void DrawEntityListHeader()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Entity", EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>绘制选中 Entity 的详情与组件值。</summary>
    private void DrawSelectedEntityDetail(World world)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
        _entityDetailScroll = EditorGUILayout.BeginScrollView(_entityDetailScroll, GUILayout.MinHeight(360f));

        if (!_selectedEntity.IsValid)
        {
            EditorGUILayout.HelpBox("Select an Entity from the left list.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            return;
        }

        if (!world.TryGetEntityDebugInfo(_selectedEntity, out EntityDebugInfo info))
        {
            EditorGUILayout.HelpBox($"{_selectedEntity} is no longer alive.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            return;
        }

        DrawSectionTitle("Selected Entity");
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        DrawReadOnlyInt("ID", _selectedEntity.ID);
        DrawReadOnlyInt("Version", _selectedEntity.Version);
        DrawReadOnlyText("Alive", info.isAlive.ToString());
        DrawReadOnlyInt("Component Count", info.componentCount);
        DrawSelectableText("Mask", info.componentMask.ToString());
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6f);
        DrawSectionTitle("Components");
        world.FillEntityComponentTypes(_selectedEntity, _componentTypes);
        DrawEntityComponentValueList(world, _selectedEntity, _componentTypes);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    /// <summary>绘制 System 调试页。</summary>
    private void DrawSystemsTab()
    {
        DrawSectionTitle($"Systems ({_systems.Count})");
        _systemSearch = EditorGUILayout.TextField("Search", _systemSearch);
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
        DrawSectionTitle($"ArcheTypes ({_archeTypes.Count})");
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
            string label = $"{i}. Entities={info.entityCount}, Components={info.componentCount}";
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
            EditorGUILayout.HelpBox("Select an ArcheType from the left list.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            return;
        }

        ArcheTypeDebugInfo info = _archeTypes[_selectedArcheTypeIndex];
        DrawSectionTitle("Selected ArcheType");
        DrawReadOnlyInt("Entity Count", info.entityCount);
        DrawReadOnlyInt("Component Count", info.componentCount);
        DrawSelectableText("Mask", info.mask.ToString());

        EditorGUILayout.Space(6f);
        DrawSectionTitle("Component Types");
        world.FillComponentTypesByMask(info.mask, _componentTypes);
        DrawTypeList(_componentTypes);

        EditorGUILayout.Space(6f);
        DrawSectionTitle("Entities");
        world.FillEntitiesByArcheType(info.mask, _archeTypeEntities);
        int count = Mathf.Min(_archeTypeEntities.Count, ArcheTypeEntityPreviewCount);
        for (int i = 0; i < count; i++)
            EditorGUILayout.LabelField(_archeTypeEntities[i].ToString());

        if (_archeTypeEntities.Count > ArcheTypeEntityPreviewCount)
            EditorGUILayout.HelpBox($"Only first {ArcheTypeEntityPreviewCount} entities are shown.", MessageType.Info);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    /// <summary>绘制 ComponentStore 调试页。</summary>
    private void DrawComponentStoresTab()
    {
        DrawSectionTitle($"Component Stores ({_componentStores.Count})");
        _componentStoreSearch = EditorGUILayout.TextField("Search", _componentStoreSearch);
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
        DrawSectionTitle($"Singletons ({_singletons.Count})");
        if (_singletons.Count == 0)
        {
            EditorGUILayout.HelpBox("No SingletonComponent is registered.", MessageType.Info);
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
                DrawReadOnlyText("Alive", info.isAlive.ToString());
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
        DrawSectionTitle($"World Events ({_events.Count})");
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
        DrawSectionTitle("Commands");

        IECSFrameCommandDebugSource commandSource = ResolveCommandDebugSource(source);
        if (commandSource == null)
        {
            EditorGUILayout.HelpBox("No IECSFrameCommandDebugSource is available for the selected World. Bind SimulationFrameCommandBuffer / SimulationFrameCommandApplier through TimeSimulator, ECSRuntimeDebugTarget, or the current debug bootstrap.", MessageType.Info);
            return;
        }

        if (!ReferenceEquals(commandSource, source))
            EditorGUILayout.HelpBox($"Selected source does not expose command data directly. Using related command source: {GetCommandDebugSourceLabel(commandSource)}", MessageType.Info);

        SimulationFrameCommandBuffer commandBuffer = commandSource.DebugFrameCommandBuffer;
        SimulationFrameCommandApplier commandApplier = commandSource.DebugFrameCommandApplier;

        DrawCommandSummary(commandBuffer, commandApplier, source != null ? source.DebugRunner : null);
        DrawCommandToolbar(commandApplier);

        EditorGUILayout.Space(8f);
        DrawCommandExecutionHistory(commandApplier);

        EditorGUILayout.Space(8f);
        DrawFrameCommandHistory(commandBuffer);
    }

    /// <summary>绘制命令调试摘要卡片。</summary>
    private void DrawCommandSummary(SimulationFrameCommandBuffer commandBuffer, SimulationFrameCommandApplier commandApplier, SimulateRunner runner)
    {
        int currentFrame = runner != null ? runner.CurrentFrameNumber : 0;
        int nextFrame = runner != null ? runner.NextFrameNumber : 1;
        int pendingFuture = commandBuffer != null ? commandBuffer.CountCommandsFromFrame(nextFrame) : 0;

        EditorGUILayout.BeginHorizontal();
        DrawOverviewCard("Frame Command Buffer", new[]
        {
            new DebugLine("Buffered Frames", commandBuffer != null ? commandBuffer.FrameCount.ToString() : "0"),
            new DebugLine("Buffered Commands", commandBuffer != null ? commandBuffer.CommandCount.ToString() : "0"),
            new DebugLine("Current Frame", currentFrame.ToString()),
            new DebugLine("Future Commands", pendingFuture.ToString())
        });

        DrawOverviewCard("Command History", new[]
        {
            new DebugLine("History Frames", commandBuffer != null ? commandBuffer.CommandHistoryFrameCount.ToString() : "0"),
            new DebugLine("History Commands", commandBuffer != null ? commandBuffer.CommandHistoryCommandCount.ToString() : "0")
        });

        DrawOverviewCard("Debug Execution", new[]
        {
            new DebugLine("Applied Timings", commandApplier != null ? commandApplier.AppliedFrameCount.ToString() : "0"),
            new DebugLine("Debug Frames", commandApplier != null ? commandApplier.DebugHistoryFrameCount.ToString() : "0"),
            new DebugLine("Debug Records", commandApplier != null ? commandApplier.DebugRecordCount.ToString() : "0")
        });
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>绘制命令搜索与调试历史控制栏。</summary>
    private void DrawCommandToolbar(SimulationFrameCommandApplier commandApplier)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        _commandSearch = EditorGUILayout.TextField("Search", _commandSearch);
        _showOnlyFailedCommands = GUILayout.Toggle(_showOnlyFailedCommands, "Failed Only", "Button", GUILayout.Width(90f));

        EditorGUI.BeginDisabledGroup(commandApplier == null);
        if (GUILayout.Button("Clear Debug History", GUILayout.Width(150f)))
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
        DrawSectionTitle($"DebugCommand Execution History ({_commandDebugFrames.Count} frames)");
        if (commandApplier == null)
        {
            EditorGUILayout.HelpBox("No SimulationFrameCommandApplier is bound to this debug source.", MessageType.Info);
            return;
        }

        if (_commandDebugFrames.Count == 0)
        {
            EditorGUILayout.HelpBox("No command execution record yet. Records appear after SimulationFrameCommandApplier applies commands.", MessageType.Info);
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
        DrawSectionTitle($"Frame Command History ({_frameCommandHistoryFrames.Count} frames)");
        if (commandBuffer == null)
        {
            EditorGUILayout.HelpBox("No SimulationFrameCommandBuffer is bound to this debug source.", MessageType.Info);
            return;
        }

        if (_frameCommandHistoryFrames.Count == 0)
        {
            EditorGUILayout.HelpBox("No frame command history yet. History appears after commands are added to SimulationFrameCommandBuffer.", MessageType.Info);
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
            EditorGUILayout.LabelField("None");
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
            EditorGUILayout.HelpBox("Component value is unavailable.", MessageType.Warning);
            return;
        }

        if (!world.TryGetComponentDebugValue(entity, componentType, out object component))
        {
            EditorGUILayout.HelpBox("Component value cannot be read. It may have been removed after the last refresh.", MessageType.Warning);
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
            EditorGUILayout.LabelField("Empty");
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
        EditorGUILayout.LabelField("Frame", EditorStyles.boldLabel, GUILayout.Width(70f));
        EditorGUILayout.LabelField("Timing", EditorStyles.boldLabel, GUILayout.Width(90f));
        EditorGUILayout.LabelField("Status", EditorStyles.boldLabel, GUILayout.Width(80f));
        EditorGUILayout.LabelField("Mode", EditorStyles.boldLabel, GUILayout.Width(70f));
        EditorGUILayout.LabelField("Command", EditorStyles.boldLabel, GUILayout.MinWidth(170f));
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel, GUILayout.Width(180f));
        EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel, GUILayout.MinWidth(260f));
        EditorGUILayout.LabelField("Message", EditorStyles.boldLabel, GUILayout.MinWidth(180f));
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>绘制帧命令表头。</summary>
    private static void DrawFrameCommandHistoryHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Frame", EditorStyles.boldLabel, GUILayout.Width(70f));
        EditorGUILayout.LabelField("Timing", EditorStyles.boldLabel, GUILayout.Width(90f));
        EditorGUILayout.LabelField("Command", EditorStyles.boldLabel, GUILayout.MinWidth(170f));
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel, GUILayout.Width(180f));
        EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel, GUILayout.MinWidth(360f));
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>绘制系统性能表头。</summary>
    private static void DrawSystemHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Name", EditorStyles.boldLabel, GUILayout.MinWidth(180f));
        EditorGUILayout.LabelField("Sequence", EditorStyles.boldLabel, GUILayout.Width(110f));
        EditorGUILayout.LabelField("Enabled", EditorStyles.boldLabel, GUILayout.Width(70f));
        EditorGUILayout.LabelField("Last", EditorStyles.boldLabel, GUILayout.Width(90f));
        EditorGUILayout.LabelField("Avg", EditorStyles.boldLabel, GUILayout.Width(90f));
        EditorGUILayout.LabelField("Max", EditorStyles.boldLabel, GUILayout.Width(90f));
        EditorGUILayout.LabelField("Ticks", EditorStyles.boldLabel, GUILayout.Width(90f));
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>绘制 ComponentStore 表头。</summary>
    private static void DrawComponentStoreHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Type", EditorStyles.boldLabel, GUILayout.MinWidth(180f));
        EditorGUILayout.LabelField("TypeID", EditorStyles.boldLabel, GUILayout.Width(70f));
        EditorGUILayout.LabelField("Count", EditorStyles.boldLabel, GUILayout.Width(70f));
        EditorGUILayout.LabelField("Capacity", EditorStyles.boldLabel, GUILayout.Width(90f));
        EditorGUILayout.LabelField("Sparse", EditorStyles.boldLabel, GUILayout.Width(110f));
        EditorGUILayout.LabelField("Load", EditorStyles.boldLabel, GUILayout.Width(90f));
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>绘制 WorldEvent 表头。</summary>
    private static void DrawWorldEventHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Type", EditorStyles.boldLabel, GUILayout.MinWidth(180f));
        EditorGUILayout.LabelField("Count", EditorStyles.boldLabel, GUILayout.Width(80f));
        EditorGUILayout.LabelField("Oldest Frame", EditorStyles.boldLabel, GUILayout.Width(110f));
        EditorGUILayout.LabelField("Newest Frame", EditorStyles.boldLabel, GUILayout.Width(110f));
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
            EditorGUILayout.LabelField("None");
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

    private static string ShortTypeName(Type type)
    {
        return type != null ? type.Name : "None";
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
