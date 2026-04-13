using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuffSystem
{
    public sealed class TagRegistry
    {
        private readonly Dictionary<string, int> _nameToID = new(StringComparer.Ordinal);
        private readonly List<string> _idToName = new();
        public int GetOrCreate(string tagName)
        {
            if (string.IsNullOrEmpty(tagName))
            {
                Debug.LogError("TagRegistry GetOrCreate Error:Cant Find TagName");
                return -1;
            }
            if (_nameToID.TryGetValue(tagName, out int id))
                return id;
            int newID = _idToName.Count;
            _idToName.Add(tagName);
            _nameToID.Add(tagName, newID);
            return newID;
        }
        public bool TryGetId(string tagName, out int tagId) => _nameToID.TryGetValue(tagName, out tagId);
        public int TagCount => _idToName.Count;
        public string GetName(int id)
        {
            if (id < 0 || id >= TagCount)
            {
                Debug.LogError($"TagRegistry GetOrCreate Error:ID:{id} Is Out Of Range");
                return null;
            }
            return _idToName[id];
        }
        public void Clear()
        {
            _nameToID.Clear();
            _idToName.Clear();
        }
    }
}
