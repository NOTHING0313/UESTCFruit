using BuffSystem;
using Contracts;
using ECSFrameWork;
using FrameWork.RollBackSystem;
using Simulation.Contracts;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace View
{
    /// <summary>
    /// View-only text HUD for the midterm demo scene. It reads runtime status only and never drives simulation logic.
    /// </summary>
    public sealed class MidtermDebugHudPresenter : MonoBehaviour
    {
        [Header("Output")]
        [SerializeField] private Text _targetText;

        [Header("Display")]
        [SerializeField] private bool _refreshInLateUpdate = true;
        [SerializeField] private float _refreshIntervalSeconds = 0.1f;

        [Header("Static Claims")]
        [SerializeField] private string _logicOnlyTestStatus = "PASS (NUnit targeted)";
        [SerializeField] private string _viewStatusText = "Midterm visualization smoke";
        [SerializeField] private string _pendingText = "rollback-view consistency, production HUD, ghost/prediction";

        private readonly StringBuilder _builder = new StringBuilder(1024);

        private World _world;
        private SimulateRunner _runner;
        private IBuffSystem _buffSystem;
        private Entity _playerEntity = Entity.Invalid;
        private IEntityViewBinder _binder;
        private Transform _worldViewRoot;
        private GameObject _playerPrefab;
        private RollbackBootstrap _rollbackBootstrap;
        private float _nextRefreshTime;
        private bool _initialized;

        public void Initialize(
            World world,
            SimulateRunner runner,
            IBuffSystem buffSystem,
            Entity playerEntity,
            IEntityViewBinder binder,
            Transform worldViewRoot,
            GameObject playerPrefab,
            RollbackBootstrap rollbackBootstrap = null,
            Text targetText = null)
        {
            _world = world;
            _runner = runner;
            _buffSystem = buffSystem;
            _playerEntity = playerEntity;
            _binder = binder;
            _worldViewRoot = worldViewRoot;
            _playerPrefab = playerPrefab;
            _rollbackBootstrap = rollbackBootstrap;
            _initialized = true;

            if (targetText != null)
                _targetText = targetText;

            ManualRefresh();
        }

        public void ManualRefresh()
        {
            if (_targetText == null)
                return;

            _targetText.text = BuildHudText();
        }

        private void LateUpdate()
        {
            if (!_refreshInLateUpdate)
                return;

            if (_refreshIntervalSeconds > 0f && Time.unscaledTime < _nextRefreshTime)
                return;

            _nextRefreshTime = Time.unscaledTime + Mathf.Max(0f, _refreshIntervalSeconds);
            ManualRefresh();
        }

        private string BuildHudText()
        {
            _builder.Length = 0;
            _builder.AppendLine("=== UESTCFruit Midterm Debug HUD ===");
            _builder.AppendLine();

            AppendSimulationSection();
            AppendViewSection();
            AppendBuffSection();
            AppendRollbackSection();
            AppendScopeSection();

            return _builder.ToString();
        }

        private void AppendSimulationSection()
        {
            _builder.AppendLine("[Simulation]");
            _builder.Append("Initialized: ").AppendLine(_initialized ? "Yes" : "No");
            _builder.Append("Frame: ").AppendLine(_runner != null ? _runner.FrameCount.ToString() : "N/A");
            _builder.Append("Entities: ").AppendLine(_world != null ? _world.AliveEntityCount.ToString() : "N/A");
            _builder.Append("Player: ").AppendLine(FormatEntity(_playerEntity));
            _builder.Append("Position: ").AppendLine(ReadPositionText());
            _builder.Append("Velocity: ").AppendLine(ReadVelocityText());
            _builder.AppendLine();
        }

        private void AppendViewSection()
        {
            bool playerViewBound = _binder != null && _binder.TryGetView(_playerEntity, out _);

            _builder.AppendLine("[View]");
            _builder.Append("ViewRoot Bound: ").AppendLine(_worldViewRoot != null ? "Yes" : "No");
            _builder.Append("PlayerPrefab Bound: ").AppendLine(_playerPrefab != null ? "Yes" : "No");
            _builder.Append("Player View Bound: ").AppendLine(playerViewBound ? "Yes" : "No");
            _builder.Append("Player ViewId: ").AppendLine(ReadPlayerViewIdText());
            _builder.Append("View Status: ").AppendLine(_viewStatusText);
            _builder.AppendLine();
        }

        private void AppendBuffSection()
        {
            IReadOnlyList<BuffViewData> buffs = _buffSystem != null && _playerEntity.IsValid
                ? _buffSystem.GetBuffs(_playerEntity)
                : null;

            _builder.AppendLine("[BuffSystem]");
            _builder.Append("Buff Count: ").AppendLine(buffs != null ? buffs.Count.ToString() : "0");

            if (buffs == null || buffs.Count == 0)
            {
                _builder.AppendLine("No Buffs");
                _builder.AppendLine();
                return;
            }

            for (int i = 0; i < buffs.Count; i++)
            {
                BuffViewData buff = buffs[i];
                _builder
                    .Append("- [")
                    .Append(buff.ConfigId)
                    .Append("] Stack: ")
                    .Append(buff.Stack)
                    .Append(" | Remain: ")
                    .Append(buff.RemainingFrames)
                    .Append(" | Source: ")
                    .Append(FormatEntity(buff.Source))
                    .AppendLine();
            }

            _builder.AppendLine();
        }

        private void AppendRollbackSection()
        {
            var coordinator = _rollbackBootstrap != null ? _rollbackBootstrap.Coordinator : null;

            _builder.AppendLine("[RollBackSystem]");
            _builder.Append("Module: ").AppendLine(BuildRollbackModuleStatus(coordinator));
            _builder.Append("Logic-only Test: ").AppendLine(_logicOnlyTestStatus);
            _builder.Append("Frame: ").AppendLine(coordinator != null ? coordinator.CurrentFrame.ToString() : "N/A");
            _builder.Append("Checksum: ").AppendLine(coordinator != null ? "0x" + coordinator.CalculateChecksum().ToString("X8") : "N/A");
            _builder.AppendLine("Last Rollback Result: N/A");
            _builder.AppendLine("Note: Logic-only rollback foundation, view consistency pending.");
            _builder.AppendLine();
        }

        private void AppendScopeSection()
        {
            _builder.AppendLine("[Scope]");
            _builder.AppendLine("BuffSystem: Editor validation completed");
            _builder.AppendLine("View: midterm visualization smoke");
            _builder.Append("Pending: ").AppendLine(_pendingText);
        }

        private string ReadPositionText()
        {
            if (_world == null || !_playerEntity.IsValid)
                return "N/A";

            return _world.TryGetComponent(_playerEntity, out PositionComponent position)
                ? FormatVector(position.x, position.y, position.z)
                : "N/A";
        }

        private string ReadVelocityText()
        {
            if (_world == null || !_playerEntity.IsValid)
                return "N/A";

            return _world.TryGetComponent(_playerEntity, out VelocityComponent velocity)
                ? FormatVector(velocity.x, velocity.y, velocity.z)
                : "N/A";
        }

        private string ReadPlayerViewIdText()
        {
            if (_world == null || !_playerEntity.IsValid)
                return "N/A";

            return _world.TryGetComponent(_playerEntity, out ViewComponent view)
                ? view.viewID.ToString()
                : "N/A";
        }

        private static string BuildRollbackModuleStatus<TInput, TSnapshot>(
            RollbackCoordinator<TInput, TSnapshot> coordinator)
            where TSnapshot : ISnapshot
        {
            return coordinator != null ? "Mounted" : "Not Mounted";
        }

        private static string FormatEntity(Entity entity)
        {
            return entity.IsValid ? entity.ID + "/" + entity.Version : "Invalid";
        }

        private static string FormatVector(float x, float y, float z)
        {
            return "(" + x.ToString("0.00") + ", " + y.ToString("0.00") + ", " + z.ToString("0.00") + ")";
        }
    }
}
