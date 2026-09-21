using DevTools.Testing;
using RealisticOrbitalTrade.Comps;

namespace RealisticOrbitalTrade.Tests;

// CompTradeShuttle.ComputeRemaining drives departure and the "required" label by subtracting the
// loaded stacks from the required entries.
[TestFixture(TestType.Playing)]
internal sealed class ComputeRemainingTests
{
    private static readonly ThingDef Silver = RimWorld.ThingDefOf.Silver;
    private static readonly ThingDef Steel = RimWorld.ThingDefOf.Steel;

    private static Thing Stack(ThingDef def, int count)
    {
        var thing = ThingMaker.MakeThing(def);
        thing.stackCount = count;
        return thing;
    }

    private static ThingDefCountWithRequirements Entry(ThingDef def, int count) =>
        new() { def = def, count = count };

    [Test]
    public static void StackLargerThanEntryCarriesLeftoverToLaterEntry()
    {
        var remaining = CompTradeShuttle.ComputeRemaining(
            [Entry(Silver, 3), Entry(Silver, 5)],
            [Stack(Silver, 6)]
        );

        Expect.AreEqual(0, remaining[0].count);
        Expect.AreEqual(2, remaining[1].count);
    }

    [Test]
    public static void EntryLargerThanStackIsReducedByTheStack()
    {
        var remaining = CompTradeShuttle.ComputeRemaining([Entry(Silver, 10)], [Stack(Silver, 4)]);

        Expect.AreEqual(6, remaining[0].count);
    }

    [Test]
    public static void TwoStacksBothCountTowardOneEntry()
    {
        var remaining = CompTradeShuttle.ComputeRemaining(
            [Entry(Silver, 10)],
            [Stack(Silver, 4), Stack(Silver, 5)]
        );

        Expect.AreEqual(1, remaining[0].count);
    }

    [Test]
    public static void PawnsAreIgnored()
    {
        var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);

        var remaining = CompTradeShuttle.ComputeRemaining([Entry(Silver, 3)], [pawn]);

        Expect.AreEqual(3, remaining[0].count);
    }

    [Test]
    public static void UnmatchedLoadedThingChangesNothing()
    {
        var remaining = CompTradeShuttle.ComputeRemaining([Entry(Silver, 3)], [Stack(Steel, 50)]);

        Expect.AreEqual(3, remaining[0].count);
    }

    [Test]
    public static void OnlyTheEntryWithTheLoadedStuffIsReduced()
    {
        var cloth = RimWorld.ThingDefOf.Cloth;
        var parka = RimWorld.ThingDefOf.Apparel_Parka;
        var thing = ThingMaker.MakeThing(parka, cloth);
        List<ThingDefCountWithRequirements> required =
        [
            new()
            {
                def = parka,
                stuffDef = RimWorld.ThingDefOf.Leather_Plain,
                count = 1,
            },
            new()
            {
                def = parka,
                stuffDef = cloth,
                count = 1,
            },
        ];

        var remaining = CompTradeShuttle.ComputeRemaining(required, [thing]);

        Expect.AreEqual(1, remaining[0].count);
        Expect.AreEqual(0, remaining[1].count);
    }

    [Test]
    public static void RequiredListIsNotModified()
    {
        List<ThingDefCountWithRequirements> required = [Entry(Silver, 5)];

        _ = CompTradeShuttle.ComputeRemaining(required, [Stack(Silver, 5)]);

        Expect.AreEqual(5, required[0].count);
    }
}

[TestFixture(TestType.Playing)]
internal sealed class IsRequiredTests
{
    private static CompTradeShuttle Comp(bool isToTrader) =>
        new()
        {
            isToTrader = isToTrader,
            requiredSpecificItems = [new() { def = RimWorld.ThingDefOf.Silver, count = 5 }],
        };

    [Test]
    public static void ToTraderShuttleClaimsMatchingThing()
    {
        Expect.IsTrue(Comp(true).IsRequired(ThingMaker.MakeThing(RimWorld.ThingDefOf.Silver)));
        Expect.IsFalse(Comp(true).IsRequired(ThingMaker.MakeThing(RimWorld.ThingDefOf.Steel)));
    }

    [Test]
    public static void ToPlayerShuttleClaimsNothing() =>
        Expect.IsFalse(Comp(false).IsRequired(ThingMaker.MakeThing(RimWorld.ThingDefOf.Silver)));
}
