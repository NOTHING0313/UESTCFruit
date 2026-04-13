using PoolSystem;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;
using Utility;
namespace BuffSystem
{
    public class BuffConfigDataLoader : Singleton<BuffConfigDataLoader>
    {
        protected override bool _isDonDestroyOnLoad => true;
        [SerializeField, LabelText("BuffConfigData的地址")]
        private string BUFF_CONFIG_DATA_ROOT_PATH = "_Scripts/FrameWork/BuffSystem/BuffConfigDataCollection";
        private Dictionary<int, BuffConfigData> _map;
        private Dictionary<string, int> _nameIDMap;
        private bool _initialized = false;
        #region TagSearch
        private readonly TagRegistry _registry = new();
        // buffId -> 该 Buff 拥有哪些 Tag
        private readonly Dictionary<int, BitSet> _buffTagBits = new();
        // tagId -> 拥有该 Tag 的所有 Buff
        private readonly Dictionary<int, BitSet> _tagBitmaps = new();
        // 为了把“位图中的索引位置”映射回 buffId
        private readonly List<int> _indexToBuffId = new();
        private readonly Dictionary<int, int> _buffIdToIndex = new();
        public bool BuffHasTag(int buffId, string tagName)
        {
            if (!_initialized)
            {
                Debug.LogError("BuffConfigDataLoader BuffHasTag Error:There Is No BuffConfigDataLoader Be Inited");
                return false;
            }
            if (_registry == null)
            {
                Debug.LogError("BuffConfigDataLoader BuffHasTag Error:registry");
                return false;
            }
            if (!_registry.TryGetId(tagName, out int tagId))
                return false;
            if (!_buffTagBits.TryGetValue(buffId, out BitSet bitSet))
                return false;
            return bitSet.Contain(tagId);
        }
        public List<int> FindBuffsWithTag(string tagName) => FindBuffWithAnyTags(tagName);
        public List<int> FindBuffsWithoutTag(string tagName) => FindBuffWithoutTags(tagName);
        public List<int> FindBuffWithAllTags(params string[] requireAll) => FindBuffs(requireAll: requireAll);
        public List<int> FindBuffWithAnyTags(params string[] requireAny) => FindBuffs(requireAny: requireAny);
        public List<int> FindBuffWithoutTags(params string[] excludeAny) => FindBuffs(excludeAny: excludeAny);
        public List<int> FindBuffs(string[] requireAll = null, string[] requireAny = null, string[] excludeAny = null)
        {
            if (!_initialized)
            {
                Debug.LogError("BuffConfigDataLoader BuffHasTag Error:There Is No BuffConfigDataLoader Be Inited");
                return new();
            }
            if (_registry == null)
            {
                Debug.LogError("BuffConfigDataLoader BuffHasTag Error:registry");
                return new();
            }
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
        private List<int> GetBuffIdsFromBitmap(BitSet bitSet)
        {
            if (bitSet == null)
            {
                Debug.LogError("BuffConfigDataLoader GetBuffIdsFromBitmap Error:BitSet Is Null");
                return null;
            }
            List<int> result = new();
            foreach (int buffIndex in bitSet.EnumerateSetBits())
            {
                if (buffIndex >= 0 && buffIndex < _indexToBuffId.Count)
                    result.Add(_indexToBuffId[buffIndex]);
            }
            return result;
        }
        #endregion
        public BuffConfigData LoadBuffConfigData(string name)
        {
            if (!_initialized)
            {
                Debug.LogError("BuffConfigDataLoader BuffHasTag Error:There Is No BuffConfigDataLoader Be Inited");
                return null;
            }
            if (!_nameIDMap.TryGetValue(name, out int id))
            {
                Debug.LogError($"BuffConfigDataLoader LoadBuffConfigData Error:Cant Found The Value Of The Name:{name}");
                return null;
            }
            return LoadBuffConfigData(id);
        }
        public BuffConfigData LoadBuffConfigData(int id)
        {
            if (!_initialized)
            {
                Debug.LogError("BuffConfigDataLoader BuffHasTag Error:There Is No BuffConfigDataLoader Be Inited");
                return null;
            }
            BuffConfigData temp = ScriptableObject.CreateInstance<BuffConfigData>();
            if (_map.ContainsKey(id))
            {
                _map[id].CopyTo(temp);
                return temp;
            }
            Debug.LogError($"BuffConfigDataLoader LoadBuffConfigData Error:Cant Found The Value Of The ID:{id}");
            return null;
        }
        public void Init(int maxFailCount = 0)
        {
            ResetList();
            int failCount = 0;
            BuffTags tagConfig = BuffTags.GetOrFind();
            if (tagConfig == null)
            {
                Debug.LogError($"BuffConfigDataLoader Init Error:Fail To Load BuffTag");
                Debug.LogError($"BuffConfigDataLoader Init Fail");
                return;
            }
            foreach (TagPair<string> pair in tagConfig.BuffTagPairs)
            {
                if (string.IsNullOrEmpty(pair.Second))
                {
                    Debug.LogError($"BuffConfigDataLoader Init Error:Cant Find Buff Tag");
                    failCount++;
                    continue;
                }
                _registry.GetOrCreate(pair.Second);
            }
            BuffConfigData[] configDatas = Resources.LoadAll<BuffConfigData>(BUFF_CONFIG_DATA_ROOT_PATH);
            if (configDatas == null)
            {
                Debug.LogError($"BuffConfigDataLoader Init Error:Cant Find Buff ConfigDatas,Maybe Path:{BUFF_CONFIG_DATA_ROOT_PATH} IS Wrong");
                Debug.LogError($"BuffConfigDataLoader Init Fail");
                return;
            }
            _map = new(configDatas.Length);
            _nameIDMap = new(configDatas.Length);
            int index = 0;
            foreach (BuffConfigData configData in configDatas)
            {
                if (_map.ContainsKey(configData.ID))
                {
                    Debug.LogError($"BuffConfigDataLoader Init Error:Duplicate Buff ID: {configData.ID} ({configData.name})");
                    failCount++;
                    continue;
                }
                if (_nameIDMap.ContainsKey(configData.Name))
                {
                    Debug.LogError($"BuffConfigDataLoader Init Error:Duplicate Buff Name: {configData.ID} ({configData.name})");
                    failCount++;
                    continue;
                }
                _map.Add(configData.ID, configData);
                int buffIndex = index++;
                _buffIdToIndex.Add(configData.ID, buffIndex);
                _indexToBuffId.Add(configData.ID);
                _nameIDMap.Add(configData.Name, configData.ID);
                if (configData.Tags == null)
                {
                    Debug.LogError($"BuffConfigDataLoader Init Error:Buff Of ID:{configData.ID}`s Tags Is Null");
                    Debug.LogError($"BuffConfigDataLoader Init Fail");
                    return;
                }
                BitSet tagBitSet = new();
                foreach (string tag in configData.Tags)
                {
                    if (!_registry.TryGetId(tag, out int tagID))
                    {
                        Debug.LogError($"BuffConfigDataLoader Init Error: Undefined Tag {tag} In Buff {configData.name}");
                        failCount++;
                        continue;
                    }
                    tagBitSet.Set(tagID);
                    if (_tagBitmaps.TryGetValue(tagID, out BitSet buffBitSet))
                        buffBitSet.Set(buffIndex);
                    else
                    {
                        if (!_tagBitmaps.TryAdd(tagID, new(configDatas.Length)))
                        {
                            Debug.LogError($"BuffConfigDataLoader Init Error:Fail To Create TagBitmaps");
                            failCount++;
                            continue;
                        }
                        _tagBitmaps[tagID].Set(buffIndex);
                    }
                }
                if (!_buffTagBits.TryAdd(configData.ID, tagBitSet))
                {
                    Debug.LogError($"BuffConfigDataLoader Init Error:Fail To Create BuffTagBits");
                    failCount++;
                    continue;
                }
            }
            _initialized = failCount <= maxFailCount;
            if (_initialized)
            {
                if (failCount == 0)
                    Debug.Log($"BuffConfigDataLoader Init Succeed: Successfully Add {_map.Count} BuffConfigData");
                else
                    Debug.LogWarning($"BuffConfigDataLoader Init Succeed: But Find {failCount} Errors");
            }
            else
                Debug.LogError($"BuffConfigDataLoader Init Fail: Error Count = {failCount}, MaxFailCount = {maxFailCount}");
        }
        protected override void Awake()
        {
            base.Awake();
            if (!_initialized)
                Init();
        }
        private void ResetList()
        {
            _map?.Clear();
            _nameIDMap?.Clear();
            _buffIdToIndex.Clear();
            _indexToBuffId.Clear();
            _tagBitmaps.Clear();
            _buffTagBits.Clear();
            _registry.Clear();
        }
        private void OnDestroy() => ResetList();
    }
    public static class BuffFactory
    {
        public static TBuff CreateBuff<TBuff>(int id, BuffRuntimeData runTimeData) where TBuff : Buff, new()
        {
            BuffConfigData buffConfigData = BuffConfigDataLoader.Instance.LoadBuffConfigData(id);
            if (buffConfigData == null)
            {
                Debug.LogError($"BuffFactory CreateBuff Error: BuffConfigData Is Null, ID:{id}");
                return null;
            }
            TBuff buff = new();
            runTimeData.ActualDuration = buffConfigData.Duration;
            buff.ConfigData = buffConfigData;
            buff.RunTimeData = runTimeData;
            return buff;
        }
    }
    public static class BuffRuntimeDataFactory
    {
        public static BuffRuntimeData Get(GameObject source, GameObject target, int stack)
        {
            BuffRuntimeData data = ReferencePoolCenter.Instance.GetReference<BuffRuntimeData>();
            data.Init(source, target, stack);
            return data;
        }
        public static ParallelBuffRunTimeData GetParallel(GameObject source, GameObject target, int stack)
        {
            ParallelBuffRunTimeData data = ReferencePoolCenter.Instance.GetReference<ParallelBuffRunTimeData>();
            data.Init(source, target, stack);
            return data;
        }
        public static void Release(BuffRuntimeData data)
        {
            if (data == null) return;
            if (data is ParallelBuffRunTimeData parallel)
                ReferencePoolCenter.Instance.ReleaseReference(parallel);
            else
                ReferencePoolCenter.Instance.ReleaseReference(data);
        }
    }
}
