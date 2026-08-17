using ECSFrameWork;
using NUnit.Framework;
using System;

namespace FrameWork.RollBackSystem.Tests
{
    [TestFixture]
    public sealed class FrameInputPredictionNUnitTests
    {
        [Test]
        public void Accumulator_IdenticalDuplicateIsIdempotent_ConflictIsRejected()
        {
            var accumulator = new FrameInputAccumulator(100);

            PlayerInputSnapshot input = CreateInput(100, 1, 1f, 0f);
            PlayerInputSnapshot same = CreateInput(100, 1, 1f, 0f);
            PlayerInputSnapshot conflict = CreateInput(100, 1, -1f, 0f);

            Assert.IsTrue(accumulator.TryAddInput(in input));
            Assert.IsFalse(accumulator.TryAddInput(in same));
            Assert.Throws<InvalidOperationException>(() => accumulator.TryAddInput(in conflict));
            Assert.AreEqual(1, accumulator.Count);
        }

        [Test]
        public void Assembler_AllRealInputs_NoPrediction()
        {
            var assembler = CreateTwoPlayerAssembler();
            var accumulator = new FrameInputAccumulator(100);

            PlayerInputSnapshot player1 = CreateInput(100, 1, 1f, 0f);
            PlayerInputSnapshot player2 = CreateInput(100, 2, -1f, 0f);

            accumulator.TryAddInput(in player2);
            accumulator.TryAddInput(in player1);

            FrameInputAssemblyResult result = assembler.Assemble(accumulator);

            Assert.IsFalse(result.HasPrediction);
            Assert.AreEqual(0, result.PredictedCount);
            Assert.AreEqual(2, result.InputSet.Count);

            Assert.IsTrue(result.InputSet.TryGetInput(1, out PlayerInputSnapshot input1));
            Assert.IsTrue(result.InputSet.TryGetInput(2, out PlayerInputSnapshot input2));

            Assert.AreEqual(1f, input1.moveX);
            Assert.AreEqual(-1f, input2.moveX);
        }

        [Test]
        public void Assembler_MissingWithoutHistory_UsesNeutralPrediction()
        {
            var assembler = CreateTwoPlayerAssembler();
            var accumulator = new FrameInputAccumulator(1);

            PlayerInputSnapshot player1 = CreateInput(1, 1, 1f, 0f);
            accumulator.TryAddInput(in player1);

            FrameInputAssemblyResult result = assembler.Assemble(accumulator);

            Assert.IsTrue(result.HasPrediction);
            Assert.AreEqual(1, result.PredictedCount);
            Assert.IsTrue(result.IsPredicted(2));
            Assert.IsFalse(result.IsPredicted(1));

            Assert.IsTrue(result.InputSet.TryGetInput(2, out PlayerInputSnapshot player2));
            Assert.AreEqual(1, player2.frameNumber);
            Assert.AreEqual(2, player2.playerID);
            Assert.AreEqual(0f, player2.moveX);
            Assert.AreEqual(0f, player2.moveY);
        }

        [Test]
        public void Assembler_MissingWithHistory_RepeatsContinuousStateAndClearsTransientState()
        {
            var assembler = new FrameInputAssembler(new LastKnownPlayerInputPredictionPolicy());
            assembler.RegisterPlayer(1);

            var frame100 = new FrameInputAccumulator(100);

            PlayerInputSnapshot real = new PlayerInputSnapshot(100, 1)
            {
                moveX = 1f,
                moveY = -1f,
                mouseX = 20f,
                mouseY = 30f,
                mouseDeltaX = 4f,
                mouseDeltaY = -3f,
                scrollX = 2f,
                scrollY = -2f
            };

            frame100.TryAddInput(in real);
            assembler.Assemble(frame100);

            var frame101 = new FrameInputAccumulator(101);
            FrameInputAssemblyResult result = assembler.Assemble(frame101);

            Assert.IsTrue(result.IsPredicted(1));
            Assert.IsTrue(result.InputSet.TryGetInput(1, out PlayerInputSnapshot predicted));

            Assert.AreEqual(1f, predicted.moveX);
            Assert.AreEqual(-1f, predicted.moveY);
            Assert.AreEqual(20f, predicted.mouseX);
            Assert.AreEqual(30f, predicted.mouseY);

            Assert.AreEqual(0f, predicted.mouseDeltaX);
            Assert.AreEqual(0f, predicted.mouseDeltaY);
            Assert.AreEqual(0f, predicted.scrollX);
            Assert.AreEqual(0f, predicted.scrollY);
        }

        [Test]
        public void Assembler_OlderLateAuthoritativeInput_DoesNotReplaceNewerPredictionHistory()
        {
            var assembler = new FrameInputAssembler(new LastKnownPlayerInputPredictionPolicy());
            assembler.RegisterPlayer(1);

            PlayerInputSnapshot newer = CreateInput(100, 1, 1f, 0f);
            PlayerInputSnapshot older = CreateInput(99, 1, -1f, 0f);

            assembler.ObserveAuthoritativeInput(in newer);
            assembler.ObserveAuthoritativeInput(in older);

            FrameInputAssemblyResult result = assembler.Assemble(new FrameInputAccumulator(101));

            Assert.IsTrue(result.InputSet.TryGetInput(1, out PlayerInputSnapshot predicted));
            Assert.AreEqual(1f, predicted.moveX);
        }

        [Test]
        public void Assembler_UnexpectedPlayer_IsRejected()
        {
            var assembler = new FrameInputAssembler(new LastKnownPlayerInputPredictionPolicy());
            assembler.RegisterPlayer(1);

            var accumulator = new FrameInputAccumulator(100);
            PlayerInputSnapshot unexpected = CreateInput(100, 2, 1f, 0f);
            accumulator.TryAddInput(in unexpected);

            Assert.Throws<InvalidOperationException>(() => assembler.Assemble(accumulator));
        }

        private static FrameInputAssembler CreateTwoPlayerAssembler()
        {
            var assembler = new FrameInputAssembler(new LastKnownPlayerInputPredictionPolicy());
            assembler.RegisterPlayer(1);
            assembler.RegisterPlayer(2);
            return assembler;
        }

        private static PlayerInputSnapshot CreateInput(int frame, int playerID, float moveX, float moveY)
        {
            return new PlayerInputSnapshot(frame, playerID)
            {
                moveX = moveX,
                moveY = moveY
            };
        }
    }
}