using DevTools.Testing;
using RealisticOrbitalTrade.Comps;

namespace RealisticOrbitalTrade.Tests;

// ThingDefCountWithRequirements.Matches is the identity predicate behind every required-cargo
// decision.
[TestFixture(TestType.Playing)]
internal sealed class ThingDefCountWithRequirementsMatchesTests
{
    private static Thing MakeThing(ThingDef def, ThingDef? stuff = null, int hitPoints = -1)
    {
        var thing = ThingMaker.MakeThing(def, stuff);
        if (hitPoints >= 0)
        {
            thing.HitPoints = hitPoints;
        }
        return thing;
    }

    private static Thing MakeQualityThing(ThingDef def, QualityCategory quality)
    {
        var thing = ThingMaker.MakeThing(def, RimWorld.ThingDefOf.Cloth);
        thing.TryGetComp<CompQuality>().SetQuality(quality, null);
        return thing;
    }

    [Test]
    public static void ZeroCountNeverMatches()
    {
        var required = new ThingDefCountWithRequirements
        {
            def = RimWorld.ThingDefOf.Silver,
            count = 0,
        };

        Expect.IsFalse(required.Matches(MakeThing(RimWorld.ThingDefOf.Silver)));
    }

    [Test]
    public static void SameDefMatchesAndOtherDefDoesNot()
    {
        var required = new ThingDefCountWithRequirements
        {
            def = RimWorld.ThingDefOf.Silver,
            count = 5,
        };

        Expect.IsTrue(required.Matches(MakeThing(RimWorld.ThingDefOf.Silver)));
        Expect.IsFalse(required.Matches(MakeThing(RimWorld.ThingDefOf.Steel)));
    }

    [Test]
    public static void WrongStuffDoesNotMatch()
    {
        var required = new ThingDefCountWithRequirements
        {
            def = RimWorld.ThingDefOf.Apparel_Parka,
            stuffDef = RimWorld.ThingDefOf.Cloth,
            count = 1,
        };

        Expect.IsTrue(
            required.Matches(
                MakeThing(RimWorld.ThingDefOf.Apparel_Parka, RimWorld.ThingDefOf.Cloth)
            )
        );
        Expect.IsFalse(
            required.Matches(
                MakeThing(RimWorld.ThingDefOf.Apparel_Parka, RimWorld.ThingDefOf.Leather_Plain)
            )
        );
    }

    [Test]
    public static void InnerThingRequirementNeedsAMinifiedWrapper()
    {
        var def = RimWorld.ThingDefOf.Apparel_Parka;
        var required = new ThingDefCountWithRequirements
        {
            def = def,
            count = 1,
            isInnerThing = true,
        };
        var inner = MakeThing(def, RimWorld.ThingDefOf.Cloth);
        required.stuffDef = RimWorld.ThingDefOf.Cloth;

        Expect.IsFalse(required.Matches(inner));
    }

    [Test]
    public static void MinifiedWrapperIsNotUnwrappedWhenNotInnerThing()
    {
        var def = RimWorld.ThingDefOf.Battery;
        var inner = MakeThing(def);
        var minified = inner.MakeMinified();

        var wantsInner = new ThingDefCountWithRequirements
        {
            def = def,
            count = 1,
            isInnerThing = true,
        };
        var wantsWrapper = new ThingDefCountWithRequirements
        {
            def = def,
            count = 1,
            isInnerThing = false,
        };

        Expect.IsTrue(wantsInner.Matches(minified));
        Expect.IsFalse(wantsWrapper.Matches(minified));
    }

    [Test]
    public static void QualityOnlyMattersWhenRequired()
    {
        var def = RimWorld.ThingDefOf.Apparel_Parka;
        var good = MakeQualityThing(def, QualityCategory.Good);
        var qualityIgnored = new ThingDefCountWithRequirements
        {
            def = def,
            stuffDef = RimWorld.ThingDefOf.Cloth,
            count = 1,
            quality = QualityCategory.Poor,
        };
        var qualityRequired = qualityIgnored.WithCount(1);
        qualityRequired.hasQuality = true;

        Expect.IsTrue(qualityIgnored.Matches(good));
        Expect.IsFalse(qualityRequired.Matches(good));
        qualityRequired.quality = QualityCategory.Good;
        Expect.IsTrue(qualityRequired.Matches(good));
    }

    [Test]
    public static void HitPointsAtToleranceBoundaryMatchAndBeyondDoNot()
    {
        var def = RimWorld.ThingDefOf.Steel;
        var savedTolerance = Settings._hitPointsTolerancePercent;
        Settings._hitPointsTolerancePercent = 0;
        try
        {
            // The allowance is at least 5 hit points.
            var required = new ThingDefCountWithRequirements
            {
                def = def,
                count = 1,
                healthAffectsPrice = true,
                hitPoints = 50,
                maxHitPoints = 100,
            };

            Expect.IsTrue(required.Matches(MakeThing(def, hitPoints: 55)));
            Expect.IsFalse(required.Matches(MakeThing(def, hitPoints: 56)));
            Expect.IsTrue(required.Matches(MakeThing(def, hitPoints: 45)));
            Expect.IsFalse(required.Matches(MakeThing(def, hitPoints: 44)));
        }
        finally
        {
            Settings._hitPointsTolerancePercent = savedTolerance;
        }
    }

    [Test]
    public static void RequiredThingMatchesOnlyThatReference()
    {
        var def = RimWorld.ThingDefOf.Silver;
        var wanted = MakeThing(def);
        var other = MakeThing(def);
        var required = new ThingDefCountWithRequirements
        {
            def = def,
            count = 1,
            requiredThing = wanted,
        };

        Expect.IsTrue(required.Matches(wanted));
        Expect.IsFalse(required.Matches(other));
    }
}
