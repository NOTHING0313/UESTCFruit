using BuffSystem;
using Contracts;
using ECSFrameWork;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace View
{
    public class LogicFrameDebugPanel : MonoBehaviour
    {
        private const int PilotConfigId = 991001;

        [Title("BuffSystem 压缩 Buff 调试面板")]
        [FoldoutGroup("运行状态")]
        [LabelText("帧文本")]
        [SerializeField] private Text _frameText;

        [FoldoutGroup("运行状态")]
        [LabelText("Entity 数量文本")]
        [SerializeField] private Text _entityCountText;

        [FoldoutGroup("运行状态")]
        [LabelText("校验值文本")]
        [SerializeField] private Text _checksumText;

        [FoldoutGroup("运行状态")]
        [LabelText("模式文本")]
        [SerializeField] private Text _modeText;

        [FoldoutGroup("运行状态")]
        [LabelText("显示 IMGUI 备用面板")]
        [SerializeField] private bool _showBuffDebug = true;

        private readonly List<string> _buffDebugLogs = new List<string>();
        private readonly List<BuffDebugViewRow> _buffViewRows = new List<BuffDebugViewRow>();
        private IDebugProbe _probe;

        [FoldoutGroup("Buff 操作")]
        [LabelText("ConfigId")]
        [InfoBox("默认 991001，用于验证 Debug_CompressedParallel_TickSmoke 是否走 CompressedExpiryFrameList。")]
        [ShowInInspector]
        private string _configIdText = PilotConfigId.ToString();

        [FoldoutGroup("Buff 操作")]
        [LabelText("Stack")]
        [ShowInInspector]
        private string _stackText = "1";

        [FoldoutGroup("Buff 操作")]
        [LabelText("Tick 帧数")]
        [ShowInInspector]
        private string _tickFramesText = "1";

        [FoldoutGroup("调试实体")]
        [LabelText("Target Entity ID")]
        [ShowInInspector]
        private string _targetIdText = "";

        [FoldoutGroup("调试实体")]
        [LabelText("Target Entity Version")]
        [ShowInInspector]
        private string _targetVersionText = "";

        [FoldoutGroup("调试实体")]
        [LabelText("Source Entity ID")]
        [ShowInInspector]
        private string _sourceIdText = "";

        [FoldoutGroup("调试实体")]
        [LabelText("Source Entity Version")]
        [ShowInInspector]
        private string _sourceVersionText = "";

        private BuffDebugSnapshot _lastBuffSnapshot;
        private bool _hasBuffSnapshot;

        [FoldoutGroup("运行状态")]
        [LabelText("当前逻辑帧")]
        [ReadOnly]
        [ShowInInspector]
        private int CurrentFrame => _probe?.CurrentFrame ?? 0;

        [FoldoutGroup("运行状态")]
        [LabelText("当前 Entity 数量")]
        [ReadOnly]
        [ShowInInspector]
        private int EntityCount => _probe?.EntityCount ?? 0;

        [FoldoutGroup("运行状态")]
        [LabelText("Rollback 状态")]
        [ReadOnly]
        [ShowInInspector]
        private bool IsRollbacking => _probe?.IsRollbacking ?? false;

        [FoldoutGroup("查询结果")]
        [LabelText("是否找到")]
        [ReadOnly]
        [ShowInInspector]
        private bool Found => _hasBuffSnapshot && _lastBuffSnapshot.Found;

        [FoldoutGroup("查询结果")]
        [LabelText("ConfigId")]
        [ReadOnly]
        [ShowInInspector]
        private int ViewConfigId => Found ? _lastBuffSnapshot.View.ConfigId : 0;

        [FoldoutGroup("查询结果")]
        [LabelText("Stack")]
        [ReadOnly]
        [ShowInInspector]
        private int ViewStack => Found ? _lastBuffSnapshot.View.Stack : 0;

        [FoldoutGroup("查询结果")]
        [LabelText("RemainingFrames")]
        [ReadOnly]
        [ShowInInspector]
        private int ViewRemainingFrames => Found ? _lastBuffSnapshot.View.RemainingFrames : 0;

        [FoldoutGroup("查询结果")]
        [LabelText("RuntimeHandle")]
        [ReadOnly]
        [ShowInInspector]
        private int ViewRuntimeHandle => Found ? _lastBuffSnapshot.View.RuntimeHandle : 0;

        [FoldoutGroup("查询结果")]
        [LabelText("GetBuffs(target) 列表")]
        [TableList(IsReadOnly = true)]
        [ReadOnly]
        [ShowInInspector]
        private List<BuffDebugViewRow> GetBuffsTargetList => _buffViewRows;

        [FoldoutGroup("Runtime 类型统计")]
        [LabelText("CompressedRuntime total")]
        [ReadOnly]
        [ShowInInspector]
        private int CompressedRuntimeTotal => _hasBuffSnapshot ? _lastBuffSnapshot.CompressedRuntimeCount : 0;

        [FoldoutGroup("Runtime 类型统计")]
        [LabelText("当前 ConfigId CompressedRuntime count")]
        [ReadOnly]
        [ShowInInspector]
        private int CurrentConfigCompressedRuntimeCount => _hasBuffSnapshot ? _lastBuffSnapshot.ConfigCompressedRuntimeCount : 0;

        [FoldoutGroup("Runtime 类型统计")]
        [LabelText("EntityPerStack total")]
        [ReadOnly]
        [ShowInInspector]
        private int EntityPerStackRuntimeTotal => _hasBuffSnapshot ? _lastBuffSnapshot.EntityPerStackRuntimeCount : 0;

        [FoldoutGroup("Runtime 类型统计")]
        [LabelText("当前 ConfigId EntityPerStack count")]
        [ReadOnly]
        [ShowInInspector]
        private int CurrentConfigEntityPerStackRuntimeCount => _hasBuffSnapshot ? _lastBuffSnapshot.ConfigEntityPerStackRuntimeCount : 0;

        [FoldoutGroup("Runtime 类型统计")]
        [LabelText("压缩路径成功")]
        [ReadOnly]
        [ShowInInspector]
        private bool CompressedPathPass => CurrentConfigCompressedRuntimeCount == 1 && CurrentConfigEntityPerStackRuntimeCount == 0;

        [FoldoutGroup("最近操作日志")]
        [LabelText("日志")]
        [ReadOnly]
        [ShowInInspector]
        private List<string> RecentLogs => _buffDebugLogs;

        [FoldoutGroup("使用说明")]
        [LabelText("说明")]
        [ReadOnly]
        [MultiLineProperty(8)]
        [ShowInInspector]
        private string Usage =>
            "1. Entity 由当前 World.CreateEntity() 创建，不是 Unity GameObject。\n"
            + "2. Source 默认等于 Target，Add / Remove / Query 使用同一组 Entity。\n"
            + "3. Add / Remove 只是入队，需要 Tick 一帧后才会被 BuffSystem 消费。\n"
            + "4. 991001 成功走 compressed 的标准：当前 ConfigId CompressedRuntime count == 1，EntityPerStack count == 0。\n"
            + "5. Add 3 层后，TryGetBuff 应找到一个 aggregate ViewData，Stack 应为 3。";

        public void Initialize(IDebugProbe probe)
        {
            _probe = probe;

            if (_probe is SimulationDebugProbe simulationProbe)
            {
                simulationProbe.EnsureDebugEntities();
                SyncEntityFields(simulationProbe);
                RefreshBuffSnapshot("Initialize");
            }
        }

        public void Refresh()
        {
            if (_probe == null)
                return;

            if (_frameText != null)
                _frameText.text = $"Frame: {_probe.CurrentFrame}";

            if (_entityCountText != null)
                _entityCountText.text = $"Entities: {_probe.EntityCount}";

            if (_checksumText != null)
                _checksumText.text = $"Checksum: {_probe.CurrentChecksum:X8}";

            if (_modeText != null)
                _modeText.text = _probe.IsRollbacking ? "ROLLBACK" : "NORMAL";
        }

        [FoldoutGroup("调试实体")]
        [Button("使用 / 创建调试 Entity")]
        private void UseOrCreateDebugEntities()
        {
            if (!(_probe is SimulationDebugProbe probe))
            {
                AppendLog("使用 / 创建调试 Entity", false, "SimulationDebugProbe 已初始化", "当前 Probe 不可用");
                return;
            }

            probe.EnsureDebugEntities();
            SyncEntityFields(probe);
            RefreshBuffSnapshot("使用 / 创建调试 Entity");
        }

        [FoldoutGroup("Buff 操作")]
        [Button("刷新查询结果")]
        private void RefreshQueryFromOdin()
        {
            RefreshBuffSnapshot("刷新查询结果");
        }

        [FoldoutGroup("Buff 操作")]
        [Button("添加 Buff")]
        private void AddBuffFromOdin()
        {
            if (_probe is SimulationDebugProbe probe)
                QueueAdd(probe, ReadStackOrDefault());
        }

        [FoldoutGroup("Buff 操作")]
        [Button("添加 3 层 Buff")]
        private void AddThreeBuffsFromOdin()
        {
            if (_probe is SimulationDebugProbe probe)
                QueueAdd(probe, 3);
        }

        [FoldoutGroup("Buff 操作")]
        [Button("移除 Buff")]
        private void RemoveBuffFromOdin()
        {
            if (_probe is SimulationDebugProbe probe)
                QueueRemove(probe, ReadStackOrDefault());
        }

        [FoldoutGroup("Buff 操作")]
        [Button("Tick 一帧")]
        private void TickOneFrameFromOdin()
        {
            if (_probe is SimulationDebugProbe probe)
                TickFrames(probe, 1);
        }

        [FoldoutGroup("Buff 操作")]
        [Button("Tick 指定帧数")]
        private void TickFramesFromOdin()
        {
            if (_probe is SimulationDebugProbe probe)
                TickFrames(probe, ReadTickFramesOrDefault());
        }

        [FoldoutGroup("最近操作日志")]
        [Button("清空日志")]
        private void ClearLogs()
        {
            _buffDebugLogs.Clear();
        }

        private void OnGUI()
        {
            if (!_showBuffDebug || !Application.isPlaying)
                return;

            if (!(_probe is SimulationDebugProbe simulationProbe))
                return;

            GUILayout.BeginArea(new Rect(12f, 120f, 460f, 620f), "BuffSystem 压缩 Buff 调试面板", GUI.skin.window);
            DrawBuffDebugPanel(simulationProbe);
            GUILayout.EndArea();
        }

        private void DrawBuffDebugPanel(SimulationDebugProbe probe)
        {
            GUILayout.Label($"当前逻辑帧: {probe.CurrentFrame}");

            GUILayout.BeginHorizontal();
            GUILayout.Label("ConfigId", GUILayout.Width(80f));
            _configIdText = GUILayout.TextField(_configIdText, GUILayout.Width(90f));
            GUILayout.Label("Stack", GUILayout.Width(45f));
            _stackText = GUILayout.TextField(_stackText, GUILayout.Width(55f));
            GUILayout.Label("Tick 帧数", GUILayout.Width(65f));
            _tickFramesText = GUILayout.TextField(_tickFramesText, GUILayout.Width(55f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Target", GUILayout.Width(80f));
            _targetIdText = GUILayout.TextField(_targetIdText, GUILayout.Width(65f));
            _targetVersionText = GUILayout.TextField(_targetVersionText, GUILayout.Width(55f));
            GUILayout.Label("Source", GUILayout.Width(55f));
            _sourceIdText = GUILayout.TextField(_sourceIdText, GUILayout.Width(65f));
            _sourceVersionText = GUILayout.TextField(_sourceVersionText, GUILayout.Width(55f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("使用 / 创建调试 Entity"))
            {
                probe.EnsureDebugEntities();
                SyncEntityFields(probe);
                RefreshBuffSnapshot("使用 / 创建调试 Entity");
            }

            if (GUILayout.Button("刷新查询结果"))
                RefreshBuffSnapshot("刷新查询结果");

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("添加 Buff"))
                QueueAdd(probe, ReadStackOrDefault());

            if (GUILayout.Button("添加 3 层 Buff"))
                QueueAdd(probe, 3);

            if (GUILayout.Button("移除 Buff"))
                QueueRemove(probe, ReadStackOrDefault());

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Tick 一帧"))
                TickFrames(probe, 1);

            if (GUILayout.Button("Tick 指定帧数"))
                TickFrames(probe, ReadTickFramesOrDefault());

            GUILayout.EndHorizontal();

            DrawSnapshot();
            DrawLogs();
        }

        private void QueueAdd(SimulationDebugProbe probe, int stack)
        {
            if (!TryReadConfigId(out int configId) || !TryReadEntities(probe, out Entity target, out Entity source))
                return;

            SetLastSnapshot(probe.AddBuff(configId, stack, target, source));
            AppendLog("添加 Buff", true, $"已入队 stack={stack}", BuildActualSummary(_lastBuffSnapshot));
        }

        private void QueueRemove(SimulationDebugProbe probe, int stack)
        {
            if (!TryReadConfigId(out int configId) || !TryReadEntities(probe, out Entity target, out Entity source))
                return;

            SetLastSnapshot(probe.RemoveBuff(configId, stack, target, source));
            AppendLog("移除 Buff", true, $"已入队 stack={stack}", BuildActualSummary(_lastBuffSnapshot));
        }

        private void TickFrames(SimulationDebugProbe probe, int frameCount)
        {
            bool ticked = probe.TickFrames(frameCount);
            RefreshBuffSnapshot($"Tick {frameCount}");
            AppendLog($"Tick {frameCount}", ticked, "Runner 推进固定帧", ticked ? "已推进" : "未推进");
        }

        private void RefreshBuffSnapshot(string action)
        {
            if (!(_probe is SimulationDebugProbe probe) || !TryReadConfigId(out int configId) || !TryReadEntities(probe, out Entity target, out Entity source))
                return;

            SetLastSnapshot(probe.CaptureBuffDebug(configId, target, source));
            bool pass = EvaluateSnapshot(_lastBuffSnapshot, out string expected, out string actual);
            AppendLog(action, pass, expected, actual);
        }

        private void SetLastSnapshot(BuffDebugSnapshot snapshot)
        {
            _lastBuffSnapshot = snapshot;
            _hasBuffSnapshot = true;
            _buffViewRows.Clear();

            if (snapshot.ViewRows == null)
                return;

            for (int i = 0; i < snapshot.ViewRows.Count; i++)
                _buffViewRows.Add(snapshot.ViewRows[i]);
        }

        private bool TryReadConfigId(out int configId)
        {
            if (int.TryParse(_configIdText, out configId) && configId > 0)
                return true;

            AppendLog("读取 ConfigId", false, "正整数 ConfigId", _configIdText);
            return false;
        }

        private bool TryReadEntities(SimulationDebugProbe probe, out Entity target, out Entity source)
        {
            probe.EnsureDebugEntities();
            target = ReadEntity(_targetIdText, _targetVersionText, probe.DebugTarget);
            source = ReadEntity(_sourceIdText, _sourceVersionText, target);

            if (!probe.TrySetDebugTarget(target))
            {
                AppendLog("读取 Target", false, "存活的 target Entity", FormatEntity(target));
                return false;
            }

            if (!probe.TrySetDebugSource(source))
                source = target;

            SyncEntityFields(probe);
            return true;
        }

        private static Entity ReadEntity(string idText, string versionText, Entity fallback)
        {
            if (int.TryParse(idText, out int id) && int.TryParse(versionText, out int version))
                return new Entity(id, version);

            return fallback;
        }

        private int ReadStackOrDefault()
        {
            return int.TryParse(_stackText, out int stack) && stack > 0 ? stack : 1;
        }

        private int ReadTickFramesOrDefault()
        {
            return int.TryParse(_tickFramesText, out int frames) && frames > 0 ? frames : 1;
        }

        private void SyncEntityFields(SimulationDebugProbe probe)
        {
            Entity target = probe.DebugTarget;
            Entity source = probe.DebugSource.IsValid ? probe.DebugSource : target;
            _targetIdText = target.ID.ToString();
            _targetVersionText = target.Version.ToString();
            _sourceIdText = source.ID.ToString();
            _sourceVersionText = source.Version.ToString();
        }

        private bool EvaluateSnapshot(BuffDebugSnapshot snapshot, out string expected, out string actual)
        {
            actual = BuildActualSummary(snapshot);

            if (!snapshot.TargetAlive)
            {
                expected = "target Entity 存活";
                return false;
            }

            if (!snapshot.Found)
            {
                expected = "不可见时没有当前 ConfigId runtime";
                return snapshot.ConfigCompressedRuntimeCount == 0
                    && snapshot.ConfigEntityPerStackRuntimeCount == 0
                    && snapshot.MatchingViewCount == 0;
            }

            expected = "一个 aggregate ViewData + CompressedRuntime=1 + EntityPerStack=0";
            return snapshot.MatchingViewCount == 1
                && snapshot.ConfigCompressedRuntimeCount == 1
                && snapshot.ConfigEntityPerStackRuntimeCount == 0;
        }

        private void DrawSnapshot()
        {
            if (!_hasBuffSnapshot)
                return;

            GUILayout.Space(6f);
            GUILayout.Label("查询结果 / Runtime");
            GUILayout.Label($"是否找到: {_lastBuffSnapshot.Found}");
            GUILayout.Label($"target: {FormatEntity(_lastBuffSnapshot.Target)} source: {FormatEntity(_lastBuffSnapshot.Source)}");

            if (_lastBuffSnapshot.Found)
            {
                BuffViewData view = _lastBuffSnapshot.View;
                GUILayout.Label($"ConfigId: {view.ConfigId} Stack: {view.Stack} RemainingFrames: {view.RemainingFrames} RuntimeHandle: {view.RuntimeHandle}");
                GUILayout.Label($"View target: {FormatEntity(view.Target)} source: {FormatEntity(view.Source)}");
            }

            GUILayout.Label($"GetBuffs 数量: {_lastBuffSnapshot.GetBuffsCount} 当前 ConfigId 匹配数量: {_lastBuffSnapshot.MatchingViewCount}");
            GUILayout.Label($"CompressedRuntime total/config: {_lastBuffSnapshot.CompressedRuntimeCount}/{_lastBuffSnapshot.ConfigCompressedRuntimeCount}");
            GUILayout.Label($"EntityPerStack total/config: {_lastBuffSnapshot.EntityPerStackRuntimeCount}/{_lastBuffSnapshot.ConfigEntityPerStackRuntimeCount}");
        }

        private void DrawLogs()
        {
            GUILayout.Space(6f);
            GUILayout.Label("最近操作日志");

            for (int i = 0; i < _buffDebugLogs.Count; i++)
                GUILayout.Label(_buffDebugLogs[i]);
        }

        private void AppendLog(string action, bool pass, string expected, string actual)
        {
            int frame = _probe?.CurrentFrame ?? 0;
            string status = pass ? "PASS" : "FAIL";
            _buffDebugLogs.Insert(0, $"[F{frame}] {action}: {status} expected={expected} actual={actual}");

            while (_buffDebugLogs.Count > 8)
                _buffDebugLogs.RemoveAt(_buffDebugLogs.Count - 1);
        }

        private static string BuildActualSummary(BuffDebugSnapshot snapshot)
        {
            return $"found={snapshot.Found}, stack={(snapshot.Found ? snapshot.View.Stack : 0)}, compressed={snapshot.ConfigCompressedRuntimeCount}, entityPerStack={snapshot.ConfigEntityPerStackRuntimeCount}, views={snapshot.MatchingViewCount}";
        }

        private static string FormatEntity(Entity entity)
        {
            return entity.IsValid ? $"{entity.ID}/{entity.Version}" : "Invalid";
        }
    }
}
