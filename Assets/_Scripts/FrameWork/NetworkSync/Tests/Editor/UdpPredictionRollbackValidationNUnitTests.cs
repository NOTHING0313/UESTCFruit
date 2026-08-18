using NUnit.Framework;

namespace FrameWork.NetworkSync.Tests
{
    [TestFixture]
    public sealed class UdpPredictionRollbackValidationNUnitTests
    {
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(6)]
        [TestCase(12)]
        [TestCase(30)]
        [TestCase(60)]
        public void TwoPlayers_RealUdpAuthority_Player2PredictionMismatch_RollbackConverges(int authoritativeDelay)
            => UdpPredictionRollbackValidationTestBootstrap.RunUdpPredictionRollbackStatic(authoritativeDelay);
    }
}