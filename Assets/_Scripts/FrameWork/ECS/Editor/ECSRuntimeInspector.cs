#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ECSFrameWork
{
/// <summary>
/// TimeSimulator 的 ECS 运行时调试 Inspector。
/// 该 Inspector 只读取 World Debug API，不直接访问 EntityManager / ComponentManager 等内部字段。
/// </summary>
[CustomEditor(typeof(TimeSimulator))]
public sealed class TimeSimulatorRuntimeInspector : Editor
{
    private readonly ECSRuntimeInspectorDrawer _drawer = new ECSRuntimeInspectorDrawer();

    /// <summary>绘制 TimeSimulator 默认字段与 ECS Debug 面板。</summary>
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        _drawer.Draw(target as IECSRuntimeDebugSource);
    }

    /// <summary>自动刷新开启时持续重绘 Inspector。</summary>
    public override bool RequiresConstantRepaint()
    {
        return _drawer.AutoRefresh;
    }
}

/// <summary>
/// ECSRuntimeDebugTarget 的 ECS 运行时调试 Inspector。
/// 适用于项目自定义 Bootstrap 持有 World，而不是直接使用 TimeSimulator 的情况。
/// </summary>
[CustomEditor(typeof(ECSRuntimeDebugTarget))]
public sealed class ECSRuntimeDebugTargetInspector : Editor
{
    private readonly ECSRuntimeInspectorDrawer _drawer = new ECSRuntimeInspectorDrawer();

    /// <summary>绘制 ECSRuntimeDebugTarget 默认字段与 ECS Debug 面板。</summary>
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        _drawer.Draw(target as IECSRuntimeDebugSource);
    }

    /// <summary>自动刷新开启时持续重绘 Inspector。</summary>
    public override bool RequiresConstantRepaint()
    {
        return _drawer.AutoRefresh;
    }
}

/// <summary>
/// ECS Runtime Inspector 的共享绘制器。
/// </summary>
internal sealed class ECSRuntimeInspectorDrawer
{
    private const int MaxEntityPreviewCount = 64;
    private const double AutoRefreshInterval = 0.25d;

    private readonly List<Entity> _entities = new List<Entity>(128);
    private readonly List<Type> _componentTypes = new List<Type>(32);
    private readonly List<ComponentStoreDebugInfo> _componentStores = new List<ComponentStoreDebugInfo>(32);
    private readonly List<ArcheTypeDebugInfo> _archeTypes = new List<ArcheTypeDebugInfo>(32);
    private readonly List<SystemDebugInfo> _systems = new List<SystemDebugInfo>(32);
    private readonly List<SingletonDebugInfo> _singletons = new List<SingletonDebugInfo>(16);
    private readonly List<WorldEventDebugInfo> _events = new List<WorldEventDebugInfo>(16);

    private bool _autoRefresh = true;
    private bool _showOverview = true;
    private bool _showRunner = true;
    private bool _showEntities = false;
    private bool _showComponentStores = false;
    private bool _showArcheTypes = true;
    private bool _showSystems = true;
    private bool _showSingletons = false;
    private bool _showEvents = true;

    private bool _hasSnapshot;
    private double _nextAutoRefreshTime;
    private WorldDebugSnapshot _snapshot;

    /// <summary>是否启用自动刷新。</summary>
    public bool AutoRefresh => _autoRefresh;

    /// <summary>绘制指定 ECS 调试源的只读 Runtime Inspector。</summary>
    public void Draw(IECSRuntimeDebugSource source)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("ECS Runtime Inspector", EditorStyles.boldLabel);

        if (source == null)
        {
            EditorGUILayout.HelpBox("Target does not implement IECSRuntimeDebugSource.", MessageType.Warning);
            return;
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("ECS Runtime Inspector only reads runtime World data in Play Mode.", MessageType.Info);
            return;
        }

