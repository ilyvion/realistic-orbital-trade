using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// Regression guard for the float-menu/TryTakeOrderedJob gap described in the "player orders a
// pawn to pick up a thing sold to a trader" bug report: outside a hauling scan/job's
// TradeHaulReservationContext.Scope (i.e. no exemption in play), a thing a trade agreement has
// reserved must come back blocked so callers can reject the order up front instead of letting it
// fail deep inside ReservationManager.Reserve.
[TestFixture(TestType.MainMenu)]
internal sealed class TradeHaulReservationContextTests
{
    private static int _nextThingId;

    private static Pawn NewThing(int stackCount = 100) =>
        new()
        {
            stackCount = stackCount,
            def = RimWorld.ThingDefOf.Steel,
            thingIDNumber = _nextThingId++,
        };

    [Test]
    public static void IsReservedByATradeIsFalseWithNoTradeAgreements()
    {
        var thing = NewThing();

        Expect.AreEqual(false, TradeHaulReservationContext.IsReservedByATrade(thing, null));
    }

    [Test]
    public static void IsReservedByATradeIsFalseWhenNoAgreementReservedTheThing()
    {
        var thing = NewThing();
        var otherThing = NewThing();
        var tradeAgreement = new TradeAgreement();
        tradeAgreement.AddReservation(otherThing, 5);

        Expect.AreEqual(
            false,
            TradeHaulReservationContext.IsReservedByATrade(thing, [tradeAgreement])
        );
    }

    [Test]
    public static void IsReservedByATradeIsTrueWhenAnAgreementReservedTheThing()
    {
        var thing = NewThing();
        var tradeAgreement = new TradeAgreement();
        tradeAgreement.AddReservation(thing, 5);

        Expect.AreEqual(
            true,
            TradeHaulReservationContext.IsReservedByATrade(thing, [tradeAgreement])
        );
    }

    [Test]
    public static void IsBlockedIsFalseWhenNothingReservedTheThing()
    {
        var claimant = NewThing();
        var thing = NewThing();

        Expect.AreEqual(false, TradeHaulReservationContext.IsBlocked(claimant, thing, null));
    }

    // The exact scenario from the bug report: a player float-menu-orders a pawn to pick up a
    // thing that's been sold to a trader, entirely outside any hauling scan/job, so there's no
    // active TradeHaulReservationContext.Scope to exempt the claimant.
    [Test]
    public static void IsBlockedIsTrueWhenReservedAndClaimantIsNotMidHaulScan()
    {
        var claimant = NewThing();
        var thing = NewThing();
        var tradeAgreement = new TradeAgreement();
        tradeAgreement.AddReservation(thing, 5);

        Expect.AreEqual(
            true,
            TradeHaulReservationContext.IsBlocked(claimant, thing, [tradeAgreement])
        );
    }
}
