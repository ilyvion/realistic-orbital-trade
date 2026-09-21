using DevTools.Testing;
using RealisticOrbitalTrade.Comps;

namespace RealisticOrbitalTrade.Tests;

// CollectItemsOutsideHome lists the spawned items that colonists ignore for lying outside the
// home area and outside any storage.
[TestFixture(TestType.Playing)]
internal sealed class CollectItemsOutsideHomeTests
{
    [Test]
    public static void OnlyLooseItemsOutsideHomeAreCollected()
    {
        var map = Find.CurrentMap;
        var origin = map.Center + new IntVec3(0, 0, -5);
        var homeCell = origin;
        var outsideCell = origin + new IntVec3(4, 0, 0);
        var storedCell = origin + new IntVec3(8, 0, 0);
        var wasHome = map.areaManager.Home[homeCell];
        var wasOutsideHome = map.areaManager.Home[outsideCell];
        var wasStoredHome = map.areaManager.Home[storedCell];
        List<Thing> spawned = [];
        Zone_Stockpile? stockpile = null;
        try
        {
            stockpile = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
            map.zoneManager.RegisterZone(stockpile);
            stockpile.AddCell(storedCell);

            var inHome = Spawn(RimWorld.ThingDefOf.Silver, homeCell, map, spawned);
            var outside = Spawn(RimWorld.ThingDefOf.Silver, outsideCell, map, spawned);
            var inStorage = Spawn(RimWorld.ThingDefOf.Silver, storedCell, map, spawned);

            map.areaManager.Home[homeCell] = true;
            map.areaManager.Home[outsideCell] = false;
            map.areaManager.Home[storedCell] = false;

            List<Thing> collected = [ThingMaker.MakeThing(RimWorld.ThingDefOf.Steel)];
            CompTradeShuttle.CollectItemsOutsideHome(map, collected);

            Expect.IsTrue(collected.Contains(outside));
            Expect.IsFalse(collected.Contains(inHome));
            Expect.IsFalse(collected.Contains(inStorage));
            Expect.IsFalse(collected.Any(t => t.def == RimWorld.ThingDefOf.Steel));
        }
        finally
        {
            foreach (var thing in spawned.Where(t => !t.Destroyed))
            {
                thing.Destroy();
            }
            stockpile?.Delete();
            map.areaManager.Home[homeCell] = wasHome;
            map.areaManager.Home[outsideCell] = wasOutsideHome;
            map.areaManager.Home[storedCell] = wasStoredHome;
        }
    }

    private static Thing Spawn(ThingDef def, IntVec3 cell, Map map, List<Thing> spawned)
    {
        var thing = ThingMaker.MakeThing(def);
        _ = GenSpawn.Spawn(thing, cell, map);
        spawned.Add(thing);
        return thing;
    }
}
