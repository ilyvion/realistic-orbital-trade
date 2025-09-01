using RealisticOrbitalTrade.Patch;

namespace RealisticOrbitalTrade.ITabs;

internal class ITab_ContentsTransporterCustom : ITab_ContentsTransporter
{
    public ITab_ContentsTransporterCustom()
    {
        canRemoveThings = false;
    }

    protected override void DoItemsLists(Rect inRect, ref float curY) =>
        Rimworld_ITab_ContentsTransporter_DoItemsLists_Reverse.DoItemsLists(this, inRect, ref curY);

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
}
