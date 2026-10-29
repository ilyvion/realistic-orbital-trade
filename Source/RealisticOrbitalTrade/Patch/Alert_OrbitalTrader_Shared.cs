using System.Reflection.Emit;
using System.Text;

namespace RealisticOrbitalTrade.Patch;

internal static class Alert_OrbitalTrader_Shared
{
    private static void AddCommsClosedForTradeExplanation(
        PassingShip passingShip,
        StringBuilder stringBuilder
    )
    {
        if (passingShip is TradeShip tradeShip)
        {
            AppendCommsClosedNote(stringBuilder, tradeShip.GetData().ticksUntilCommsClosed);
        }
    }

    /// <summary>
    /// Replaces the trailing newline of <paramref name="stringBuilder"/> with the comms-closed
    /// note; <c>-1</c> (no comms deadline) leaves it untouched.
    /// </summary>
    internal static void AppendCommsClosedNote(
        StringBuilder stringBuilder,
        int ticksUntilCommsClosed
    )
    {
        string note;
        if (ticksUntilCommsClosed > 0)
        {
            note = "RealisticOrbitalTrade.UntilCommsCloseForTrade".Translate(
                ticksUntilCommsClosed.ToStringTicksToPeriod()
            );
        }
        else if (ticksUntilCommsClosed == 0)
        {
            note = "RealisticOrbitalTrade.CommsClosedForTrade".Translate();
        }
        else
        {
            return;
        }

        var newLineLength = Math.Min(Environment.NewLine.Length, stringBuilder.Length);
        _ = stringBuilder.Remove(stringBuilder.Length - newLineLength, newLineLength);
        _ = stringBuilder.AppendLine(" " + note);
    }

    private static readonly FieldInfo _fieldTicksUntilDeparture = AccessTools.Field(
        typeof(PassingShip),
        nameof(PassingShip.ticksUntilDeparture)
    );

    private static readonly MethodInfo _methodAddCommsClosedForTradeExplanation =
        SymbolExtensions.GetMethodInfo(() => AddCommsClosedForTradeExplanation(new(), new()));

    internal static IEnumerable<CodeInstruction> TranspileGetExplanation(
        IEnumerable<CodeInstruction> instructions,
        string modName
    ) =>
        TranspilerUtility.PatchOrOriginal(
            instructions,
            $"patch {modName}'s Alert_OrbitalTrader.GetExplanation",
            RealisticOrbitalTradeMod.Instance.LogError,
            codeMatcher =>
            {
                _ = codeMatcher
                    .ExpectForward(
                        i =>
                            i.opcode == OpCodes.Ldfld
                            && i.operand is FieldInfo f
                            && f == _fieldTicksUntilDeparture,
                        "access to PassingShip.ticksUntilDeparture"
                    )
                    .ExpectForward(
                        i => i.opcode == OpCodes.Pop,
                        "[pop] after access to PassingShip.ticksUntilDeparture"
                    );

                // We should be in the right spot now. Transpiler, go go go!

                _ = codeMatcher.Insert([
                    new(OpCodes.Ldloc_3),
                    new(OpCodes.Ldloc_0),
                    new(OpCodes.Call, _methodAddCommsClosedForTradeExplanation),
                ]);
            }
        );
}
