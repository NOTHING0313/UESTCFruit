using BuffSystem;
using ECSFrameWork;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;
using View;

namespace View.EditorTesting
{
    [TestFixture]
    public sealed class BuffTextHudFormatterTests
    {
        [Test]
        public void NullListFormatsNoBuffs()
        {
            BuffTextHudFormatter formatter = new BuffTextHudFormatter();
            Assert.AreEqual("No Buffs", formatter.Format(null));
        }

        [Test]
        public void EmptyListFormatsNoBuffs()
        {
            BuffTextHudFormatter formatter = new BuffTextHudFormatter();
            Assert.AreEqual("No Buffs", formatter.Format(Array.Empty<BuffViewModel>()));
        }

        [Test]
        public void SingleBuffIncludesCoreFields()
        {
            BuffTextHudFormatter formatter = new BuffTextHudFormatter();
            string text = formatter.Format(new[]
            {
                new BuffViewModel(1001, 2, 120, 1, "9001", "Buff 1001")
            });

            Assert.That(text, Does.Contain("[1001]"));
            Assert.That(text, Does.Contain("Stack: 2"));
            Assert.That(text, Does.Contain("Remain: 120"));
            Assert.That(text, Does.Contain("Source: 1"));
            Assert.That(text, Does.Contain("Effect: 9001"));
        }

        [Test]
        public void MultipleBuffsPreserveOrderAndUseMultipleLines()
        {
            BuffTextHudFormatter formatter = new BuffTextHudFormatter();
            string text = formatter.Format(new[]
            {
                new BuffViewModel(1001, 2, 120, 1, "9001", "Buff 1001"),
                new BuffViewModel(1002, 1, 60, 1, "N/A", "Buff 1002")
            });

            Assert.That(text, Does.Contain("Buffs:"));
            Assert.Less(text.IndexOf("[1001]", StringComparison.Ordinal), text.IndexOf("[1002]", StringComparison.Ordinal));
            Assert.That(text, Does.Contain("\n- [1002]"));
        }

        [Test]
        public void FormatterDoesNotExposeRuntimeImplementationDetails()
        {
            BuffTextHudFormatter formatter = new BuffTextHudFormatter();
            string text = formatter.Format(new[]
            {
                new BuffViewModel(1001, 1, 30, 1, "N/A", "Buff 1001")
            });

            Assert.That(text, Does.Not.Contain("CompressedRuntime"));
            Assert.That(text, Does.Not.Contain("EntityPerStack"));
            Assert.That(text, Does.Not.Contain("RuntimeHandle"));
        }

        [Test]
        public void PresenterManualRefreshBeforeInitializeDoesNotThrow()
        {
            GameObject host = new GameObject("BuffTextHudPresenterTestsHost");
            try
            {
                BuffTextHudPresenter presenter = host.AddComponent<BuffTextHudPresenter>();
                Assert.DoesNotThrow(presenter.ManualRefresh);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void PresenterManualRefreshWithoutTextDoesNotThrow()
        {
            GameObject host = new GameObject("BuffTextHudPresenterTestsHost");
            try
            {
                BuffTextHudPresenter presenter = host.AddComponent<BuffTextHudPresenter>();
                presenter.Initialize(new FakeBuffSystem(), new Entity(1, 1));
                Assert.DoesNotThrow(presenter.ManualRefresh);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private sealed class FakeBuffSystem : IBuffSystem
        {
            public void Tick(World world, SimulationContext context) { }
            public void AddBuff(AddBuffCommand command) { }
            public void RemoveBuff(RemoveBuffCommand command) { }
            public void Raise<TEvent>(World world, SimulationContext context, in TEvent gameEvent) where TEvent : struct, IGameEvent { }
            public bool TryGetBuff(Entity target, int configId, Entity source, out BuffViewData data)
            {
                data = default;
                return false;
            }

            public IReadOnlyList<BuffViewData> GetBuffs(Entity target)
            {
                return Array.Empty<BuffViewData>();
            }
        }
    }
}
