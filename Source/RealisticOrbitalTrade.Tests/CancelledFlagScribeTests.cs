using RealisticOrbitalTrade.Comps;
using RealisticOrbitalTrade.ShipJobs;
using RimTestRedux;

namespace RealisticOrbitalTrade.Tests;

// Regression suite: `CompTradeShuttle.cancelled` and `ShipJob_CancelLoad.done` were
// never scribed, so saving/loading mid-cancellation reset both flags to false, causing CompTick
// to re-initiate loading and reviving the renegotiation UI on a dead trade. These tests drive a
// real Scribe save/load cycle (via ilyvion.Laboratory's CustomStream Scribe helpers, so no
// on-disk save file is needed) and assert the flags survive the round trip.
[TestSuite]
internal static class CancelledFlagScribeTests
{
    /// <summary>
    /// A <see cref="Stream"/> wrapper whose dispose is a no-op, so the wrapped stream survives
    /// Scribe's own save/load lifecycle (which closes whatever stream it was handed) and can
    /// still be read from afterwards.
    /// </summary>
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
    public static void CompTradeShuttleCancelledSurvivesSaveLoadWhenTrue()
    {
        var saveHarness = new CompTradeShuttleHarness();
        saveHarness.Comp.cancelled = true;
        var loadHarness = new CompTradeShuttleHarness();

        RoundTrip(saveHarness, loadHarness);

        Assert.That(loadHarness.Comp.cancelled).Is.True();
    }

    [Test]
    public static void CompTradeShuttleCancelledSurvivesSaveLoadWhenFalse()
    {
        var saveHarness = new CompTradeShuttleHarness();
        saveHarness.Comp.cancelled = false;
        var loadHarness = new CompTradeShuttleHarness();

        RoundTrip(saveHarness, loadHarness);

        Assert.That(loadHarness.Comp.cancelled).Is.False();
    }

    [Test]
    public static void ShipJobCancelLoadDoneSurvivesSaveLoadWhenTrue()
    {
        var saveJob = new ShipJob_CancelLoad { done = true };
        var loadJob = new ShipJob_CancelLoad();

        RoundTrip(saveJob, loadJob);

        Assert.That(loadJob.done).Is.True();
    }
}
