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
    public class EnsureSingleton<T> : MonoBehaviour where T : EnsureSingleton<T>
    {
        private static T _instance;
        public static T Instance
        {
            get
            {
                if (_instance == null || _instance.IsUnityNull())
                {
                    _instance = FindAnyObjectByType<T>();
                    if (_instance == null || _instance.IsUnityNull())
                    {
                        CreateInstance?.Invoke();
                        if (!_instance.awaked)
                        {
                            _instance.Awake();
                        }
                    }
                }
                return _instance;
            }
        }
        /// <summary>
        /// 是否退出场景不摧毁自身
        /// </summary>
        protected virtual bool _isDonDestroyOnLoad => false;
        /// <summary>
        /// 创建逻辑
        /// </summary>
        protected static Func<T> CreateInstance { get; set; } = () =>
        {
            GameObject newObj = new GameObject(typeof(T).Name);
            return newObj.AddComponent<T>();
        };

        protected bool awaked = false;
        protected virtual void Awake()
        {
            if (awaked)
                return;
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
