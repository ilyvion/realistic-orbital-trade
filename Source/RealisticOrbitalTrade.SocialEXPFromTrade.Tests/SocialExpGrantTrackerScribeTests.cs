using DevTools.Testing;

namespace RealisticOrbitalTrade.SocialEXPFromTrade.Tests;

// Regression suite: SocialExpGrantTracker.ExposeData() read its four persisted lists via
// Scribe_Collections.Look and then, in its PostLoadInit pass, indexed into them without checking
// for null. Verse's Scribe_Collections.Look sets a list to null (rather than leaving its
// pre-existing empty-list default) when the save being loaded has no matching XML node for it -
// which is exactly what happens the first time a save made before this integration persisted
// anything is loaded with the integration active (e.g. the integration mod, or Social Experience
// from Trade itself, gets added to an existing save that already has an in-flight ROT trade).
// That crashed every such load with a NullReferenceException. These tests drive a real Scribe
// save/load cycle (via ilyvion.Laboratory's CustomStream Scribe helpers, so no on-disk save file
// is needed) to guard against that regressing.
[TestFixture(TestType.MainMenu)]
internal sealed class SocialExpGrantTrackerScribeTests
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

    private sealed class EmptyHarness : IExposable
    {
        public void ExposeData() { }
    }

    private sealed class SocialExpGrantTrackerHarness : IExposable
    {
        public void ExposeData() => SocialExpGrantTracker.ExposeData();
    }

    private static void RoundTrip<TSave, TLoad>(TSave saveTarget, TLoad loadTarget)
        where TSave : IExposable
        where TLoad : IExposable
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
    public static void LoadingASaveWithNoPersistedPendingDataDoesNotThrow()
    {
        SocialExpGrantTracker.Reset();

        RoundTrip(new EmptyHarness(), new SocialExpGrantTrackerHarness());

        SocialExpGrantTracker.Reset();
    }
}
