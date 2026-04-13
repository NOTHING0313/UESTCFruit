using UnityEngine;

namespace PoolSystem
{
    public sealed class PoolItem : MonoBehaviour
    {
        public int PoolID { get; internal set; } = -1;
        public int PrefabInstanceID { get; internal set; }//用于校验
        public bool IsInPool { get; internal set; } = true;
    }
}