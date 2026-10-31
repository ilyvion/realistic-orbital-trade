using DevTools.Testing;
using Verse.AI;

namespace RealisticOrbitalTrade.Tests;

// Another mod's prefix on LoadTransportersJobUtility's HasJobOnTransporter/JobOnTransporter can
// skip the original (and with it our own prefix) or throw; the reservation scope our prefix opens
// must be closed either way, and a skipped prefix must not make our cleanup throw.
[TestFixture(TestType.MainMenu)]
internal sealed class LoadTransportersScopePatchTests
{
    private const string HarmonyId = "RealisticOrbitalTrade.Tests.LoadTransportersScopePatch";

    private static readonly Harmony _harmony = new(HarmonyId);

    private static readonly MethodInfo HasJobOnTransporterMethod = AccessTools.Method(
        typeof(LoadTransportersJobUtility),
        nameof(LoadTransportersJobUtility.HasJobOnTransporter)
    );

    private static readonly MethodInfo JobOnTransporterMethod = AccessTools.Method(
        typeof(LoadTransportersJobUtility),
        nameof(LoadTransportersJobUtility.JobOnTransporter)
    );

    private static Pawn? _scopePawnSeenByLaterPrefix;

    [TearDown]
    public static void TearDown()
    {
        _harmony.UnpatchAll(HarmonyId);
        _scopePawnSeenByLaterPrefix = null;
    }

    private static bool SkipHasJobOnTransporter(ref bool __result)
    {
        __result = true;
        return false;
    }

    private static bool SkipJobOnTransporter(ref Job? __result)
    {
        __result = null;
        return false;
    }

    private static void ThrowAfterScopeEntered()
    {
        _scopePawnSeenByLaterPrefix = TradeHaulReservationContext.ActivePawn;
        throw new InvalidOperationException("thrown by another mod's prefix");
    }

    private static void PatchPrefix(MethodInfo target, string prefixName, int priority) =>
        _ = _harmony.Patch(
            target,
            prefix: new HarmonyMethod(typeof(LoadTransportersScopePatchTests), prefixName)
            {
                priority = priority,
            }
        );

    [Test]
    public static void HasJobOnTransporterKeepsTheSkippingPrefixsResult()
    {
        PatchPrefix(
            HasJobOnTransporterMethod,
            nameof(SkipHasJobOnTransporter),
            HarmonyLib.Priority.First
        );

        var result = LoadTransportersJobUtility.HasJobOnTransporter(new Pawn(), new());

        Expect.IsTrue(result);
        Expect.IsNull(TradeHaulReservationContext.ActivePawn);
    }

    [Test]
    public static void JobOnTransporterKeepsTheSkippingPrefixsResult()
    {
        PatchPrefix(
            JobOnTransporterMethod,
            nameof(SkipJobOnTransporter),
            HarmonyLib.Priority.First
        );

        var result = LoadTransportersJobUtility.JobOnTransporter(new Pawn(), new());

        Expect.IsNull(result);
        Expect.IsNull(TradeHaulReservationContext.ActivePawn);
    }

    [Test]
    public static void HasJobOnTransporterClosesTheScopeWhenALaterPrefixThrows()
    {
        PatchPrefix(
            HasJobOnTransporterMethod,
            nameof(ThrowAfterScopeEntered),
            HarmonyLib.Priority.Last
        );
        var pawn = new Pawn();

        _ = Expect.Throws<InvalidOperationException>(() =>
            LoadTransportersJobUtility.HasJobOnTransporter(pawn, new())
        );

        Expect.ReferencesAreEqual(pawn, _scopePawnSeenByLaterPrefix);
        Expect.IsNull(TradeHaulReservationContext.ActivePawn);
    }

    [Test]
    public static void JobOnTransporterClosesTheScopeWhenALaterPrefixThrows()
    {
        PatchPrefix(
            JobOnTransporterMethod,
            nameof(ThrowAfterScopeEntered),
            HarmonyLib.Priority.Last
        );
        var pawn = new Pawn();

        _ = Expect.Throws<InvalidOperationException>(() =>
            LoadTransportersJobUtility.JobOnTransporter(pawn, new())
        );

        Expect.ReferencesAreEqual(pawn, _scopePawnSeenByLaterPrefix);
        Expect.IsNull(TradeHaulReservationContext.ActivePawn);
    }
}
