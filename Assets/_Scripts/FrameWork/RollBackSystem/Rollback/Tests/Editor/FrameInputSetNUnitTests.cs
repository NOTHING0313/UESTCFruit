using ECSFrameWork;
using NUnit.Framework;
using System;

namespace FrameWork.RollBackSystem.Tests
{
    [TestFixture]
    public sealed class FrameInputSetNUnitTests
    {
        [Test]
        public void FrameInputSet_UnorderedInputs_AreSortedAndQueryable()
        {
            FrameInputSet set = new FrameInputSet(100, new[]
            {
                CreateInput(100,3,3f),
                CreateInput(100,1,1f),
                CreateInput(100,2,2f)
            });

            Assert.AreEqual(3, set.Count);
            Assert.AreEqual(1, set.GetInputAt(0).playerID);
            Assert.AreEqual(2, set.GetInputAt(1).playerID);
            Assert.AreEqual(3, set.GetInputAt(2).playerID);

            Assert.IsTrue(set.TryGetInput(2, out PlayerInputSnapshot input));
            Assert.AreEqual(2, input.playerID);
            Assert.AreEqual(2f, input.moveX);
            Assert.IsFalse(set.TryGetInput(99, out _));
        }

        [Test]
        public void FrameInputSet_DuplicateOrWrongFrame_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => new FrameInputSet(100, new[]
            {
                CreateInput(100,1,1f),
                CreateInput(100,1,2f)
            }));

            Assert.Throws<ArgumentException>(() => new FrameInputSet(100, new[]
            {
                CreateInput(100,1,1f),
                CreateInput(101,2,2f)
            }));
        }

        [Test]
        public void FrameInputSetComparer_DifferentOrderIsEqual_ContentMismatchIsDifferent()
        {
            var comparer = new FrameInputSetComparer();

            FrameInputSet a = new FrameInputSet(100, new[]
            {
                CreateInput(100,1,1f),
                CreateInput(100,2,-1f)
            });

            FrameInputSet b = new FrameInputSet(100, new[]
            {
                CreateInput(100,2,-1f),
                CreateInput(100,1,1f)
            });

            FrameInputSet c = new FrameInputSet(100, new[]
            {
                CreateInput(100,1,1f),
                CreateInput(100,2,1f)
            });

            Assert.IsTrue(comparer.IsEqual(a, b));
            Assert.IsFalse(comparer.IsEqual(a, c));
        }

        [Test]
        public void FrameInputSetApplier_TwoPlayers_ApplyToCorrectEntities()
        {
            var world = new World { EnableSystemProfile = false };

            try
            {
                Entity player1 = world.CreateEntity();
                Entity player2 = world.CreateEntity();

                var applier = new FrameInputSetApplier();
                applier.RegisterPlayer(1, player1);
                applier.RegisterPlayer(2, player2);

                FrameInputSet input = new FrameInputSet(100, new[]
                {
                    CreateInput(100,2,-1f),
                    CreateInput(100,1,1f)
                });

                applier.Apply(world, input);

                Assert.IsTrue(world.TryGetComponent(player1, out PlayerInputSnapshotComponent input1));
                Assert.IsTrue(world.TryGetComponent(player2, out PlayerInputSnapshotComponent input2));

                Assert.AreEqual(100, input1.inputFrame);
                Assert.AreEqual(1, input1.playerID);
                Assert.AreEqual(1f, input1.moveX);

                Assert.AreEqual(100, input2.inputFrame);
                Assert.AreEqual(2, input2.playerID);
                Assert.AreEqual(-1f, input2.moveX);
            }
            finally
            {
                world.Dispose();
            }
        }

        [Test]
        public void FrameInputSetApplier_MissingRegisteredPlayerInput_IsRejected()
        {
            var world = new World { EnableSystemProfile = false };

            try
            {
                Entity player1 = world.CreateEntity();
                Entity player2 = world.CreateEntity();

                var applier = new FrameInputSetApplier();
                applier.RegisterPlayer(1, player1);
                applier.RegisterPlayer(2, player2);

                FrameInputSet input = new FrameInputSet(100, new[]
                {
                    CreateInput(100,1,1f)
                });

                Assert.Throws<InvalidOperationException>(() => applier.Apply(world, input));
            }
            finally
            {
                world.Dispose();
            }
        }

        private static PlayerInputSnapshot CreateInput(int frame, int playerID, float moveX)
        {
            return new PlayerInputSnapshot(frame, playerID)
            {
                moveX = moveX,
                moveY = playerID
            };
        }
    }
}