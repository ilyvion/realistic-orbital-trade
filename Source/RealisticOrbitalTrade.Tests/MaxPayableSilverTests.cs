using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// Regression/coverage guard for TradeAgreement.MaxPayableSilver: during a departure
// renegotiation, the currency tradeable's own CountToTransfer is recalculated every frame by
// vanilla's TradeDeal.UpdateCurrencyCount to auto-balance the deal, so it can never itself show
// how much silver was actually already loaded onto the shuttle before the dialog opened. This is
// the pure lookup that value comes from - see Rimworld_Dialog_Trade_DoWindowContents'
// DrawSilverCeilingRow (and its DTI mirror) for where it's surfaced to the player.
[TestFixture(TestType.MainMenu)]
internal sealed class MaxPayableSilverTests
{
    // Thing.Equals/List<Thing>.Contains dereferences def and compares thingIDNumber - see
    // ReservedTraderThingsTests for why every test thing needs a real def and a distinct id.
    private static int _nextThingId;

    private static Thing NewThing(ThingDef def, int stackCount) =>
        new()
        {
            def = def,
            stackCount = stackCount,
            thingIDNumber = _nextThingId++,
        };

    private static Tradeable NewTradeable(ThingDef def, int countToTransfer)
    {
        var thing = NewThing(def, Math.Max(1, Math.Abs(countToTransfer)));
        Tradeable tradeable = new();
        tradeable.AddThing(thing, countToTransfer <= 0 ? Transactor.Colony : Transactor.Trader);
        tradeable.ForceTo(countToTransfer);
        return tradeable;
    }

    [Test]
    public static void ReturnsTheMagnitudeOfAlreadySoldSilver()
    {
        var silver = NewTradeable(RimWorld.ThingDefOf.Silver, -299);
        var steel = NewTradeable(RimWorld.ThingDefOf.Steel, -300);

        var result = TradeAgreement.MaxPayableSilver([steel, silver]);

        Expect.AreEqual(299, result);
    }

    [Test]
    public static void ReturnsZeroWhenNoSilverWasSold()
    {
        var steel = NewTradeable(RimWorld.ThingDefOf.Steel, -300);

        var result = TradeAgreement.MaxPayableSilver([steel]);

        Expect.AreEqual(0, result);
    }

    [Test]
    public static void ReturnsZeroWhenTheColonyIsBuyingSilverInstead()
    {
        // Positive CountToTransfer means silver is flowing to the colony, not away from it - there's
        // nothing already loaded onto the shuttle that would cap what the colony can still pay.
        var silver = NewTradeable(RimWorld.ThingDefOf.Silver, 50);

        var result = TradeAgreement.MaxPayableSilver([silver]);

        Expect.AreEqual(0, result);
    }
}
