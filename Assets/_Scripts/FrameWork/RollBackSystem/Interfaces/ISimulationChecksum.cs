/*
 * 文件说明：ISimulationChecksum 定义逻辑状态校验接口。
 * 设计约束：Checksum 必须稳定且确定，用于检测客户端与服务器状态漂移。
 */

namespace Simulation.Contracts
{
    public interface ISimulationChecksum
    {
        uint CalculateChecksum();
    }
}