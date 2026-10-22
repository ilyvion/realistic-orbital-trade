using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// With two traders in orbit, goods already sold to one aren't offered to the other, whether the
// shuttle is still on its way, waiting, or partly loaded.
[TestFixture(TestType.Playing)]
internal sealed class SecondTraderEndToEndTests
{
    private const int SteelSoldInFull = 40;
    private const int SteelOnMap = 100;
    private const int SteelSoldToFirst = 60;
    private const int SteelOnMapWhileLoading = 500;
    private const int SteelSoldWhileLoading = 400;

    [Test]
    public static IEnumerator GoodsSoldToOneTraderArentOfferedToAnother()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, SteelSoldInFull);
        var secondTrader = scene.AddTrader();

        yield return scene.SellAndWaitForLanding(steel, SteelSoldInFull);

        Expect.AreEqual(
            0,
            scene.OfferedForSaleTo(secondTrader, steel),
            "Steel sold to the first trader was offered to the second"
        );
    }

    [Test]
    public static IEnumerator OnlyTheUnsoldPartOfAStackIsOfferedAndSold()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, SteelOnMap);
        scene.StopColonistsHauling();
        var secondTrader = scene.AddTrader();

        yield return scene.SellAndWaitForLanding(steel, SteelSoldToFirst);

        const int Unsold = SteelOnMap - SteelSoldToFirst;
        Expect.AreEqual(
            Unsold,
            scene.OfferedForSaleTo(secondTrader, steel),
            "The second trader wasn't offered exactly the steel left unsold"
        );

        scene.SellTo(secondTrader, steel, Unsold);
        var agreements = new[]
        {
            scene.TradeAgreement,
            secondTrader.GetData().activeTradeAgreement,
        };
        Expect.IsNotNull(agreements[1], "No trade agreement with the second trader");
        foreach (var stack in scene.Map.listerThings.ThingsOfDef(steel))
        {
            Expect.LessThanOrEqualTo(
                agreements.Sum(agreement => agreement?.ReservedCountFor(stack) ?? 0),
                stack.stackCount,
                "Both traders were sold the same steel"
            );
        }
    }

    [Test]
    public static IEnumerator GoodsBeingLoadedArentOfferedToAnotherTrader()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, SteelOnMapWhileLoading);
        var secondTrader = scene.AddTrader();

        yield return scene.SellAndWaitForLanding(steel, SteelSoldWhileLoading);
        yield return scene.WaitForPartialLoad();

        Expect.AreEqual(
            SteelOnMapWhileLoading - SteelSoldWhileLoading,
            scene.OfferedForSaleTo(secondTrader, steel),
            "The second trader wasn't offered exactly the steel left unsold"
        );
    }
}
