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

    [Test]
    public static void TranspilerInsertsCallBeforeFirstPopAfterDepartureFieldLoad()
    {
        var departureField = AccessTools.Field(
            typeof(PassingShip),
            nameof(PassingShip.ticksUntilDeparture)
        );
        var instructions = new List<CodeInstruction>
        {
            new(OpCodes.Pop),
            new(OpCodes.Ldarg_0),
            new(OpCodes.Ldfld, departureField),
            new(OpCodes.Nop),
            new(OpCodes.Pop),
            new(OpCodes.Ret),
        };

        var result = Alert_OrbitalTrader_Shared
            .TranspileGetExplanation(instructions, "Test Mod")
            .ToList();

        Expect.AreEqual(9, result.Count);
        Expect.AreEqual(OpCodes.Nop, result[3].opcode);
        Expect.AreEqual(OpCodes.Ldloc_3, result[4].opcode);
        Expect.AreEqual(OpCodes.Ldloc_0, result[5].opcode);
        Expect.AreEqual(OpCodes.Call, result[6].opcode);
        Expect.AreEqual(OpCodes.Pop, result[7].opcode);
        Expect.AreEqual(OpCodes.Pop, result[0].opcode);
    }
}
