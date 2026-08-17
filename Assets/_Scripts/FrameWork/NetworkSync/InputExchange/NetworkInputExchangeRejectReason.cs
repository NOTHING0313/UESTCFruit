namespace FrameWork.NetworkSync
{
    /// <summary>本地输入交换层拒绝 Datagram 的原因。</summary>
    public enum NetworkInputExchangeRejectReason
    {
        None,
        DecodeFailed,
        SessionMismatch,
        UnregisteredPlayer,
        EndpointMismatch,
        InputConflict
    }
}