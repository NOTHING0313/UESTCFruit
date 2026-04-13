using UnityEngine;
using System;
using Unity.VisualScripting;

namespace Utility
{
    /// <summary>
    /// 单例继承类
    /// </summary>
    /// <typeparam name="T">
    /// 单例类型
    /// </typeparam>
    public class Singleton<T> : MonoBehaviour where T : Singleton<T>
    {
        private static T _instance;
        public static T Instance => _instance;
        /// <summary>
        /// 是否退出场景不摧毁自身
        /// </summary>
        protected virtual bool _isDonDestroyOnLoad => false;

        protected bool awaked = false;
        protected virtual void Awake()
        {
            if (awaked) return;
            if (_instance == null || _instance.IsUnityNull())
                _instance = this as T;
            else if (_instance != this)
            {
                DestroyImmediate(this);
                return;
            }
            if (_instance._isDonDestroyOnLoad)
                DontDestroyOnLoad(this);
            awaked = true;
        }
    }
}
