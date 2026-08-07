using DynamicTradeInterface.UserInterface.Columns;
using RealisticOrbitalTrade.Patch;

namespace RealisticOrbitalTrade.DynamicTradeInterface.Patch;

// DTI's button-based count column ("<<"/"<"/">"/">>") drives its non-shift-click "buy/sell as
// much as affordable" behavior through ColumnButtons.MaxAmount(row, action), which returns a
// currency-affordability-based count. Clamping that return value here to the relevant shuttle's
// remaining mass capacity makes "<<"/">>" stop at the mass limit too, mirroring the vanilla
// trade dialog's "M<"/">M" buttons (Rimworld_TradeUI_DrawTradeableRow.GetMassBounds) without
// needing a dedicated button DTI's layout has no room for. The shift-click "ignore affordability,
// go to true max" path bypasses MaxAmount entirely and isn't covered here, but the mod's
// Accept-time mass check (Rimworld_TradeDeal_TryExecute) still catches that case. Uses the same
// late-patching pattern as DynamicTradeInterface_UserInterface_Window_DynamicTrade_
// DoWindowContents, since Harmony attributes patch too early for DTI's types.
//
// ColumnButtons is internal to DTI's own assembly; the
// RealisticOrbitalTrade.DynamicTradeInterface.csproj Publicize items make it accessible here.
[StaticConstructorOnStartup]
[HotSwappable]
internal static class DynamicTradeInterface_Columns_ColumnButtons_MaxAmount
{
    static DynamicTradeInterface_Columns_ColumnButtons_MaxAmount()
    {
        Harmony harmony = new(Constants.Id);
        _ = harmony.Patch(
            AccessTools.Method(typeof(ColumnButtons), nameof(ColumnButtons.MaxAmount)),
            postfix: new HarmonyMethod(
                AccessTools.Method(
                    typeof(DynamicTradeInterface_Columns_ColumnButtons_MaxAmount),
                    nameof(Postfix)
                )
            )
        );
    }

    private static void Postfix(Tradeable row, TradeAction action, ref int __result)
    {
        if (TradeShipData.tradeAgreementForQuest == null)
        {
            return;
        }

        var (massMin, massMax) = Rimworld_TradeUI_DrawTradeableRow.GetMassBounds(row);
        if (action == TradeAction.PlayerBuys && massMax.HasValue)
        {
            __result = Math.Min(__result, massMax.Value);
        }
        else if (action == TradeAction.PlayerSells && massMin.HasValue)
        {
            __result = Math.Min(__result, -massMin.Value);
        }
    }
}
