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
                _ = codeMatcher.ExpectForward(
                    i => i.CallsConstructorOf(typeof(StringBuilder)),
                    "[newobj StringBuilder]"
                );
                _ = codeMatcher.Advance(1);
                if (!codeMatcher.IsValid || !codeMatcher.Instruction.IsStloc())
                {
                    throw new TranspilerMismatchException(
                        "[stloc] of the new StringBuilder not found."
                    );
                }
                var loadStringBuilder = codeMatcher.Instruction.LoadSameLocal();

                _ = codeMatcher.ExpectForward(
                    i =>
                        i.opcode == OpCodes.Ldfld
                        && i.operand is FieldInfo f
                        && f == _fieldTicksUntilDeparture,
                    "access to PassingShip.ticksUntilDeparture"
                );
                var loadPassingShip = codeMatcher.InstructionAt(-1);
                if (!loadPassingShip.IsLdloc())
                {
                    throw new TranspilerMismatchException(
                        $"[ldloc] of the passing ship before PassingShip.ticksUntilDeparture not found, was {loadPassingShip.opcode}."
                    );
                }

                _ = codeMatcher.ExpectForward(
                    i => i.opcode == OpCodes.Pop,
                    "[pop] after access to PassingShip.ticksUntilDeparture"
                );

                _ = codeMatcher.Insert([
                    loadPassingShip.LoadSameLocal(),
                    loadStringBuilder,
                    new(OpCodes.Call, _methodAddCommsClosedForTradeExplanation),
                ]);
            }
        );
}
