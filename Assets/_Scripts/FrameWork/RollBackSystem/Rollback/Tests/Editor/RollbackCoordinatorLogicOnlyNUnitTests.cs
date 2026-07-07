using NUnit.Framework;

namespace FrameWork.RollBackSystem.Tests
{
    [TestFixture]
    public sealed class RollbackCoordinatorLogicOnlyNUnitTests
    {
        [Test]
        public void LogicOnlyRollback_AllCases_Pass()
        {
            RollbackCoordinatorLogicOnlyTestBootstrap.RunLogicOnlyRollbackTestsStatic();
        }
    }
}
