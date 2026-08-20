using ECSFrameWork;
using FrameWork.RollBackSystem;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Threading;

namespace FrameWork.NetworkSync.Tests
{
    [TestFixture]
    public sealed class KcpNetworkInputLoopbackNUnitTests
    {
        private const uint SessionId=0x11223344u;
        private const int TimeoutMs=3000;

        [Test]
        public void TwoClients_100Frames_KcpAuthorityRoundTrip_BitExact()
        {
            using var server=new KcpNetworkInputServer(0,2,SessionId);
            int port=server.LocalEndPoint.Port;

            using var client1=new KcpNetworkInputClient("127.0.0.1",port,SessionId,1);
            using var client2=new KcpNetworkInputClient("127.0.0.1",port,SessionId,2);

            WaitConnected(server,client1,client2);

            uint lastSequence1=0,lastSequence2=0;

            for(int frame=1;frame<=100;frame++)
            {
                PlayerInputSnapshot input1=CreateInput(frame,1);
                PlayerInputSnapshot input2=CreateInput(frame,2);

                client1.SendInput(in input1);
                client2.SendInput(in input2);

                ServerAuthorityFramePacket authority1=default,authority2=default;
                bool received1=false,received2=false;
                Stopwatch stopwatch=Stopwatch.StartNew();

                while(stopwatch.ElapsedMilliseconds<TimeoutMs)
                {
                    server.Tick();

                    if(!received1&&client1.TryReceiveAuthority(out ServerAuthorityFramePacket packet1))
                    {
                        authority1=packet1;
                        received1=true;
                    }

                    if(!received2&&client2.TryReceiveAuthority(out ServerAuthorityFramePacket packet2))
                    {
                        authority2=packet2;
                        received2=true;
                    }

                    if(received1&&received2) break;
                    Thread.Sleep(1);
                }

                Assert.IsTrue(received1,$"KCP Client1 Authority Timeout: Frame={frame}");
                Assert.IsTrue(received2,$"KCP Client2 Authority Timeout: Frame={frame}");

                Assert.Greater(authority1.Sequence,lastSequence1);
                Assert.Greater(authority2.Sequence,lastSequence2);
                lastSequence1=authority1.Sequence;
                lastSequence2=authority2.Sequence;

                var expected=new FrameInputSet(frame,new[] { input1,input2 });
                AssertInputSetBitExact(expected,authority1.InputSet,frame,"Client1");
                AssertInputSetBitExact(expected,authority2.InputSet,frame,"Client2");
            }

            Assert.AreEqual(200,server.ProcessedMessageCount);
            Assert.AreEqual(0,server.RejectedMessageCount);
            Assert.AreEqual(100,server.AuthorityFrameCount);
            Assert.AreEqual(100u,client1.LastSentSequence);
            Assert.AreEqual(100u,client2.LastSentSequence);
        }

        private static void WaitConnected(KcpNetworkInputServer server,KcpNetworkInputClient client1,KcpNetworkInputClient client2)
        {
            Stopwatch stopwatch=Stopwatch.StartNew();

            while(stopwatch.ElapsedMilliseconds<TimeoutMs)
            {
                server.Tick();
                client1.Tick();
                client2.Tick();

                if(client1.IsConnected&&client2.IsConnected&&server.ConnectedConnectionCount==2) return;
                Thread.Sleep(1);
            }

            Assert.Fail(
                $"KCP Connect Timeout: Client1={client1.IsConnected}, Client2={client2.IsConnected}, ServerConnections={server.ConnectedConnectionCount}, " +
                $"Client1Error={client1.LastKcpError}:{client1.LastKcpErrorMessage}, Client2Error={client2.LastKcpError}:{client2.LastKcpErrorMessage}");
        }

        private static PlayerInputSnapshot CreateInput(int frame,int playerID)
        {
            return new PlayerInputSnapshot(frame,playerID)
            {
                moveX=(frame+playerID)%3-1,
                moveY=(frame*3+playerID)%3-1,
                mouseX=frame*0.25f+playerID,
                mouseY=-frame*0.125f-playerID,
                mouseDeltaX=playerID*0.5f,
                mouseDeltaY=playerID*-0.25f,
                scrollX=playerID,
                scrollY=-playerID
            };
        }

        private static void AssertInputSetBitExact(FrameInputSet expected,FrameInputSet actual,int frame,string stage)
        {
            Assert.IsTrue(expected.IsCreated&&actual.IsCreated,$"{stage} InputSet Not Created: Frame={frame}");
            Assert.AreEqual(expected.frameNumber,actual.frameNumber,$"{stage} Frame Mismatch");
            Assert.AreEqual(expected.Count,actual.Count,$"{stage} Count Mismatch");

            for(int i=0;i<expected.Count;i++)
            {
                PlayerInputSnapshot a=expected.GetInputAt(i);
                PlayerInputSnapshot b=actual.GetInputAt(i);
                AssertInputBitExact(in a,in b,frame,stage);
            }
        }

        private static void AssertInputBitExact(in PlayerInputSnapshot a,in PlayerInputSnapshot b,int frame,string stage)
        {
            var writerA=new NetworkPacketWriter(PlayerInputSnapshotNetworkCodec.WireSize);
            var writerB=new NetworkPacketWriter(PlayerInputSnapshotNetworkCodec.WireSize);

            PlayerInputSnapshotNetworkCodec.Write(writerA,in a);
            PlayerInputSnapshotNetworkCodec.Write(writerB,in b);

            CollectionAssert.AreEqual(writerA.ToArray(),writerB.ToArray(),$"{stage} Input Bit Mismatch: Frame={frame}, PlayerID={a.playerID}");
        }
    }
}
