
using System;
using System.Collections.Generic;

namespace BuffSystem
{
    public interface IGameEvent { }
    #region 事件系统接口
    public interface IBuffEventSubscriber
    {
        /// <summary>
        /// 返回该buff要订阅的事件监听器集合（对象可缓存复用）
        /// </summary>
        /// <returns></returns>
        IEnumerable<IEventListener> GetEventListeners();
    }
    /// <summary>
    /// 强类型事件监听器（不装箱，不反射）
    /// </summary>
    public interface IEventListener
    {
        Type EventType { get; }
        int Priority { get; }
        int OwnerBuffID { get; set; }
        /// <summary>
        /// 路由层统一入口
        /// </summary>
        /// <param name="buffHandler"></param>
        /// <param name="buffEvent"></param>
        void Invoke(BuffHandler buffHandler, in IGameEvent buffEvent);
    }
    #endregion
}
