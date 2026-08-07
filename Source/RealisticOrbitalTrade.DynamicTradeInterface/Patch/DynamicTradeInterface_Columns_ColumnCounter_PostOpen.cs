using DynamicTradeInterface.UserInterface.Columns;
using RealisticOrbitalTrade.Patch;

namespace RealisticOrbitalTrade.DynamicTradeInterface.Patch;

// Clamps the cached min/max transferable count DTI computes for each row (backing its count
// text field's range) down to whatever the relevant shuttle's remaining mass capacity allows,
// mirroring the vanilla trade dialog's "M<"/">M" buttons (see
// Rimworld_TradeUI_DrawTradeableRow.GetMassBounds). DTI's count column has no room for a
// labeled mass button, so this reuses its existing text field instead of adding a new one. Uses
// the same late-patching pattern as DynamicTradeInterface_UserInterface_Window_DynamicTrade_
// DoWindowContents, since Harmony attributes patch too early for DTI's types.
[StaticConstructorOnStartup]
[HotSwappable]
internal static class DynamicTradeInterface_Columns_ColumnCounter_PostOpen
{
    static DynamicTradeInterface_Columns_ColumnCounter_PostOpen()
    {
        Harmony harmony = new(Constants.Id);
        _ = harmony.Patch(
            AccessTools.Method(typeof(ColumnCounter), nameof(ColumnCounter.PostOpen)),
            postfix: new HarmonyMethod(
                AccessTools.Method(
                    typeof(DynamicTradeInterface_Columns_ColumnCounter_PostOpen),
                    nameof(Postfix)
                )
            )
        );
    }

    private static void Postfix(IEnumerable<Tradeable> rows)
    {
        if (TradeShipData.tradeAgreementForQuest == null)
        {
            return;
        }

        foreach (var row in rows)
        {
            if (!ColumnCounter._editableCache.TryGetValue(row, out var cache))
            {
                continue;
            }

            var (massMin, massMax) = Rimworld_TradeUI_DrawTradeableRow.GetMassBounds(row);
            if (massMin.HasValue)
            {
                cache.MinimumQuantity = Math.Max(cache.MinimumQuantity, massMin.Value);
            }
            if (massMax.HasValue)
            {
                cache.MaximumQuantity = Math.Min(cache.MaximumQuantity, massMax.Value);
            }
        }
    }
}
