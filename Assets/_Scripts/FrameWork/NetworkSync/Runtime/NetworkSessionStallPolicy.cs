using System;

namespace FrameWork.NetworkSync
{
    /// <summary>网络 Session 暂停原因。</summary>
    public enum NetworkSessionStallReason
    {
        None,
        TransportUnavailable,
        AuthorityTimeout
    }

    /// <summary>
    /// Session 逻辑推进门禁。只根据 Transport 状态与 Authority 心跳判断是否应继续正常模拟。
    /// 不直接依赖 Unity、TimeSimulator 或 RollbackCoordinator。
    /// </summary>
    public sealed class NetworkSessionStallPolicy
    {
        private readonly double _authorityTimeoutSeconds;
        private bool _initialized;
        private int _lastAuthorityCount;
        private double _lastAuthorityProgressTime;

        public double AuthorityTimeoutSeconds => _authorityTimeoutSeconds;
        public NetworkSessionStallReason StallReason { get; private set; }=NetworkSessionStallReason.TransportUnavailable;
        public bool ShouldRunSimulation => StallReason==NetworkSessionStallReason.None;
        public int LastAuthorityCount => _lastAuthorityCount;
        public double LastAuthorityProgressTime => _lastAuthorityProgressTime;

        public NetworkSessionStallPolicy(double authorityTimeoutSeconds)
        {
            if(authorityTimeoutSeconds<=0d) throw new ArgumentOutOfRangeException(nameof(authorityTimeoutSeconds));
            _authorityTimeoutSeconds=authorityTimeoutSeconds;
        }

        /// <summary>为一个新 Session / Runtime 建立基线。</summary>
        public void Reset(double nowSeconds,NetworkInputClientConnectionState connectionState,int authorityCount=0)
        {
            Validate(nowSeconds,authorityCount);

            _initialized=true;
            _lastAuthorityCount=authorityCount;
            _lastAuthorityProgressTime=nowSeconds;
            StallReason=connectionState==NetworkInputClientConnectionState.Connected
                ?NetworkSessionStallReason.None
                :NetworkSessionStallReason.TransportUnavailable;
        }

        /// <summary>
        /// 评估当前 Session 是否允许继续推进正常逻辑帧。
        /// AuthorityCount 只允许单调不减；收到新 Authority 即刷新心跳并解除 AuthorityTimeout。
        /// </summary>
        public bool Evaluate(double nowSeconds,NetworkInputClientConnectionState connectionState,int authorityCount)
        {
            Validate(nowSeconds,authorityCount);

            if(!_initialized)
                Reset(nowSeconds,connectionState,authorityCount);

            if(authorityCount<_lastAuthorityCount)
                throw new InvalidOperationException(
                    $"Network Session Authority Count Regressed: Previous={_lastAuthorityCount}, Current={authorityCount}");

            if(authorityCount>_lastAuthorityCount)
            {
                _lastAuthorityCount=authorityCount;
                _lastAuthorityProgressTime=nowSeconds;
            }

            if(connectionState!=NetworkInputClientConnectionState.Connected)
            {
                StallReason=NetworkSessionStallReason.TransportUnavailable;
                return false;
            }

            if(nowSeconds-_lastAuthorityProgressTime>=_authorityTimeoutSeconds)
            {
                StallReason=NetworkSessionStallReason.AuthorityTimeout;
                return false;
            }

            StallReason=NetworkSessionStallReason.None;
            return true;
        }

        private static void Validate(double nowSeconds,int authorityCount)
        {
            if(double.IsNaN(nowSeconds)||double.IsInfinity(nowSeconds)||nowSeconds<0d)
                throw new ArgumentOutOfRangeException(nameof(nowSeconds));
            if(authorityCount<0) throw new ArgumentOutOfRangeException(nameof(authorityCount));
        }
    }
}