        World world = source.DebugWorld;
        if (world == null)
        {
            EditorGUILayout.HelpBox("No World is bound to this runtime debug source. Call InitSimulator(runner) or ECSRuntimeDebugTarget.Bind(world, runner).", MessageType.Warning);
            return;
        }

        DrawToolbar(world, source);
        RefreshIfNeeded(world);

        if (!_hasSnapshot)
        {
            EditorGUILayout.HelpBox("Snapshot has not been refreshed yet.", MessageType.Info);
            return;
        }

        DrawOverview(_snapshot, source);
        DrawRunner(source.DebugRunner);
        DrawSystems(world);
        DrawArcheTypes(world);
        DrawEntities(world);
        DrawComponentStores(world);
        DrawSingletons(world);
        DrawEvents(world);
    }

    /// <summary>绘制刷新、自动刷新和 Dump 按钮。</summary>
    private void DrawToolbar(World world, IECSRuntimeDebugSource source)
    {
        EditorGUILayout.BeginHorizontal();
        _autoRefresh = GUILayout.Toggle(_autoRefresh, "Auto Refresh", "Button", GUILayout.Width(110f));

        if (GUILayout.Button("Refresh", GUILayout.Width(90f)))
            Refresh(world);

        if (GUILayout.Button("Dump Snapshot", GUILayout.Width(120f)))
        {
            Refresh(world);
            Debug.Log($"[ECS Runtime Inspector] {source.DebugSourceName}: {_snapshot}");
        }

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>根据自动刷新间隔更新 Debug 数据。</summary>
    private void RefreshIfNeeded(World world)
    {
        if (!_hasSnapshot)
        {
            Refresh(world);
            return;
        }

        if (!_autoRefresh)
            return;

        double time = EditorApplication.timeSinceStartup;
        if (time < _nextAutoRefreshTime)
            return;

        Refresh(world);
        _nextAutoRefreshTime = time + AutoRefreshInterval;
    }

    /// <summary>刷新所有 Inspector 需要展示的 Debug 数据。</summary>
    private void Refresh(World world)
    {
        if (world == null)
            return;

        _snapshot = world.GetDebugSnapshot();
        _hasSnapshot = true;

        world.FillAliveEntities(_entities);
        world.FillComponentStoreDebugInfos(_componentStores);
        world.FillArcheTypeDebugInfos(_archeTypes);
        world.FillSystemDebugInfos(_systems);
        world.FillSingletonDebugInfos(_singletons);
        world.FillWorldEventDebugInfos(_events);
    }

    /// <summary>绘制 World 总览。</summary>
    private void DrawOverview(WorldDebugSnapshot snapshot, IECSRuntimeDebugSource source)
    {
        _showOverview = EditorGUILayout.Foldout(_showOverview, "Overview", true);
        if (!_showOverview)
            return;

        EditorGUI.indentLevel++;
        DrawReadOnlyText("Source", source.DebugSourceName);
        DrawReadOnlyText("World State", snapshot.currentState.ToString());
        DrawReadOnlyInt("Created Entities", snapshot.statistics.createdEntityCount);
        DrawReadOnlyInt("Alive Entities", snapshot.statistics.aliveEntityCount);
        DrawReadOnlyInt("Free Entities", snapshot.statistics.freeEntityCount);
        DrawReadOnlyInt("Entity Capacity", snapshot.entityCapacity);
        DrawReadOnlyInt("Component Types", snapshot.componentTypeCount);
        DrawReadOnlyInt("Component Stores", snapshot.componentStoreCount);
        DrawReadOnlyInt("ArcheTypes", snapshot.archeTypeCount);
        DrawReadOnlyInt("Query Cache", snapshot.queryCacheCount);
        DrawReadOnlyInt("ArcheType Version", snapshot.statistics.archeTypeVersion);
        DrawReadOnlyInt("Systems", snapshot.systemCount);
        DrawReadOnlyInt("Singletons", snapshot.singletonCount);
        DrawReadOnlyInt("WorldEvent Types", snapshot.worldEventTypeCount);
        DrawReadOnlyInt("WorldEvents", snapshot.worldEventCount);
        DrawReadOnlyInt("Pending Structural Changes", snapshot.pendingStructuralChangeCount);
        DrawReadOnlyInt("Pending System Changes", snapshot.pendingSystemChangeCount);
        EditorGUI.indentLevel--;
    }

    /// <summary>绘制 Runner 状态。</summary>
    private void DrawRunner(SimulateRunner runner)
    {
        _showRunner = EditorGUILayout.Foldout(_showRunner, "Runner", true);
        if (!_showRunner)
            return;

        EditorGUI.indentLevel++;
        if (runner == null)
        {
            EditorGUILayout.HelpBox("No SimulateRunner is bound.", MessageType.Info);
            EditorGUI.indentLevel--;
            return;
        }

        DrawReadOnlyInt("Frame Count", runner.FrameCount);
        DrawReadOnlyInt("Current Frame", runner.CurrentFrameNumber);
        DrawReadOnlyInt("Next Frame", runner.NextFrameNumber);
        DrawReadOnlyText("Is Ticking", runner.IsTicking.ToString());
        DrawReadOnlyFloat("Tick Length", runner.TickLength);
        DrawReadOnlyFloat("Tick Counter", runner.TickCounter);
        EditorGUI.indentLevel--;
    }

    /// <summary>绘制 System Profile 简表。</summary>
    private void DrawSystems(World world)
    {
        _showSystems = EditorGUILayout.Foldout(_showSystems, $"Systems ({_systems.Count})", true);
        if (!_showSystems)
            return;

        EditorGUI.indentLevel++;
        if (_systems.Count == 0)
        {
            EditorGUILayout.HelpBox("No systems registered.", MessageType.Info);
            EditorGUI.indentLevel--;
            return;
        }

        for (int i = 0; i < _systems.Count; i++)
        {
            SystemDebugInfo info = _systems[i];
            EditorGUILayout.LabelField($"{i}. {info.name}", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            DrawReadOnlyText("Type", ShortTypeName(info.systemType));
            DrawReadOnlyText("Sequence", info.sequence.ToString());
            DrawReadOnlyText("Enabled", info.enabled.ToString());
            DrawReadOnlyText("Last Tick", $"{info.lastTickMilliseconds:F4} ms");
            DrawReadOnlyText("Average Tick", $"{info.averageTickMilliseconds:F4} ms");
            DrawReadOnlyText("Max Tick", $"{info.maxTickMilliseconds:F4} ms");
            DrawReadOnlyInt("Tick Count", info.tickCount);
            EditorGUI.indentLevel--;
        }

        EditorGUI.indentLevel--;
    }

    /// <summary>绘制 ArcheType 分组简表。</summary>
    private void DrawArcheTypes(World world)
    {
        _showArcheTypes = EditorGUILayout.Foldout(_showArcheTypes, $"ArcheTypes ({_archeTypes.Count})", true);
        if (!_showArcheTypes)
            return;

        EditorGUI.indentLevel++;
        for (int i = 0; i < _archeTypes.Count; i++)
        {
            ArcheTypeDebugInfo info = _archeTypes[i];
            EditorGUILayout.LabelField($"{i}. Entities={info.entityCount}, Components={info.componentCount}");
            EditorGUI.indentLevel++;
            DrawReadOnlyText("Mask", info.mask.ToString());
            world.FillComponentTypesByMask(info.mask, _componentTypes);
            DrawReadOnlyText("Component Types", JoinTypeNames(_componentTypes));
            EditorGUI.indentLevel--;
        }
        EditorGUI.indentLevel--;
    }

    /// <summary>绘制 Entity 预览列表。</summary>
    private void DrawEntities(World world)
    {
        _showEntities = EditorGUILayout.Foldout(_showEntities, $"Entities ({_entities.Count})", true);
        if (!_showEntities)
            return;

        EditorGUI.indentLevel++;
        int count = Mathf.Min(_entities.Count, MaxEntityPreviewCount);
        for (int i = 0; i < count; i++)
        {
            Entity entity = _entities[i];
            if (!world.TryGetEntityDebugInfo(entity, out EntityDebugInfo info))
            {
                EditorGUILayout.LabelField($"{i}. {entity} <invalid>");
                continue;
            }

            EditorGUILayout.LabelField($"{i}. Entity ID={entity.ID}, Version={entity.Version}, Components={info.componentCount}");
            EditorGUI.indentLevel++;
            world.FillEntityComponentTypes(entity, _componentTypes);
            DrawReadOnlyText("Component Types", JoinTypeNames(_componentTypes));
            EditorGUI.indentLevel--;
        }

        if (_entities.Count > MaxEntityPreviewCount)
            EditorGUILayout.HelpBox($"Only first {MaxEntityPreviewCount} entities are shown to keep Inspector responsive.", MessageType.Info);

        EditorGUI.indentLevel--;
    }

    /// <summary>绘制 ComponentStore 简表。</summary>
    private void DrawComponentStores(World world)
    {
        _showComponentStores = EditorGUILayout.Foldout(_showComponentStores, $"Component Stores ({_componentStores.Count})", true);
        if (!_showComponentStores)
            return;

        EditorGUI.indentLevel++;
        for (int i = 0; i < _componentStores.Count; i++)
        {
            ComponentStoreDebugInfo info = _componentStores[i];
            EditorGUILayout.LabelField($"{i}. {ShortTypeName(info.componentType)}", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            DrawReadOnlyInt("Register ID", info.registerID);
            DrawReadOnlyInt("Count", info.count);
            DrawReadOnlyInt("Capacity", info.capacity);
            DrawReadOnlyInt("Sparse Capacity", info.sparseCapacity);
            EditorGUI.indentLevel--;
        }
        EditorGUI.indentLevel--;
    }

    /// <summary>绘制 SingletonComponent 映射简表。</summary>
    private void DrawSingletons(World world)
    {
        _showSingletons = EditorGUILayout.Foldout(_showSingletons, $"Singletons ({_singletons.Count})", true);
        if (!_showSingletons)
            return;

        EditorGUI.indentLevel++;
        for (int i = 0; i < _singletons.Count; i++)
        {
            SingletonDebugInfo info = _singletons[i];
            EditorGUILayout.LabelField($"{i}. {ShortTypeName(info.componentType)}");
            EditorGUI.indentLevel++;
            DrawReadOnlyText("Entity", info.entity.ToString());
            DrawReadOnlyText("Alive", info.isAlive.ToString());
            EditorGUI.indentLevel--;
        }
        EditorGUI.indentLevel--;
    }

    /// <summary>绘制 WorldEvent 缓冲区简表。</summary>
    private void DrawEvents(World world)
    {
        _showEvents = EditorGUILayout.Foldout(_showEvents, $"World Events ({_events.Count})", true);
        if (!_showEvents)
            return;

        EditorGUI.indentLevel++;
        for (int i = 0; i < _events.Count; i++)
        {
            WorldEventDebugInfo info = _events[i];
            EditorGUILayout.LabelField($"{i}. {ShortTypeName(info.eventType)}");
            EditorGUI.indentLevel++;
            DrawReadOnlyInt("Count", info.eventCount);
            DrawReadOnlyInt("Oldest Frame", info.oldestFrame);
            DrawReadOnlyInt("Newest Frame", info.newestFrame);
            EditorGUI.indentLevel--;
        }
        EditorGUI.indentLevel--;
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

    private static string ShortTypeName(Type type)
    {
        return type != null ? type.Name : "None";
    }

    private static string JoinTypeNames(List<Type> types)
    {
        if (types == null || types.Count == 0)
            return "None";

        string result = string.Empty;
        for (int i = 0; i < types.Count; i++)
        {
            if (i > 0)
                result += ", ";

            result += ShortTypeName(types[i]);
        }

        return result;
    }
}
}
#endif
