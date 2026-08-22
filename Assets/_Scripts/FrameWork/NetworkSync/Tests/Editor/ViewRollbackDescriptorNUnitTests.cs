using ECSFrameWork;
using FrameWork.RollBackSystem;
using NUnit.Framework;
using UnityEngine;

namespace FrameWork.NetworkSync.Tests
{
    [TestFixture]
    public sealed class ViewRollbackDescriptorNUnitTests
    {
        [Test]
        public void ViewPrefabDescriptor_IsCapturedBySnapshot()
        {
            World world=new World { EnableSystemProfile=false };

            try
            {
                Entity entity=world.CreateEntity();
                world.SetComponent(entity,new PositionComponent(0f,0f,0f));
                world.SetComponent(entity,new ViewPrefabComponent(7));

                EcsWorldSnapshot snapshot=world.CaptureSnapshot(0);
                bool found=false;

                for(int i=0;i<snapshot.ComponentStores.Count;i++)
                {
                    if(snapshot.ComponentStores[i].ComponentType==typeof(ViewPrefabComponent))
                    {
                        found=true;
                        break;
                    }
                }

                Assert.IsTrue(found,"ViewPrefabComponent must survive Rollback Snapshot as stable presentation descriptor.");
            }
            finally
            {
                world.Dispose();
            }
        }

        [Test]
        public void ViewPrefabDescriptor_DoesNotAffectLogicChecksum()
        {
            World a=CreateWorld(1);
            World b=CreateWorld(99);

            try
            {
                Assert.AreEqual(
                    WorldChecksumCalculator.Calculate(a),
                    WorldChecksumCalculator.Calculate(b),
                    "Stable View prefab metadata must not pollute gameplay logic checksum.");
            }
            finally
            {
                a.Dispose();
                b.Dispose();
            }
        }

        private static World CreateWorld(int prefabID)
        {
            var world=new World { EnableSystemProfile=false };
            Entity entity=world.CreateEntity();
            world.SetComponent(entity,new PositionComponent(1f,2f,3f));
            world.SetComponent(entity,new ViewPrefabComponent(prefabID));
            return world;
        }
    }
}
