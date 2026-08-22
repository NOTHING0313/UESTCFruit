using NUnit.Framework;

namespace FrameWork.NetworkSync.Tests
{
    [TestFixture]
    public sealed class NetworkClientLaunchOptionsNUnitTests
    {
        [Test]
        public void Parse_NoOverrides_RemainsEmpty()
        {
            NetworkClientLaunchOptions options=NetworkClientLaunchOptions.Parse(new[] { "UESTCFruit.exe","-logFile","client.log" });

            Assert.IsFalse(options.HasAnyOverride);
            Assert.IsNull(options.PlayerID);
            Assert.IsNull(options.PlayerCount);
            Assert.IsNull(options.ServerAddress);
            Assert.IsNull(options.ServerPort);
            Assert.IsNull(options.SessionId);
        }

        [Test]
        public void Parse_EqualsSyntax_ParsesTwoPlayerLaunch()
        {
            NetworkClientLaunchOptions options=NetworkClientLaunchOptions.Parse(new[]
            {
                "UESTCFruit.exe",
                "--network-player-id=2",
                "--network-player-count=2",
                "--network-server=8.137.83.229",
                "--network-port=28015",
                "--network-session=0x11223344"
            });

            Assert.IsTrue(options.HasAnyOverride);
            Assert.AreEqual(2,options.PlayerID);
            Assert.AreEqual(2,options.PlayerCount);
            Assert.AreEqual("8.137.83.229",options.ServerAddress);
            Assert.AreEqual(28015,options.ServerPort);
            Assert.AreEqual(0x11223344u,options.SessionId);
        }

        [Test]
        public void Parse_SplitSyntax_ParsesDecimalSession()
        {
            NetworkClientLaunchOptions options=NetworkClientLaunchOptions.Parse(new[]
            {
                "UESTCFruit.exe",
                "--network-player-id","1",
                "--network-player-count","2",
                "--network-server","127.0.0.1",
                "--network-port","28015",
                "--network-session","287454020"
            });

            Assert.AreEqual(1,options.PlayerID);
            Assert.AreEqual(2,options.PlayerCount);
            Assert.AreEqual("127.0.0.1",options.ServerAddress);
            Assert.AreEqual(28015,options.ServerPort);
            Assert.AreEqual(0x11223344u,options.SessionId);
        }
    }
}
