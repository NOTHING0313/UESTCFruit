using NUnit.Framework;

namespace FrameWork.RollBackSystem.Tests
{
    [TestFixture]
    public sealed class BuffRollbackIntegrationValidationNUnitTests
    {
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(6)]
        [TestCase(12)]
        [TestCase(30)]
        [TestCase(60)]
        public void Buff_RollbackRestoreListener_ConvergesToReferenceWorld(int rollbackDepth) => BuffRollbackIntegrationValidationTestBootstrap.RunBuffRollbackRestoreIntegrationTestStatic(rollbackDepth);
    }
}