using DevTools.Testing;
using RealisticOrbitalTrade.Quests;

namespace RealisticOrbitalTrade.Tests;

// A purchase too heavy for one shuttle is delivered by several, one after another, each landing
// once the one before it has left, before the trade finishes. The shuttles' capacity is lowered so
// a modest purchase is heavy enough.
[TestFixture(TestType.Playing)]
internal sealed class MultiShuttleDeliveryEndToEndTests
{
    private const float ShuttleCapacityKg = 20f;
    private const int SteelToBuy = 100;
    private const int SilverOnHand = 1000;

    [Test]
    public static IEnumerator PurchaseTooHeavyForOneShuttleIsDeliveredByShuttlesInTurn()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.LimitShuttleCapacity(ShuttleCapacityKg);
        Expect.GreaterThan(
            SteelToBuy * steel.BaseMass,
            ShuttleCapacityKg,
            "The purchase isn't heavy enough to need a second shuttle"
        );
        Expect.IsTrue(
            scene.HeldByTrader(steel) >= SteelToBuy,
            "The trader doesn't have enough steel to sell"
        );
        scene.PlaceOnMap(scene.SilverDef, SilverOnHand);

        yield return scene.BuyAndWaitForLanding(steel, SteelToBuy);
        if (scene.CurrentSale?.Quest == null)
        {
            yield break;
        }
        var mostDeliveryShuttlesOnMap = 0;
        yield return scene.WaitForQuestToEnd(() =>
            mostDeliveryShuttlesOnMap = Math.Max(
                mostDeliveryShuttlesOnMap,
                scene.DeliveryShuttlesOnMap
            )
        );

        Expect.GreaterThan(
            scene.DeliveryShuttlesSent,
            1,
            "A purchase over the shuttle capacity wasn't delivered by several shuttles"
        );
        Expect.AreEqual(
            1,
            mostDeliveryShuttlesOnMap,
            "The delivery shuttles didn't come one at a time"
        );
        scene.ExpectSaleCompleted();
    }

    [Test]
    public static void NextShareTakesWhatOneShuttleCanCarry()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        List<Thing> things = [MakeStack(steel, 50), MakeStack(steel, 50)];
        var capacity = 30 * steel.BaseMass;

        var share = QuestPart_DeliverBoughtItemsByShuttles.NextShare(things, capacity);

        Expect.AreEqual(30, share.Sum(line => line.count), "The share isn't one shuttle's worth");
    }

    [Test]
    public static void NextShareTakesEverythingWhenSomethingIsTooHeavyForAShuttle()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        List<Thing> things = [MakeStack(steel, 50), MakeStack(steel, 50)];
        var capacity = steel.BaseMass / 2;

        var share = QuestPart_DeliverBoughtItemsByShuttles.NextShare(things, capacity);

        Expect.AreEqual(100, share.Sum(line => line.count), "Not everything was taken");
    }

    private static Thing MakeStack(ThingDef def, int count)
    {
        var thing = ThingMaker.MakeThing(def);
        thing.stackCount = count;
        return thing;
    }
}
