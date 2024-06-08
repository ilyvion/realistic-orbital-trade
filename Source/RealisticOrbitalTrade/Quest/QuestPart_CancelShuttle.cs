using System.Collections.Generic;
using RealisticOrbitalTrade.Comps;
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
        if (Scribe.mode == LoadSaveMode.PostLoadInit && shuttle != null)
        {
            if (shuttle?.TryGetComp<CompTradeShuttle>(out var compTradeShuttle) ?? false)
            {
                compTradeShuttle.cancelTradeAction = Complete;
            }
        }
    }

    public override void Cleanup()
    {
        base.Cleanup();
        shuttle = null;
    }

    public override void PostQuestAdded()
    {
        base.PostQuestAdded();
        if (shuttle?.TryGetComp<CompTradeShuttle>(out var compTradeShuttle) ?? false)
        {
            RealisticOrbitalTradeMod.Dev(() => "Setting cancel trade action on CompTradeShuttle");
            compTradeShuttle.cancelTradeAction = Complete;
        }
    }
}
