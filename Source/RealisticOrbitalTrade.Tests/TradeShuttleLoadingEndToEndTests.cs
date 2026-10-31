using DevTools.Testing;
using RealisticOrbitalTrade.Comps;

namespace RealisticOrbitalTrade.Tests;

// Only this mod's own loading job loads a trade shuttle. Vanilla's transporter loading, and other
// mods' prefixes on the LoadTransportersJobUtility methods it goes through, never see one.
[TestFixture(TestType.Playing)]
internal sealed class TradeShuttleLoadingEndToEndTests
{
    private const string HarmonyId = "RealisticOrbitalTrade.Tests.TradeShuttleLoading";
    private const int SteelOnMap = 500;
    private const int SteelSold = 400;

    private static readonly Harmony _harmony = new(HarmonyId);

    private static int _otherModPrefixCallsOnTradeShuttles;

    [TearDown]
    public static void TearDown()
    {
        _harmony.UnpatchAll(HarmonyId);
        _otherModPrefixCallsOnTradeShuttles = 0;
    }

    // Stands in for another mod's loading system patching these methods with the default priority.
    private static bool OtherModPrefix(CompTransporter transporter)
    {
        if (transporter.parent.TryGetComp<CompTradeShuttle>() != null)
        {
            _otherModPrefixCallsOnTradeShuttles++;
        }
        return true;
    }

    private static void PatchAsOtherMod(string methodName) =>
        _ = _harmony.Patch(
            AccessTools.Method(typeof(LoadTransportersJobUtility), methodName),
            prefix: new HarmonyMethod(
                typeof(TradeShuttleLoadingEndToEndTests),
                nameof(OtherModPrefix)
            )
        );

    [Test]
    public static IEnumerator ASaleIsLoadedOnlyByThisModsJob()
    {
        PatchAsOtherMod(nameof(LoadTransportersJobUtility.HasJobOnTransporter));
        PatchAsOtherMod(nameof(LoadTransportersJobUtility.JobOnTransporter));
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, SteelOnMap);
        HashSet<JobDef> otherJobs = [];

        yield return scene.SellAndWatchShuttleJobs(steel, SteelSold, otherJobs);

        scene.ExpectSaleCompleted();
        Expect.IsEmpty(otherJobs, "Another job worked on the trade shuttle");
        Expect.AreEqual(
            0,
            _otherModPrefixCallsOnTradeShuttles,
            "Another mod's prefix saw a trade shuttle"
        );
    }
}
