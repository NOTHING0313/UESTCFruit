using NUnit.Framework;

namespace FrameWork.NetworkSync.Tests
{
    [TestFixture]
    public sealed class NetworkPlayerLayoutNUnitTests
    {
        [Test]
        public void TwoPlayers_SpawnSymmetricallyByPlayerID()
        {
            Assert.AreEqual(-1.5f,NetworkPlayerLayout.GetSpawnX(1,2,3f));
            Assert.AreEqual(1.5f,NetworkPlayerLayout.GetSpawnX(2,2,3f));
        }

        [Test]
        public void OnePlayer_SpawnsAtOrigin()
            =>Assert.AreEqual(0f,NetworkPlayerLayout.GetSpawnX(1,1,3f));

        [Test]
        public void ThreePlayers_MiddlePlayerSpawnsAtOrigin()
        {
            Assert.AreEqual(-3f,NetworkPlayerLayout.GetSpawnX(1,3,3f));
            Assert.AreEqual(0f,NetworkPlayerLayout.GetSpawnX(2,3,3f));
            Assert.AreEqual(3f,NetworkPlayerLayout.GetSpawnX(3,3,3f));
        }
    }
}
