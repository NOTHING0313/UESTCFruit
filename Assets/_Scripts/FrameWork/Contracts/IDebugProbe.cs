using System.Collections.Generic;
using ECS;
using BuffSystem;

namespace Contracts
{
    /// <summary>
    /// 调试探针接口（4号提供，给调试面板使用）。
    /// 只暴露逻辑世界的只读数据，供 1、2、3 号联调时查看帧、实体、校验和等信息。
    /// </summary>
    public interface IDebugProbe
    {
        int CurrentFrame { get; }
        bool IsRollbacking { get; }
        int EntityCount { get; }
        uint CurrentChecksum { get; }
        IReadOnlyList<BuffViewData> GetBuffs(EntityHandle entity);
    }
}