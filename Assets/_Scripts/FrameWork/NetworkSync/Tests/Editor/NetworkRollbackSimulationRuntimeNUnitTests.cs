using NUnit.Framework;

namespace FrameWork.NetworkSync.Tests
{
    [TestFixture]
    public sealed class NetworkRollbackSimulationRuntimeNUnitTests
    {
        [Test]
        public void Kcp_TwoPlayers_100Frames_RealRunner_RollbackConverges()
            =>NetworkRollbackSimulationRuntimeValidationTestBootstrap.RunKcpTwoPlayers100FramesStatic();
    }
}
