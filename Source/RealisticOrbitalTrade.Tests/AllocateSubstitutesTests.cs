using DevTools.Testing;
using RealisticOrbitalTrade.Comps;

namespace RealisticOrbitalTrade.Tests;

// Regression/coverage guard for TradeAgreement.AllocateSubstitutes: losing one sold Thing of a
// fungible required-cargo entry (plain stackables, or quality/health-tracked items matched by
// ThingDefCountWithRequirements.Matches's def+stuff+quality/hitpoints bucket) shouldn't force a
// renegotiation if the colony still holds enough spare (not already sold-and-reserved through this
// trade) stock to cover the requirement without it - and the replacement stack(s) actually picked
// must be reported so the caller can record them as sold in the lost thing's place, without ever
// drawing more than a candidate's spare capacity.
[TestFixture(TestType.MainMenu)]
internal sealed class AllocateSubstitutesTests
{
    private static int _nextThingId;

    private static Thing NewThing(ThingDef def, int stackCount) =>
        new()
        {
            def = def,
            stackCount = stackCount,
            thingIDNumber = _nextThingId++,
        };

    private static ThingWithComps NewQualityThing(
        ThingDef def,
        QualityCategory quality,
        int stackCount
    )
    {
        ThingWithComps thing = new()
        {
            def = def,
            stackCount = stackCount,
            thingIDNumber = _nextThingId++,
        };
        CompQuality compQuality = new() { parent = thing };
        compQuality.SetQuality(quality, null);
        thing.compQuality = compQuality;
        return thing;
    }

    [Test]
    public static void PlainStackableWithEnoughSpareColonyStockIsSubstitutable()
    {
        var thingDef = new ThingDef { defName = "TestMealDef", healthAffectsPrice = false };
        var destroyed = NewThing(thingDef, 0);
        var replacement = NewThing(thingDef, 10);
        List<ThingDefCount> requiredItems = [new(thingDef, 10)];
        List<(Thing, int)> sendable = [(replacement, 10)];

        var allocation = TradeAgreement.AllocateSubstitutes(
            destroyed,
            10,
            [],
            requiredItems,
            sendable
        );

        Expect.IsFalse(allocation == null);
        Expect.AreEqual(1, allocation!.Count);
        Expect.IsTrue(ReferenceEquals(allocation[0].candidate, replacement));
        Expect.AreEqual(10, allocation[0].count);
    }

    [Test]
    public static void PlainStackableAllocationCanSpanMultipleStacks()
    {
        var thingDef = new ThingDef { defName = "TestMealDef", healthAffectsPrice = false };
        var destroyed = NewThing(thingDef, 0);
        var firstReplacement = NewThing(thingDef, 4);
        var secondReplacement = NewThing(thingDef, 6);
        List<ThingDefCount> requiredItems = [new(thingDef, 10)];
        List<(Thing, int)> sendable = [(firstReplacement, 4), (secondReplacement, 6)];

        var allocation = TradeAgreement.AllocateSubstitutes(
            destroyed,
            10,
            [],
            requiredItems,
            sendable
        );

        Expect.IsFalse(allocation == null);
        Expect.AreEqual(2, allocation!.Count);
        Expect.IsTrue(ReferenceEquals(allocation[0].candidate, firstReplacement));
        Expect.AreEqual(4, allocation[0].count);
        Expect.IsTrue(ReferenceEquals(allocation[1].candidate, secondReplacement));
        Expect.AreEqual(6, allocation[1].count);
    }

    [Test]
    public static void PlainStackableWithInsufficientSpareColonyStockIsNotSubstitutable()
    {
        var thingDef = new ThingDef { defName = "TestMealDef", healthAffectsPrice = false };
        var destroyed = NewThing(thingDef, 0);
        List<ThingDefCount> requiredItems = [new(thingDef, 10)];
        List<(Thing, int)> sendable = [(NewThing(thingDef, 4), 4)];

        var allocation = TradeAgreement.AllocateSubstitutes(
            destroyed,
            10,
            [],
            requiredItems,
            sendable
        );

        Expect.IsTrue(allocation == null);
    }

    [Test]
    public static void PlainStackableWithNoMatchingRequiredEntryIsNotSubstitutable()
    {
        var thingDef = new ThingDef { defName = "TestMealDef", healthAffectsPrice = false };
        var destroyed = NewThing(thingDef, 0);
        List<(Thing, int)> sendable = [(NewThing(thingDef, 10), 10)];

        var allocation = TradeAgreement.AllocateSubstitutes(destroyed, 10, [], [], sendable);

        Expect.IsTrue(allocation == null);
    }

    // A stack already partly committed to this trade (e.g. 1 of its 6 units already reserved
    // elsewhere in the ledger) must only offer up its remaining spare units as a substitute, never
    // its full physical stackCount - otherwise the same physical unit ends up both under its
    // original sold-to-trader entry and a new one.
    [Test]
    public static void PartiallyReservedStockOnlyContributesItsSpareCapacity()
    {
        var thingDef = new ThingDef { defName = "TestMealDef", healthAffectsPrice = false };
        var destroyed = NewThing(thingDef, 0);
        var partiallyReserved = NewThing(thingDef, 6);
        List<ThingDefCount> requiredItems = [new(thingDef, 5)];
        List<(Thing, int)> sendable = [(partiallyReserved, 5)];

        var allocation = TradeAgreement.AllocateSubstitutes(
            destroyed,
            5,
            [],
            requiredItems,
            sendable
        );

        Expect.IsFalse(allocation == null);
        Expect.AreEqual(1, allocation!.Count);
        Expect.IsTrue(ReferenceEquals(allocation[0].candidate, partiallyReserved));
        Expect.AreEqual(5, allocation[0].count);
    }

