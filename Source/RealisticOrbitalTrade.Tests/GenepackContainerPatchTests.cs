using DevTools.Testing;
using RealisticOrbitalTrade.Patch;

namespace RealisticOrbitalTrade.Tests;

[TestFixture(TestType.Playing)]
[LoadIfModsActive("ludeon.rimworld.biotech")]
internal sealed class GenepackContainerPatchTests
{
    private static readonly AccessTools.FieldRef<ListerThings, List<IHaulSource>> HaulSourcesRef =
        AccessTools.FieldRefAccess<ListerThings, List<IHaulSource>>("haulSources");

    private static Thing SpawnGenebank(Map map)
    {
        var cell = CellFinder.RandomClosewalkCellNear(map.Center + new IntVec3(6, 0, 6), map, 10);
        return GenSpawn.Spawn(
            ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("GeneBank")),
            cell,
            map
        );
    }

    [Test]
    public static void SpawningRegistersTheHaulSourceOnceAndDespawningUnregistersIt()
    {
        var map = Find.CurrentMap;
        var genebank = SpawnGenebank(map);
        try
        {
            var comp = genebank.TryGetComp<CompGenepackContainer>();
            Expect.IsTrue(GenepackContainerHaulSourceRegistry.IsRegistered(comp));
            Expect.IsTrue(
                GenepackContainerHaulSourceRegistry.TryGetHaulSource(comp, out var source)
            );
            Expect.AreEqual(1, HaulSourcesRef(map.listerThings).Count(s => s == source));

            var cell = genebank.Position;
            genebank.DeSpawn();
            Expect.AreEqual(0, HaulSourcesRef(map.listerThings).Count(s => s == source));

            _ = GenSpawn.Spawn(genebank, cell, map);
            Expect.AreEqual(1, HaulSourcesRef(map.listerThings).Count(s => s == source));
            Expect.IsTrue(
                GenepackContainerHaulSourceRegistry.TryGetHaulSource(comp, out var again)
            );
            Expect.AreEqual(source, again);
        }
        finally
        {
            genebank.Destroy();
        }
    }

#if v1_7_OR_GREATER
    // On CoreCLR the JIT can inline these small patched methods into their callers, so the
    // patches are checked through vanilla callers instead of being called directly.
    [Test]
    public static void VanillaCallersSeeAGenepackInAGenebankAsHaulableAndStored()
    {
        var map = Find.CurrentMap;
        var genebank = SpawnGenebank(map);
        try
        {
            var genepack = ThingMaker.MakeThing(RimWorld.ThingDefOf.Genepack);
            Expect.IsTrue(
                genebank.TryGetComp<CompGenepackContainer>().innerContainer.TryAdd(genepack)
            );

            Expect.AreEqual(
                genepack,
                GenClosest.ClosestThing_Global(genebank.Position, new List<Thing> { genepack })
            );
            Expect.IsTrue(genepack.IsInAnyStorage());
        }
        finally
        {
            genebank.Destroy();
        }
    }
#endif

    [Test]
    public static void UnregisteringAContainerThatWasNeverRegisteredDoesNothing()
    {
        var comp = new CompGenepackContainer();
        GenepackContainerHaulSourceRegistry.Unregister(comp, Find.CurrentMap);
        Expect.IsFalse(GenepackContainerHaulSourceRegistry.IsRegistered(comp));
    }

    [Test]
    public static void RemainingCountIsRequiredMinusLoadedButNeverNegative()
    {
        Expect.AreEqual(
            7,
            Rimworld_CompShuttle_CompInspectStringExtra_RemainingAmounts.RemainingCount(10, 3)
        );
        Expect.AreEqual(
            0,
            Rimworld_CompShuttle_CompInspectStringExtra_RemainingAmounts.RemainingCount(10, 10)
        );
        Expect.AreEqual(
            0,
            Rimworld_CompShuttle_CompInspectStringExtra_RemainingAmounts.RemainingCount(10, 15)
        );
    }
}
