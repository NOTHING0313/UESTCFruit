using System.Collections.Generic;
using BuffSystem;
using ECSFrameWork;
using UnityEngine;

namespace View
{
    public sealed class BuffUIViewPresenter : MonoBehaviour
    {
        [SerializeField] private BuffUIViewConfig _config;
        [SerializeField] private float _fixedDeltaTime = 1f / 60f;

        private readonly Dictionary<Entity, BuffBarView> _bars = new Dictionary<Entity, BuffBarView>();
        private ObjectPoolFacade _pool;

        public void Initialize(ObjectPoolFacade pool, BuffUIViewConfig config, float fixedDeltaTime)
        {
            _pool = pool;
            if (config != null)
                _config = config;

            _fixedDeltaTime = fixedDeltaTime;
        }

        public void Register(Entity entity, BuffBarView bar)
        {
            if (!entity.IsValid || bar == null)
                return;

            bar.Initialize(_pool, _config, _fixedDeltaTime);
            _bars[entity] = bar;
        }

        public void Unregister(Entity entity)
        {
            _bars.Remove(entity);
        }

        public void RefreshAll(IBuffSystem buffSystem)
        {
            if (buffSystem == null)
                return;

            foreach (KeyValuePair<Entity, BuffBarView> pair in _bars)
            {
                IReadOnlyList<BuffViewData> buffs = buffSystem.GetBuffs(pair.Key);
                pair.Value.Refresh(buffs);
            }
        }
    }
}