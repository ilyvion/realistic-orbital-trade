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
            var ticksUntilCommsClosed = tradeShip.GetData().ticksUntilCommsClosed;
            if (ticksUntilCommsClosed > 0)
            {
                _ = stringBuilder.Remove(
                    stringBuilder.Length - Environment.NewLine.Length,
                    Environment.NewLine.Length
                );
                _ = stringBuilder.AppendLine(
                    " "
                        + "RealisticOrbitalTrade.UntilCommsCloseForTrade".Translate(
                            ticksUntilCommsClosed.ToStringTicksToPeriod()
                        )
                );
            }
            else if (ticksUntilCommsClosed == 0)
            {
                _ = stringBuilder.Remove(
                    stringBuilder.Length - Environment.NewLine.Length,
                    Environment.NewLine.Length
                );
                _ = stringBuilder.AppendLine(
                    " " + "RealisticOrbitalTrade.CommsClosedForTrade".Translate()
                );
            }
        }
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
    )
    {
        var codeMatcher = new CodeMatcher(instructions);
        _ = codeMatcher.SearchForward(i =>
            i.opcode == OpCodes.Ldfld && i.operand is FieldInfo f && f == _fieldTicksUntilDeparture
        );
        if (!codeMatcher.IsValid)
        {
            RealisticOrbitalTradeMod.Error(
                $"Could not patch {modName}'s Alert_OrbitalTrader.GetExplanation, IL does not match expectations"
            );
            return codeMatcher.Instructions();
        }
        _ = codeMatcher.SearchForward(i => i.opcode == OpCodes.Pop);
        if (!codeMatcher.IsValid)
        {
            RealisticOrbitalTradeMod.Error(
                $"Could not patch {modName}'s Alert_OrbitalTrader.GetExplanation, IL does not match expectations"
            );
            return codeMatcher.Instructions();
        }

        // We should be in the right spot now. Transpiler, go go go!

        _ = codeMatcher.Insert(
            [
                new(OpCodes.Ldloc_3),
                new(OpCodes.Ldloc_0),
                new(OpCodes.Call, _methodAddCommsClosedForTradeExplanation),
            ]
        );

        return codeMatcher.Instructions();
    }
}
