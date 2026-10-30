using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// A sold pawn drops its weapon and inventory onto the map as it boards a trade shuttle, the way a
// pawn sold on the map does.
[TestFixture(TestType.Playing)]
internal sealed class SoldPawnBoardingTests
{
    private const int SteelToSell = 40;
    private const int MedicineCarried = 5;

    private static IEnumerator Landed(TradeScene scene)
    {
        var steel = RimWorld.ThingDefOf.Steel;
        scene.PlaceOnMap(steel, SteelToSell);
        scene.StopColonistsHauling();
        yield return scene.SellAndWaitForLanding(steel, SteelToSell);
    }

    private static (ThingWithComps weapon, Thing medicine) Equip(Pawn pawn)
    {
        pawn.equipment.DestroyAllEquipment();
        pawn.inventory.DestroyAll();
        var weapon = (ThingWithComps)
            ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_Revolver"));
        pawn.equipment.AddEquipment(weapon);
        var medicine = ThingMaker.MakeThing(RimWorld.ThingDefOf.MedicineHerbal);
        medicine.stackCount = MedicineCarried;
        _ = pawn.inventory.innerContainer.TryAdd(medicine);
        return (weapon, medicine);
    }

    private static void Board(Pawn pawn, TransportShip shuttle)
    {
        pawn.DeSpawn();
        _ = shuttle.TransporterComp.innerContainer.TryAdd(pawn);
    }

    [Test]
    public static IEnumerator ASoldPawnDropsItsWeaponAndInventoryAsItBoards()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        yield return Landed(scene);
        var shuttle = scene.ToTraderShuttles.First();
        var pawn = scene.SpawnPawn();
        var (weapon, medicine) = Equip(pawn);
        scene.TradeAgreement!.pawnsSoldToTrader.Add(pawn);

        Board(pawn, shuttle);

        var container = shuttle.TransporterComp.innerContainer;
        Expect.IsTrue(container.Contains(pawn), "The pawn didn't board the shuttle");
        Expect.IsNull(pawn.equipment.Primary, "The pawn kept its weapon");
        Expect.IsEmpty(pawn.inventory.innerContainer, "The pawn kept its inventory");
        Expect.IsTrue(weapon.Spawned, "The weapon isn't on the map");
        Expect.IsTrue(weapon.IsForbidden(Faction.OfPlayer), "The dropped weapon isn't forbidden");
        Expect.AreEqual(
            MedicineCarried,
            scene.CountOnMap(medicine.def),
            "The inventory isn't on the map"
        );
        Expect.AreEqual(1, container.Count, "Something besides the pawn went into the shuttle");

        _ = scene.TradeAgreement.pawnsSoldToTrader.Remove(pawn);
    }

    [Test]
    public static IEnumerator AnUnsoldPawnKeepsItsGearAsItBoards()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        yield return Landed(scene);
        var shuttle = scene.ToTraderShuttles.First();
        var pawn = scene.SpawnPawn();
        var (weapon, medicine) = Equip(pawn);

        Board(pawn, shuttle);

        Expect.AreEqual(weapon, pawn.equipment.Primary, "The pawn dropped its weapon");
        Expect.IsTrue(
            pawn.inventory.innerContainer.Contains(medicine),
            "The pawn dropped its inventory"
        );
    }
}
