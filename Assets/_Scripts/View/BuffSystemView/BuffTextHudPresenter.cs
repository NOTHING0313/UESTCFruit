using BuffSystem;
using ECSFrameWork;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace View
{
    /// <summary>
    /// Minimal attachable text HUD presenter for Buff view models.
    /// </summary>
    public sealed class BuffTextHudPresenter : MonoBehaviour
    {
        [SerializeField] private Text _targetText;
        [SerializeField] private bool _refreshInLateUpdate = true;

        private IBuffSystem _buffSystem;
        private Entity _ownerEntity = Entity.Invalid;
        private BuffSystemViewAdapter _adapter = new BuffSystemViewAdapter();
        private readonly BuffTextHudFormatter _formatter = new BuffTextHudFormatter();

        public void Initialize(
            IBuffSystem buffSystem,
            Entity ownerEntity,
            Text targetText = null,
            IBuffViewDefinitionResolver resolver = null)
        {
            _buffSystem = buffSystem;
            _ownerEntity = ownerEntity;
            _adapter = new BuffSystemViewAdapter(resolver);

            if (targetText != null)
                _targetText = targetText;
        }

        public void Initialize(
            IBuffSystem buffSystem,
            int ownerEntity,
            Text targetText = null,
            IBuffViewDefinitionResolver resolver = null)
        {
            Entity entity = ownerEntity >= 0 ? new Entity(ownerEntity, 1) : Entity.Invalid;
            Initialize(buffSystem, entity, targetText, resolver);
        }

        public void SetOwnerEntity(Entity ownerEntity)
        {
            _ownerEntity = ownerEntity;
        }

        public void ManualRefresh()
        {
            if (_targetText == null || _buffSystem == null || !_ownerEntity.IsValid)
                return;

            IReadOnlyList<BuffViewModel> buffs = _adapter.BuildViewModels(_buffSystem, _ownerEntity);
            _targetText.text = _formatter.Format(buffs);
        }

        private void LateUpdate()
        {
            if (_refreshInLateUpdate)
                ManualRefresh();
        }
    }
}
