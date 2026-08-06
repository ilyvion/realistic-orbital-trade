using RimTestRedux;

namespace RealisticOrbitalTrade.Tests;

// Regression guard: RealisticOrbitalTradeGameComponent.AmendsQuest used to be recomputed by
// scanning the full quest list every time it was read (once per tick per orbital trade ship).
// It's now set directly by QuestNode_Root_TradeShipMakeAmends and only cleared lazily (via
// Utils.GetLiveOrClear) once the quest goes historical. RimTest Redux runs its suite before any
// game/save is loaded, so Quest/Find.* can't be exercised here (Quest.MakeRaw() needs
// Find.TickManager, which is null pre-load) — these tests instead cover the extracted, pure
// caching helper directly.
[TestSuite]
internal static class AmendsQuestTests
{
    private sealed class Item
    {
        public bool Done;
    }

    [Test]
    public static void ReturnsCacheWhileNotDone()
    {
        Item? cache = new();

        var result = Utils.GetLiveOrClear(ref cache, i => i.Done);

        Assert.That(ReferenceEquals(result, cache)).Is.True();
        Assert.That(cache is null).Is.False();
    }

    [Test]
    public static void ClearsCacheOnceDone()
    {
        Item? cache = new() { Done = true };

        var result = Utils.GetLiveOrClear(ref cache, i => i.Done);

        Assert.That(result is null).Is.True();
        Assert.That(cache is null).Is.True();
    }

    [Test]
    public static void ReturnsNullWhenCacheAlreadyNull()
    {
        Item? cache = null;

        var result = Utils.GetLiveOrClear(ref cache, i => i.Done);

        Assert.That(result is null).Is.True();
    }
}
