using RealisticOrbitalTrade.Comps;
using RealisticOrbitalTrade.Patch;

namespace RealisticOrbitalTrade.ITabs;

internal class ITab_ContentsTransporterCustom : ITab_ContentsTransporter
{
    public ITab_ContentsTransporterCustom()
    {
        canRemoveThings = false;
    }

    protected override void DoItemsLists(Rect inRect, ref float curY)
    {
        Rimworld_ITab_ContentsTransporter_DoItemsLists_Reverse.DoItemsLists(this, inRect, ref curY);
        DoMissingSoldItemsList(inRect, ref curY);
    }

    internal new void DoThingRow(
        ThingDef thingDef,
        int count,
        List<Thing> things,
        float width,
        ref float curY,
        Action<int> discardAction
    ) =>
        Rimworld_ITab_ContentsBase_DoThingRow_Reverse.DoThingRow(
            this,
            thingDef,
            count,
            things,
            width,
            ref curY,
            discardAction
        );

    // A thing/pawn already sold to the trader but lost before delivery (see
    // TradeAgreement.Notify_OwnThingDestroyed) is dropped from the shuttle's required cargo right
    // away, so it never shows up in the "to load" list above - it can't be loaded either way. That
    // leaves the player with no way to see what's actually missing from a trade with hundreds of
    // sold items, so it gets its own list here instead, sourced from TradeAgreement.lostSoldItems
    // rather than the "to load" list's own things, since there's no live Thing left to draw a row
    // for.
    private void DoMissingSoldItemsList(Rect inRect, ref float curY)
    {
        var lostSoldItems = SelThing.TryGetComp<CompTradeShuttle>()?.tradeAgreement?.lostSoldItems;
        if (lostSoldItems is not { Count: > 0 })
        {
            return;
        }

        Rect rect = new(0f, curY, inRect.width, inRect.height);
        var curY2 = 0f;
        Widgets.BeginGroup(rect);
        Widgets.ListSeparator(
            ref curY2,
            rect.width,
            "RealisticOrbitalTrade.MissingSoldItemsLabel".Translate()
        );
        foreach (var record in lostSoldItems)
        {
            DoMissingSoldItemRow(record, rect.width, ref curY2);
        }
        Widgets.EndGroup();
        curY += curY2;
    }

    private static void DoMissingSoldItemRow(
        TradeAgreement.LostTradeItemRecord record,
        float width,
        ref float curY
    )
    {
        Rect rect = new(0f, curY, width, 28f);
        if (record.def != null)
        {
            _ = Widgets.InfoCardButton(rect.width - 24f, curY, record.def);
            rect.width -= 24f;
            Widgets.ThingIcon(new Rect(4f, curY, 28f, 28f), record.def, record.stuffDef);
        }

        var label = record.Label;
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = Color.red;
        Rect labelRect = new(36f, curY, rect.width - 36f, rect.height);
        Text.WordWrap = false;
        Widgets.Label(labelRect, label.Truncate(labelRect.width));
        Text.WordWrap = true;
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;
        TooltipHandler.TipRegion(
            rect,
            "RealisticOrbitalTrade.MissingSoldItemTooltip".Translate(label)
        );

        curY += 28f;
    }
}
