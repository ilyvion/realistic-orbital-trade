using System.Reflection.Emit;
using System.Text;
using DevTools.Testing;
using RealisticOrbitalTrade.Patch;

namespace RealisticOrbitalTrade.Tests;

// The Tweaks Galore / We Had A Trader alert explanation transpiler and the comms-closed note it
// injects.
[TestFixture(TestType.MainMenu)]
internal sealed class AlertOrbitalTraderSharedTests
{
    private static string Note(int ticksUntilCommsClosed, string initial)
    {
        var builder = new StringBuilder(initial);
        Alert_OrbitalTrader_Shared.AppendCommsClosedNote(builder, ticksUntilCommsClosed);
        return builder.ToString();
    }

    private static readonly string NewLine = Environment.NewLine;

    [Test]
    public static void CountdownReplacesTrailingNewline()
    {
        var expected =
            "Line"
            + " "
            + "RealisticOrbitalTrade.UntilCommsCloseForTrade"
                .Translate(5000.ToStringTicksToPeriod())
                .RawText.StripTags()
            + NewLine;

        Expect.AreEqual(expected, Note(5000, "Line" + NewLine));
    }

    [Test]
    public static void ClosedTextReplacesTrailingNewline()
    {
        var expected =
            "Line"
            + " "
            + "RealisticOrbitalTrade.CommsClosedForTrade".Translate().Resolve()
            + NewLine;

        Expect.AreEqual(expected, Note(0, "Line" + NewLine));
    }

    [Test]
    public static void NoCommsDeadlineLeavesTextUntouched() =>
        Expect.AreEqual("Line" + NewLine, Note(-1, "Line" + NewLine));

    [Test]
    public static void OnlyOneTrailingNewlineIsRemoved()
    {
        var result = Note(0, "A" + NewLine + "B" + NewLine);

        Expect.IsTrue(result.StartsWith("A" + NewLine + "B ", StringComparison.Ordinal));
        Expect.IsTrue(result.EndsWith(NewLine, StringComparison.Ordinal));
    }

    [Test]
    public static void EmptyBuilderDoesNotThrow()
    {
        var expected =
            " " + "RealisticOrbitalTrade.CommsClosedForTrade".Translate().Resolve() + NewLine;

        Expect.AreEqual(expected, Note(0, ""));
    }

    // The shape of the mods' GetExplanation around the departure time: the StringBuilder is
    // created first, the passing ship is a foreach local, and the line appended is popped.
    private static List<CodeInstruction> Explanation(
        int stringBuilderLocal,
        int passingShipLocal
    ) =>
        [
            new(OpCodes.Newobj, typeof(StringBuilder).GetConstructor(Type.EmptyTypes)),
            new(OpCodes.Stloc_S, (byte)stringBuilderLocal),
            new(OpCodes.Pop),
            new(OpCodes.Ldloc_S, (byte)stringBuilderLocal),
            new(OpCodes.Ldloc_S, (byte)passingShipLocal),
            new(
                OpCodes.Ldfld,
                AccessTools.Field(typeof(PassingShip), nameof(PassingShip.ticksUntilDeparture))
            ),
            new(OpCodes.Nop),
            new(OpCodes.Pop),
            new(OpCodes.Ret),
        ];

    [Test]
    public static void TranspilerInsertsCallBeforeFirstPopAfterDepartureFieldLoad()
    {
        var result = Alert_OrbitalTrader_Shared
            .TranspileGetExplanation(Explanation(0, 3), "Test Mod")
            .ToList();

        Expect.AreEqual(12, result.Count);
        Expect.AreEqual(OpCodes.Pop, result[2].opcode);
        Expect.AreEqual(OpCodes.Nop, result[6].opcode);
        Expect.IsTrue(result[7].LoadsLocal(3));
        Expect.IsTrue(result[8].LoadsLocal(0));
        Expect.AreEqual(OpCodes.Call, result[9].opcode);
        Expect.AreEqual(OpCodes.Pop, result[10].opcode);
    }

    [Test]
    public static void TranspilerPassesThePassingShipAndStringBuilderLocalsWhereverTheyAre()
    {
        // Tweaks Galore's 1.7 build keeps the map in a local, so the ship is local 4.
        var tweaksGalore17 = Alert_OrbitalTrader_Shared
            .TranspileGetExplanation(Explanation(0, 4), "Test Mod")
            .ToList();
        Expect.IsTrue(tweaksGalore17[7].LoadsLocal(4));
        Expect.IsTrue(tweaksGalore17[8].LoadsLocal(0));
        Expect.AreEqual(OpCodes.Call, tweaksGalore17[9].opcode);

        var elsewhere = Alert_OrbitalTrader_Shared
            .TranspileGetExplanation(Explanation(2, 5), "Test Mod")
            .ToList();
        Expect.IsTrue(elsewhere[7].LoadsLocal(5));
        Expect.IsTrue(elsewhere[8].LoadsLocal(2));
        Expect.AreEqual(OpCodes.Call, elsewhere[9].opcode);
    }
}
