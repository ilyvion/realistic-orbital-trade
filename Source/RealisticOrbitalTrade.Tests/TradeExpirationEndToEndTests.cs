using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// The shuttles' departure timer runs out before the goods are all loaded: with nothing aboard the
// trade fails and everything stays put; with part aboard the trader offers to renegotiate down to
// what is loaded and the trade completes on that.
[TestFixture(TestType.Playing)]
internal sealed class TradeExpirationEndToEndTests
{
    private const int SteelToSell = 400;
    private const int TicksBeforeDeparture = 2000;

    private static readonly TradeScene.SettingsSnapshot NoGraceTime = TradeScene
        .SettingsSnapshot
        .Defaults with
    {
        MinTicksUntilDepartureBeforeGraceTime = 0,
    };

    [Test]
    public static IEnumerator ExpiringWithNothingLoadedFailsTheTrade()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap, NoGraceTime);
        scene.PlaceOnMap(steel, SteelToSell);
        scene.ShortenTraderStay(TicksBeforeDeparture);
        scene.StopColonistsHauling();

        yield return scene.SellAndWaitForLanding(steel, SteelToSell);
        yield return scene.WaitForQuestToEnd();
        yield return scene.WaitForShuttlesToLeave();

        scene.ExpectSaleCancelled();
    }

    [Test]
    public static IEnumerator ExpiringWithPartLoadedCompletesTheLoadedPart()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap, NoGraceTime);
        scene.PlaceOnMap(steel, SteelToSell);

        yield return scene.SellAndWaitForLanding(steel, SteelToSell);
        yield return scene.WaitForPartialLoad();
        scene.RunDownDepartureTimer();
        yield return scene.WaitForDepartureRenegotiation();
        scene.AcceptDepartureRenegotiation();
        yield return scene.WaitForQuestToEnd();

        scene.ExpectSaleCompleted();
    }
}
