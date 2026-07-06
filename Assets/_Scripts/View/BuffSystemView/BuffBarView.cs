using System.Collections.Generic;
using BuffSystem;
using Contracts;
using UnityEngine;

namespace View
{
    public sealed class BuffBarView : MonoBehaviour
    {
        [SerializeField] private int _buffIconPrefabId;
        [SerializeField] private RectTransform _contentRoot;
        [SerializeField] private BuffUIViewConfig _config;
        [SerializeField] private float _fixedDeltaTime = 1f / 60f;

        private readonly List<GameObject> _activeIcons = new List<GameObject>();
        private IObjectPoolFacade _pool;

        public void Initialize(IObjectPoolFacade pool, BuffUIViewConfig config, float fixedDeltaTime)
        {
            _pool = pool;
            if (config != null)
                _config = config;

            _fixedDeltaTime = fixedDeltaTime;
        }

        public void Refresh(IReadOnlyList<BuffViewData> buffs)
        {
            ReleaseAll();

            if (_pool == null || _contentRoot == null || buffs == null)
                return;

            for (int i = 0; i < buffs.Count; i++)
            {
                GameObject iconObject = _pool.GetUIView(
                    _buffIconPrefabId,
                    _contentRoot,
                    Vector2.zero);

                if (iconObject == null)
                    continue;

                if (iconObject.TryGetComponent(out BuffIconView icon))
                    icon.Bind(buffs[i], _config, _fixedDeltaTime);

                _activeIcons.Add(iconObject);
            }
        }

        private void OnDisable()
        {
            ReleaseAll();
        }

        private void ReleaseAll()
        {
            if (_pool == null)
            {
                _activeIcons.Clear();
                return;
            }

            for (int i = 0; i < _activeIcons.Count; i++)
                _pool.Release(_activeIcons[i]);

            _activeIcons.Clear();
        }
    }
}