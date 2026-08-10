using RealisticOrbitalTrade.Comps;
using RealisticOrbitalTrade.Quests;
using RimTestRedux;

namespace RealisticOrbitalTrade.Tests;

[TestSuite]
internal static class BuyMoreTimeTests
{
    private sealed class NonClosingStream(Stream inner) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override void Flush() => inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) =>
            inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        public override void SetLength(long value) => inner.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) =>
            inner.Write(buffer, offset, count);
    }

    private sealed class CompTradeShuttleHarness : IExposable
    {
        public readonly CompTradeShuttle Comp = new();

        public void ExposeData() => Comp.PostExposeData();
    }

    private static void RoundTrip<T>(T saveTarget, T loadTarget)
        where T : IExposable
    {
        using var memory = new MemoryStream();
        using (var nonClosing = new NonClosingStream(memory))
        {
            CustomStreamScribeSaver.InitSaving(nonClosing, "root");
            try
            {
                saveTarget.ExposeData();
                Scribe.saver.FinalizeSaving();
            }
            finally
            {
                if (Scribe.mode != LoadSaveMode.Inactive)
                {
                    Scribe.ForceStop();
                }
            }
        }

        memory.Position = 0;
        using (var reader = new StreamReader(memory))
        {
            CustomStreamReaderScribeLoader.InitLoading(reader);
        }
        try
        {
            Scribe.loader.curParent = loadTarget;
            loadTarget.ExposeData();
            Scribe.loader.FinalizeLoading();
        }
        finally
        {
            if (Scribe.mode != LoadSaveMode.Inactive)
            {
                Scribe.ForceStop();
            }
        }
    }

    [Test]
    public static void TimesBoughtMoreTimeSurvivesSaveLoad()
    {
        var saveHarness = new CompTradeShuttleHarness();
        saveHarness.Comp.timesBoughtMoreTime = 3;
        var loadHarness = new CompTradeShuttleHarness();

        RoundTrip(saveHarness, loadHarness);

        Assert.That(loadHarness.Comp.timesBoughtMoreTime).Is.EqualTo(3);
    }

    [Test]
    public static void NextBuyMoreTimeCostGrowsWithPurchaseCount()
    {
        var originalBaseCost = Settings._buyMoreTimeBaseSilverCost;
        var originalGrowthFactor = Settings._buyMoreTimeCostGrowthFactor;
        try
        {
            Settings._buyMoreTimeBaseSilverCost = 200;
            Settings._buyMoreTimeCostGrowthFactor = 2f;

            CompTradeShuttle comp = new() { timesBoughtMoreTime = 0 };
            Assert.That(comp.NextBuyMoreTimeCost).Is.EqualTo(200);

            comp.timesBoughtMoreTime = 1;
            Assert.That(comp.NextBuyMoreTimeCost).Is.EqualTo(400);

            comp.timesBoughtMoreTime = 2;
            Assert.That(comp.NextBuyMoreTimeCost).Is.EqualTo(800);
        }
        finally
        {
            Settings._buyMoreTimeBaseSilverCost = originalBaseCost;
            Settings._buyMoreTimeCostGrowthFactor = originalGrowthFactor;
        }
    }

    // Regression guard: buying more time used to only extend the shuttle's own leave-delay quest
    // timer, leaving the underlying TradeShip's ticksUntilDeparture on its original schedule. The
    // trade ship would then despawn out from under a shuttle still waiting on it, leaving a
    // dangling tradeShip reference that failed to deep-save ("PassingShip_0 is referenced ... but
    // is not deep-saved").
    [Test]
    public static void ExtendDepartureTimeExtendsBothShuttleDelayAndTradeShip()
    {
        var tradeShip = new TradeShip { ticksUntilDeparture = 500 };
        var delayTicks = 500;

        QuestPart_TradeShuttleLeaveDelay.ExtendDepartureTime(ref delayTicks, tradeShip, 1000);

        Assert.That(delayTicks).Is.EqualTo(1500);
        Assert.That(tradeShip.ticksUntilDeparture).Is.EqualTo(1500);
    }
}
