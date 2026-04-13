using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using Sirenix.OdinInspector;
namespace PoolSystem
{
    public class TestGameObjectPool : MonoBehaviour
    {
        public GameObject prefab;
        private List<GameObject> instances=new();
        [Button]
        public void Add() => instances.Add(GameObjectPoolCenter.Instance.GetInstance(prefab,new Vector3(Random.Range(-10,10), Random.Range(-10,10)),Quaternion.Euler(new Vector3(1,1,1))));
        [Button]
        public void Release() { if (instances.Count > 0) { GameObjectPoolCenter.Instance.Release(instances[instances.Count - 1]); instances.RemoveAt(instances.Count - 1); } }
    }
}
