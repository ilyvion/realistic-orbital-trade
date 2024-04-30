using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RealisticOrbitalTrade.Quests;

[StaticConstructorOnStartup]
internal abstract class QuestPart_CancelShuttle : QuestPartActivable
{
    private static readonly Texture2D Cancel = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");

    public Thing? shuttle;

    protected abstract string DefaultLabel { get; }
    protected abstract string DefaultDesc { get; }

    public override IEnumerable<Gizmo> ExtraGizmos(ISelectable target)
    {
        if (target == shuttle)
        {
            Command_Action commandActionCancel = new()
            {
                defaultLabel = DefaultLabel,
                defaultDesc = DefaultDesc,
                icon = Cancel,
                activateSound = SoundDefOf.Tick_Low,
                action = () =>
                {
                    Complete();
                },
                hotKey = KeyBindingDefOf.Designator_Cancel,
            };
            return new[]
            {
                commandActionCancel
            };
        }
        else
        {
            return base.ExtraGizmos(target);
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref shuttle, "shuttle");
    }

    public override void Cleanup()
    {
        base.Cleanup();
        shuttle = null;
    }
}
