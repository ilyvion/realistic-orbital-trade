using RimTestRedux;

namespace RealisticOrbitalTrade.Tests;

// Regression guard: destroying an item/pawn already sold to a trader left a dangling reference in
// TradeAgreement.thingsSoldToTrader/pawnsSoldToTrader. In the same session the reference stayed
// non-null but Destroyed, producing a "ghost" tradeable that crashed RimWorld's own
// Tradeable.TraderWillTrade when the renegotiation dialog opened. After a save/load, a destroyed
// thing's Scribe_References reference resolved to null, crashing TradeAgreement.AllTradeables
// (and thus the renegotiation job) with a NullReferenceException before the dialog could open.
[TestSuite]
internal static class SoldItemDestroyedTests
{
    // Thing.Destroy() dereferences Find.WorldPawns (via Pawn.Discard) among other live-game
    // state, so it can't run outside an actual loaded game. Thing.Destroyed is just a read of the
    // private mapIndexOrState field though, so it's faked directly here rather than routing
    // through the real (heavily side-effecting) Destroy() method.
    private static readonly FieldInfo MapIndexOrStateField = typeof(Thing).GetField(
        "mapIndexOrState",
        BindingFlags.NonPublic | BindingFlags.Instance
    );

    private static void MarkDestroyed(Thing thing) =>
        MapIndexOrStateField.SetValue(thing, (sbyte)-2);

    [Test]
    public static void IsSoldThingLostIsFalseForALiveThing()
    {
        var thingCount = new ThingCountClass(new Pawn(), 1);

        Assert.That(TradeAgreement.IsSoldThingLost(thingCount)).Is.False();
    }

    [Test]
    public static void IsSoldThingLostIsTrueForANullThingReference()
    {
        var thingCount = new ThingCountClass(null, 1);

        Assert.That(TradeAgreement.IsSoldThingLost(thingCount)).Is.True();
    }

    [Test]
    public static void IsSoldThingLostIsTrueForADestroyedThing()
    {
        var thing = new Pawn();
        MarkDestroyed(thing);
        var thingCount = new ThingCountClass(thing, 1);

        Assert.That(TradeAgreement.IsSoldThingLost(thingCount)).Is.True();
    }

    [Test]
    public static void IsSoldPawnLostIsFalseForALivePawn() =>
        Assert.That(TradeAgreement.IsSoldPawnLost(new Pawn())).Is.False();

    [Test]
    public static void IsSoldPawnLostIsTrueForANullPawn() =>
        Assert.That(TradeAgreement.IsSoldPawnLost(null)).Is.True();

    [Test]
    public static void IsSoldPawnLostIsTrueForADestroyedPawn()
    {
        var pawn = new Pawn();
        MarkDestroyed(pawn);

        Assert.That(TradeAgreement.IsSoldPawnLost(pawn)).Is.True();
    }
}
