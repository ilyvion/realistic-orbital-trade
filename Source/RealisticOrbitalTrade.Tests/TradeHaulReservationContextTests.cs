using RimTestRedux;

namespace RealisticOrbitalTrade.Tests;

// Regression guard for the float-menu/TryTakeOrderedJob gap described in the "player orders a
// pawn to pick up a thing sold to a trader" bug report: outside a hauling scan/job's
// TradeHaulReservationContext.Scope (i.e. no exemption in play), a thing a trade agreement has
// reserved must come back blocked so callers can reject the order up front instead of letting it
// fail deep inside ReservationManager.Reserve.
[TestSuite]
internal static class TradeHaulReservationContextTests
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

        Assert.That(TradeHaulReservationContext.IsReservedByATrade(thing, null)).Is.EqualTo(false);
    }

    [Test]
    public static void IsReservedByATradeIsFalseWhenNoAgreementReservedTheThing()
    {
        var thing = NewThing();
        var otherThing = NewThing();
        var tradeAgreement = new TradeAgreement();
        tradeAgreement.AddReservation(otherThing, 5);

        Assert
            .That(TradeHaulReservationContext.IsReservedByATrade(thing, [tradeAgreement]))
            .Is.EqualTo(false);
    }

    [Test]
    public static void IsReservedByATradeIsTrueWhenAnAgreementReservedTheThing()
    {
        var thing = NewThing();
        var tradeAgreement = new TradeAgreement();
        tradeAgreement.AddReservation(thing, 5);

        Assert
            .That(TradeHaulReservationContext.IsReservedByATrade(thing, [tradeAgreement]))
            .Is.EqualTo(true);
    }

    [Test]
    public static void IsBlockedIsFalseWhenNothingReservedTheThing()
    {
        var claimant = NewThing();
        var thing = NewThing();

        Assert.That(TradeHaulReservationContext.IsBlocked(claimant, thing, null)).Is.EqualTo(false);
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

        Assert
            .That(TradeHaulReservationContext.IsBlocked(claimant, thing, [tradeAgreement]))
            .Is.EqualTo(true);
    }
}
