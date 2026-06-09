using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace BuffSystem
{
    public class BuffConfigDataLoader : Singleton<BuffConfigDataLoader>, IBuffDefinitionProvider
    {
        protected override bool _isDonDestroyOnLoad => true;

        [SerializeField, LabelText("Buff 配置 Resources 路径"), Tooltip("用于 Resources.LoadAll 读取 BuffConfigData 的根路径。")]
        private string BUFF_CONFIG_DATA_ROOT_PATH = "BuffSystem/Buff";

        [SerializeField, LabelText("模拟帧长"), Tooltip("Authoring 配置转换为 BuffDefinition 时使用的固定帧长度。")]
        private float _tickLength = 1f / 60f;

        private readonly TagRegistry _registry = new TagRegistry();
        private readonly Dictionary<int, BuffConfigData> _map = new Dictionary<int, BuffConfigData>();
        private readonly Dictionary<string, int> _nameIDMap = new Dictionary<string, int>();
        private readonly Dictionary<int, BitSet> _buffTagBits = new Dictionary<int, BitSet>();
        private readonly Dictionary<int, BitSet> _tagBitmaps = new Dictionary<int, BitSet>();
        private readonly List<int> _indexToBuffId = new List<int>();
        private readonly Dictionary<int, int> _buffIdToIndex = new Dictionary<int, int>();
        private readonly BuffDefinitionRegistry _definitionRegistry = new BuffDefinitionRegistry();

        private bool _initialized;

        public bool IsInitialized => _initialized;
        public int DefinitionCount => _definitionRegistry.Count;

        public void SetTickLength(float tickLength)
        {
            _tickLength = tickLength > 0f ? tickLength : 1f / 60f;

            if (_initialized)
                RebuildDefinitions();
        }

        public bool TryGetDefinition(int configId, out BuffDefinition definition)
        {
            if (!_initialized)
                Init();

            return _definitionRegistry.TryGetDefinition(configId, out definition);
        }

        public bool BuffHasTag(int buffId, string tagName)
        {
            if (!_initialized || string.IsNullOrEmpty(tagName))
                return false;

            if (!_registry.TryGetId(tagName, out int tagId))
                return false;

            return _buffTagBits.TryGetValue(buffId, out BitSet bitSet) && bitSet.Contain(tagId);
        }

        public List<int> FindBuffsWithTag(string tagName) => FindBuffWithAnyTags(tagName);
        public List<int> FindBuffsWithoutTag(string tagName) => FindBuffWithoutTags(tagName);
        public List<int> FindBuffWithAllTags(params string[] requireAll) => FindBuffs(requireAll: requireAll);
        public List<int> FindBuffWithAnyTags(params string[] requireAny) => FindBuffs(requireAny: requireAny);
        public List<int> FindBuffWithoutTags(params string[] excludeAny) => FindBuffs(excludeAny: excludeAny);

        public List<int> FindBuffs(string[] requireAll = null, string[] requireAny = null, string[] excludeAny = null)
        {
            if (!_initialized)
                return new List<int>();

            BitSet result = new BitSet(_indexToBuffId.Count);
            result.FillAll(_indexToBuffId.Count);

            if (requireAll != null && requireAll.Length > 0)
            {
                foreach (string tagName in requireAll)
                {
                    if (!_registry.TryGetId(tagName, out int tagId))
                        return new List<int>();

                    if (!_tagBitmaps.TryGetValue(tagId, out BitSet bitmap))
                        return new List<int>();

                    result.AndWith(bitmap);
                }
            }

            if (requireAny != null && requireAny.Length > 0)
            {
                BitSet anySet = new BitSet(_indexToBuffId.Count);

                foreach (string tagName in requireAny)
                {
                    if (_registry.TryGetId(tagName, out int tagId) && _tagBitmaps.TryGetValue(tagId, out BitSet bitmap))
                        anySet.OrWith(bitmap);
                }

                result.AndWith(anySet);
            }

            if (excludeAny != null && excludeAny.Length > 0)
            {
                foreach (string tagName in excludeAny)
                {
                    if (_registry.TryGetId(tagName, out int tagId) && _tagBitmaps.TryGetValue(tagId, out BitSet bitmap))
                        result.AndNotWith(bitmap);
                }
            }

            return GetBuffIdsFromBitmap(result);
        }

        public BuffConfigData LoadBuffConfigData(string name)
        {
            if (!_initialized)
                Init();

            if (string.IsNullOrEmpty(name) || !_nameIDMap.TryGetValue(name, out int id))
                return null;

            return LoadBuffConfigData(id);
        }

        public BuffConfigData LoadBuffConfigData(int id)
        {
            if (!_initialized)
                Init();

            if (!_map.TryGetValue(id, out BuffConfigData source))
                return null;

            BuffConfigData copy = ScriptableObject.CreateInstance<BuffConfigData>();
            source.CopyTo(copy);
            return copy;
        }

        public void Init(int maxFailCount = 0)
        {
            ResetList();

            int failCount = RegisterTags();
            BuffConfigData[] configDatas = Resources.LoadAll<BuffConfigData>(BUFF_CONFIG_DATA_ROOT_PATH);

            if (configDatas == null || configDatas.Length == 0)
            {
                Debug.LogWarning($"BuffConfigDataLoader Init Warning: no BuffConfigData found at {BUFF_CONFIG_DATA_ROOT_PATH}.");
                _initialized = failCount <= maxFailCount;
                return;
            }

            for (int i = 0; i < configDatas.Length; i++)
            {
                BuffConfigData configData = configDatas[i];

                if (configData == null || configData.ID <= 0)
                {
                    failCount++;
                    continue;
                }

                if (_map.ContainsKey(configData.ID))
                {
                    Debug.LogError($"BuffConfigDataLoader Init Error: duplicate Buff ID {configData.ID}.");
                    failCount++;
                    continue;
                }

                if (!string.IsNullOrEmpty(configData.Name) && _nameIDMap.ContainsKey(configData.Name))
                {
                    Debug.LogError($"BuffConfigDataLoader Init Error: duplicate Buff name {configData.Name}.");
                    failCount++;
                    continue;
                }

                _map.Add(configData.ID, configData);

                if (!string.IsNullOrEmpty(configData.Name))
                    _nameIDMap.Add(configData.Name, configData.ID);

                int buffIndex = _indexToBuffId.Count;
                _buffIdToIndex.Add(configData.ID, buffIndex);
                _indexToBuffId.Add(configData.ID);
                RegisterBuffTags(configData, buffIndex, ref failCount);
                _definitionRegistry.Register(configData.ToDefinition(_tickLength));
            }

            _initialized = failCount <= maxFailCount;

            if (_initialized)
                Debug.Log($"BuffConfigDataLoader Init Succeed: loaded {_definitionRegistry.Count} Buff definitions.");
            else
                Debug.LogError($"BuffConfigDataLoader Init Failed: Error Count={failCount}, MaxFailCount={maxFailCount}.");
        }

        protected override void Awake()
        {
            base.Awake();

            if (!_initialized)
                Init();
        }

        private int RegisterTags()
        {
            int failCount = 0;
            BuffTags tagConfig = BuffTags.GetOrFind();

            if (tagConfig == null)
                return failCount;

            foreach (TagPair<string> pair in tagConfig.BuffTagPairs)
            {
                if (string.IsNullOrEmpty(pair.Second))
                {
                    failCount++;
                    continue;
                }

                _registry.GetOrCreate(pair.Second);
            }

            return failCount;
        }

        private void RegisterBuffTags(BuffConfigData configData, int buffIndex, ref int failCount)
        {
            BitSet tagBitSet = new BitSet();

            if (configData.Tags == null)
            {
                _buffTagBits[configData.ID] = tagBitSet;
                return;
            }

            foreach (string tag in configData.Tags)
            {
                if (string.IsNullOrEmpty(tag))
                    continue;

                if (!_registry.TryGetId(tag, out int tagID))
                {
                    failCount++;
                    continue;
                }

                tagBitSet.Set(tagID);

                if (!_tagBitmaps.TryGetValue(tagID, out BitSet buffBitSet))
                {
                    buffBitSet = new BitSet(_indexToBuffId.Capacity);
                    _tagBitmaps.Add(tagID, buffBitSet);
                }

                buffBitSet.Set(buffIndex);
            }

            _buffTagBits[configData.ID] = tagBitSet;
        }

        private List<int> GetBuffIdsFromBitmap(BitSet bitSet)
        {
            List<int> result = new List<int>();

            if (bitSet == null)
                return result;

            foreach (int buffIndex in bitSet.EnumerateSetBits())
            {
                if (buffIndex >= 0 && buffIndex < _indexToBuffId.Count)
                    result.Add(_indexToBuffId[buffIndex]);
            }

            return result;
        }

        private void RebuildDefinitions()
        {
            _definitionRegistry.Clear();

            foreach (BuffConfigData configData in _map.Values)
                _definitionRegistry.Register(configData.ToDefinition(_tickLength));
        }

        private void ResetList()
        {
            _map.Clear();
            _nameIDMap.Clear();
            _buffIdToIndex.Clear();
            _indexToBuffId.Clear();
            _tagBitmaps.Clear();
            _buffTagBits.Clear();
            _definitionRegistry.Clear();
            _registry.Clear();
            _initialized = false;
        }

        private void OnDestroy()
        {
            ResetList();
        }
    }
}