    // A stack already fully committed to this trade (spare capacity of zero, despite a nonzero
    // physical stackCount) must be skipped entirely in favor of stock that's actually free.
    [Test]
    public static void FullyCommittedStockIsSkippedInFavorOfSpareStock()
    {
        var thingDef = new ThingDef { defName = "TestMealDef", healthAffectsPrice = false };
        var destroyed = NewThing(thingDef, 0);
        var fullyCommitted = NewThing(thingDef, 10);
        var free = NewThing(thingDef, 10);
        List<ThingDefCount> requiredItems = [new(thingDef, 10)];
        List<(Thing, int)> sendable = [(fullyCommitted, 0), (free, 10)];

        var allocation = TradeAgreement.AllocateSubstitutes(
            destroyed,
            10,
            [],
            requiredItems,
            sendable
        );

        Expect.IsFalse(allocation == null);
        Expect.AreEqual(1, allocation!.Count);
        Expect.IsTrue(ReferenceEquals(allocation[0].candidate, free));
        Expect.AreEqual(10, allocation[0].count);
    }

    // Stock that's only fully committed, with nothing else spare anywhere, is a genuine shortfall.
    [Test]
    public static void OnlyFullyCommittedStockIsNotSubstitutable()
    {
        var thingDef = new ThingDef { defName = "TestMealDef", healthAffectsPrice = false };
        var destroyed = NewThing(thingDef, 0);
        var fullyCommitted = NewThing(thingDef, 10);
        List<ThingDefCount> requiredItems = [new(thingDef, 10)];
        List<(Thing, int)> sendable = [(fullyCommitted, 0)];

        var allocation = TradeAgreement.AllocateSubstitutes(
            destroyed,
            10,
            [],
            requiredItems,
            sendable
        );

        Expect.IsTrue(allocation == null);
    }

    [Test]
    public static void QualityTrackedItemWithMatchingQualitySpareStockIsSubstitutable()
    {
        var thingDef = new ThingDef { defName = "TestApparelDef" };
        var destroyed = NewQualityThing(thingDef, QualityCategory.Excellent, 0);
        List<ThingDefCountWithRequirements> requiredSpecificItems =
        [
            new()
            {
                def = thingDef,
                count = 1,
                hasQuality = true,
                quality = QualityCategory.Excellent,
            },
        ];
        var replacement = NewQualityThing(thingDef, QualityCategory.Excellent, 1);
        List<(Thing, int)> sendable = [(replacement, 1)];

        var allocation = TradeAgreement.AllocateSubstitutes(
            destroyed,
            1,
            requiredSpecificItems,
            [],
            sendable
        );

        Expect.IsFalse(allocation == null);
        Expect.AreEqual(1, allocation!.Count);
        Expect.IsTrue(ReferenceEquals(allocation[0].candidate, replacement));
        Expect.AreEqual(1, allocation[0].count);
    }

    [Test]
    public static void QualityTrackedItemWithOnlyDifferentQualityStockIsNotSubstitutable()
    {
        var thingDef = new ThingDef { defName = "TestApparelDef" };
        var destroyed = NewQualityThing(thingDef, QualityCategory.Excellent, 0);
        List<ThingDefCountWithRequirements> requiredSpecificItems =
        [
            new()
            {
                def = thingDef,
                count = 1,
                hasQuality = true,
                quality = QualityCategory.Excellent,
            },
        ];
        List<(Thing, int)> sendable = [(NewQualityThing(thingDef, QualityCategory.Poor, 5), 5)];

        var allocation = TradeAgreement.AllocateSubstitutes(
            destroyed,
            1,
            requiredSpecificItems,
            [],
            sendable
        );

        Expect.IsTrue(allocation == null);
    }

    // Genepacks/xenogerms pin their required-cargo entry to the exact destroyed Thing via
    // requiredThing, so Matches() never matches any other instance sharing the same def - this
    // must never be treated as substitutable even with plenty of same-def stock elsewhere.
    [Test]
    public static void RequiredThingPinnedItemIsNeverSubstitutable()
    {
        var thingDef = new ThingDef { defName = "TestGenepackDef" };
        var destroyed = NewQualityThing(thingDef, QualityCategory.Normal, 0);
        List<ThingDefCountWithRequirements> requiredSpecificItems =
        [
            new()
            {
                def = thingDef,
                count = 1,
                requiredThing = destroyed,
            },
        ];
        List<(Thing, int)> sendable = [(NewThing(thingDef, 10), 10)];

        var allocation = TradeAgreement.AllocateSubstitutes(
            destroyed,
            1,
            requiredSpecificItems,
            [],
            sendable
        );

        Expect.IsTrue(allocation == null);
    }

    // The destroyed Thing's own (already-zeroed) reference must never be picked as its own
    // replacement, even if it somehow still appeared in the sendable pool (e.g. a stale
    // enumeration snapshot).
    [Test]
    public static void DestroyedThingItselfIsNeverPickedAsItsOwnReplacement()
    {
        var thingDef = new ThingDef { defName = "TestMealDef", healthAffectsPrice = false };
        var destroyed = NewThing(thingDef, 0);
        var replacement = NewThing(thingDef, 10);
        List<ThingDefCount> requiredItems = [new(thingDef, 10)];
        List<(Thing, int)> sendable = [(destroyed, 0), (replacement, 10)];

        var allocation = TradeAgreement.AllocateSubstitutes(
            destroyed,
            10,
            [],
            requiredItems,
            sendable
        );

        Expect.IsFalse(allocation == null);
        Expect.AreEqual(1, allocation!.Count);
        Expect.IsTrue(ReferenceEquals(allocation[0].candidate, replacement));
    }
}
