using NUnit.Framework;

namespace FrameWork.NetworkSync.Tests
{
    [TestFixture]
    public sealed class NetworkRollbackClientRuntimeNUnitTests
    {
        [Test]
        public void RawUdpRuntime_20Frames_AuthorityAppliedThroughCommonRuntime()
            =>NetworkRollbackClientRuntimeValidationTestBootstrap.RunRawUdp20FramesStatic();

        [Test]
        public void KcpRuntime_20Frames_AuthorityAppliedThroughCommonRuntime()
            =>NetworkRollbackClientRuntimeValidationTestBootstrap.RunKcp20FramesStatic();
    }
}
