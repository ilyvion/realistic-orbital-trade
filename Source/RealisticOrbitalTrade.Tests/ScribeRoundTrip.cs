namespace RealisticOrbitalTrade.Tests;

/// <summary>
/// Drives a real Scribe save/load cycle through in-memory streams (via ilyvion.Laboratory's
/// CustomStream Scribe helpers), so no on-disk save file is needed.
/// </summary>
internal static class ScribeRoundTrip
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

    /// <summary>
    /// Saves <paramref name="saveTarget"/> and loads the result into <paramref name="loadTarget"/>.
    /// </summary>
    public static void Run<TSave, TLoad>(TSave saveTarget, TLoad loadTarget)
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
            Scribe.loader.crossRefs.RegisterForCrossRefResolve(loadTarget);
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
}
