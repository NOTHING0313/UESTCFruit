using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Utility.EventCenter
{
    public class EventCenter
    {
        private Dictionary<Type, Delegate> _pool = new();
        public void Listen<EventType>(Action<EventType> action) where EventType : IEventData
        {
            if (_pool == null)
            {
                Debug.LogError("EventCenter Listen Error:Pool Is Null");
                return;
            }
            Type type = typeof(EventType);
            if (_pool.ContainsKey(type))
                _pool[type] = Delegate.Combine(_pool[type], action);
            else
                _pool[type] = action;
        }
        public void CancelListen<EventType>(Action<EventType> action) where EventType : IEventData
        {
            if (_pool == null)
            {
                Debug.LogError("EventCenter CancelListen Error:Pool Is Null");
                return;
            }
            Type type = typeof(EventType);
            if (!_pool.ContainsKey(type))
                Debug.LogError($"EventCenter CancelListen Error:The Value Of The Key:{type.Name} Cant Found In The Pool");
            else
                _pool[type] = Delegate.Remove(_pool[type], action);
        }
        public void Invoke<EventType>(EventType _data) where EventType : IEventData
        {
            Type type = typeof(EventType);
            if (!_pool.ContainsKey(type))
            {
                Debug.LogWarning($"EventCenter Invoke Warning:The Value Of The Key:{type.Name} Cant Found In The Pool");
                return;
            }
            Delegate _dele = _pool[type];
            if (_dele != null && _dele is Action<EventType> _action && _action != null)
                _action.Invoke(_data);
        }
    }
}
