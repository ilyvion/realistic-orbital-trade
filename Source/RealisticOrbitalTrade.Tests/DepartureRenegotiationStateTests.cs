using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// Utils.TryBeginDepartureRenegotiation either locks in what is loaded and offers to renegotiate,
// or reports that the trade has to fail; either way it leaves the trade session's trader as it
// found it. Utils.ExplainLostSoldItemsIfNeeded shows its explanation once per renegotiation.
[TestFixture(TestType.Playing)]
internal sealed class DepartureRenegotiationStateTests
{
    private const int SteelToSell = 400;
    private const string FailedSignal = "ROT.Test.DepartureFailed";

    private static int SoldSteel(TradeAgreement agreement) =>
        agreement
            .thingsSoldToTrader.Where(t => t.thing?.def == RimWorld.ThingDefOf.Steel)
            .Sum(t => t.Count);

    [Test]
    public static IEnumerator NoNegotiatorFailsWithoutStartingARenegotiation()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, SteelToSell);
        yield return scene.SellAndWaitForLanding(steel, SteelToSell);
        var agreement = scene.TradeAgreement!;
        var soldBefore = SoldSteel(agreement);
        var sentinel = new FakeTrader();
        agreement.negotiator.DeSpawn();
        TradeSession.trader = sentinel;

        try
        {
            var started = Utils.TryBeginDepartureRenegotiation(agreement, FailedSignal);

            Expect.IsFalse(started, "Renegotiation started without a negotiator");
            Expect.IsFalse(Utils.RenegotiatingBecauseDeparting);
            Expect.IsTrue(ReferenceEquals(sentinel, TradeSession.trader));
            Expect.AreEqual(soldBefore, SoldSteel(agreement));
        }
        finally
        {
            TradeSession.trader = null;
        }
    }

    [Test]
    public static IEnumerator NothingLoadedFailsWithoutTouchingWhatWasSold()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, SteelToSell);
        scene.StopColonistsHauling();
        yield return scene.SellAndWaitForLanding(steel, SteelToSell);
        var agreement = scene.TradeAgreement!;
        var sentinel = new FakeTrader();
        TradeSession.trader = sentinel;

        try
        {
            var started = Utils.TryBeginDepartureRenegotiation(agreement, FailedSignal);

            Expect.IsFalse(started, "Renegotiation started with nothing loaded");
            Expect.IsFalse(Utils.RenegotiatingBecauseDeparting);
            Expect.IsTrue(ReferenceEquals(sentinel, TradeSession.trader));
            Expect.AreEqual(SteelToSell, SoldSteel(agreement));
        }
        finally
        {
            TradeSession.trader = null;
        }
    }

    [Test]
    public static IEnumerator PartLoadedLocksInTheLoadedAmountAndOffersToRenegotiate()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, SteelToSell);
        yield return scene.SellAndWaitForLanding(steel, SteelToSell);
        yield return scene.WaitForPartialLoad();
        var agreement = scene.TradeAgreement!;
        var loaded = scene.LoadedInShuttles(steel);
        var sentinel = new FakeTrader();
        TradeSession.trader = sentinel;

        try
        {
            var started = Utils.TryBeginDepartureRenegotiation(agreement, FailedSignal);

            Expect.IsTrue(started, "Renegotiation didn't start with part of the goods loaded");
            Expect.IsTrue(Utils.RenegotiatingBecauseDeparting);
            Expect.AreEqual(FailedSignal, Utils.DepartureRenegotiationFailedSignal);
            Expect.IsTrue(ReferenceEquals(sentinel, TradeSession.trader));
            Expect.AreEqual(loaded, SoldSteel(agreement));
            Expect.IsNotNull(Find.WindowStack.WindowOfType<Dialog_MessageBox>());
        }
        finally
        {
            TradeSession.trader = null;
        }
    }

    private static int ExplanationDialogs() =>
        Find.WindowStack.Windows.OfType<Dialog_MessageBox>().Count();

    [Test]
    public static IEnumerator LostItemsAreExplainedOncePerRenegotiation()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, SteelToSell);
        yield return scene.SellAndWaitForLanding(steel, SteelToSell);
        var agreement = scene.TradeAgreement!;
        agreement.lostSoldItems.Add(new() { def = steel, count = 5 });
        var dialogsBefore = ExplanationDialogs();

        Utils.BeginRenegotiation(agreement);
        agreement.tradeShip.TryOpenComms(agreement.negotiator);
        var afterOpening = ExplanationDialogs();
        Utils.ExplainLostSoldItemsIfNeeded(agreement, TradeSession.deal);
        var afterSecondCall = ExplanationDialogs();
        Find.WindowStack.WindowOfType<Dialog_Trade>()?.Close(doCloseSound: false);

        Expect.AreEqual(dialogsBefore + 1, afterOpening, "Opening didn't explain the loss once");
        Expect.AreEqual(afterOpening, afterSecondCall, "The loss was explained twice");
    }

    [Test]
    public static IEnumerator NothingLostIsNotExplained()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, SteelToSell);
        yield return scene.SellAndWaitForLanding(steel, SteelToSell);
        var agreement = scene.TradeAgreement!;
        var dialogsBefore = ExplanationDialogs();

        Utils.BeginRenegotiation(agreement);
        agreement.tradeShip.TryOpenComms(agreement.negotiator);
        var afterOpening = ExplanationDialogs();
        Find.WindowStack.WindowOfType<Dialog_Trade>()?.Close(doCloseSound: false);

        Expect.AreEqual(dialogsBefore, afterOpening);
    }

    [Test]
    public static IEnumerator LossesOfADifferentAgreementAreNotExplained()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, SteelToSell);
        yield return scene.SellAndWaitForLanding(steel, SteelToSell);
        var agreement = scene.TradeAgreement!;
        var other = new TradeAgreement(scene.TradeShip, agreement.negotiator, false, false);
        other.lostSoldItems.Add(new() { def = steel, count = 5 });
        Utils.BeginRenegotiation(agreement);
        agreement.tradeShip.TryOpenComms(agreement.negotiator);
        var dialogsBefore = ExplanationDialogs();

        Utils.ExplainLostSoldItemsIfNeeded(other, TradeSession.deal);
        var after = ExplanationDialogs();
        Find.WindowStack.WindowOfType<Dialog_Trade>()?.Close(doCloseSound: false);

        Expect.AreEqual(dialogsBefore, after);
    }
}
