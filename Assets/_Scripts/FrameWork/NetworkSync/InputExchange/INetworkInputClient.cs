using ECSFrameWork;
using System;
using System.Net;

namespace FrameWork.NetworkSync
{
    /// <summary>
    /// 网络输入客户端公共契约。上层只依赖输入发送与 Authority 接收语义，不依赖具体 UDP/KCP 实现。
    /// </summary>
    public interface INetworkInputClient : IDisposable
    {
        NetworkInputTransportMode TransportMode { get; }
        uint SessionId { get; }
        int PlayerID { get; }
        bool IsReady { get; }
        IPEndPoint LocalEndPoint { get; }
        uint LastSentSequence { get; }
        NetworkInputExchangeRejectReason LastRejectReason { get; }
        NetworkPacketDecodeError LastDecodeError { get; }
        bool HasTransportError { get; }
        string LastTransportError { get; }

        /// <summary>推进需要主动 Pump 的传输；Raw UDP 为无操作。</summary>
        void Tick();

        /// <summary>发送当前客户端玩家的一帧输入。</summary>
        void SendInput(in PlayerInputSnapshot input);

        /// <summary>非阻塞获取一个服务器 Authority Frame。</summary>
        bool TryReceiveAuthority(out ServerAuthorityFramePacket packet);
    }
}
