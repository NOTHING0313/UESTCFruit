using FrameWork.RollBackSystem;
using ECSFrameWork;
using System;

namespace FrameWork.NetworkSync
{
    /// <summary>创建已接入 Prediction History 与 RollbackCoordinator 的网络客户端运行时。</summary>
    public static class NetworkRollbackClientRuntimeFactory
    {
        public static NetworkRollbackClientRuntime Create(
            NetworkInputClientOptions clientOptions,
            FrameInputAssembler assembler,
            RollbackCoordinator<FrameInputSet,EcsWorldSnapshot> coordinator)
        {
            if(clientOptions==null) throw new ArgumentNullException(nameof(clientOptions));
            if(assembler==null) throw new ArgumentNullException(nameof(assembler));
            if(coordinator==null) throw new ArgumentNullException(nameof(coordinator));

            INetworkInputClient client=NetworkInputClientFactory.Create(clientOptions);
            var authorityDriver=new NetworkAuthorityRollbackDriver(assembler,coordinator);

            try
            {
                return new NetworkRollbackClientRuntime(client,authorityDriver);
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }
    }
}
