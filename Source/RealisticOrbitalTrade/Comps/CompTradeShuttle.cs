using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RealisticOrbitalTrade.Comps;

public class CompTradeShuttle : ThingComp
{
    internal bool cancelled = false;
    internal bool isToTrader = false;
    internal TradeAgreement? tradeAgreement;
    internal List<ThingDefCountWithRequirements> requiredSpecificItems = new();

    private CompShuttle? _cachedCompShuttle;
    private CompShuttle Shuttle
    {
        get
        {
            _cachedCompShuttle ??= parent.GetComp<CompShuttle>();
            return _cachedCompShuttle;
        }
    }
    internal bool ShuttleAutoLoad
    {
        get => Traverse.Create(Shuttle).Field<bool>("autoload").Value;
        set => Traverse.Create(Shuttle).Field<bool>("autoload").Value = value;
    }

    private CompTransporter? _cachedCompTransporter;
    private CompTransporter Transporter
    {
        get
        {
            _cachedCompTransporter ??= parent.GetComp<CompTransporter>();
            return _cachedCompTransporter;
        }
    }

    public override void CompTick()
    {
        base.CompTick();
        if (isToTrader && parent.IsHashIntervalTick(120))
        {
            if (!parent.Spawned || cancelled)
            {
                return;
            }

            ShuttleAutoLoad = true;
            if (!Shuttle.LoadingInProgressOrReadyToLaunch)
            {
                TransporterUtility.InitiateLoading(Gen.YieldSingle(Transporter));
            }
        }
    }

    private static readonly List<string> tmpRequiredLabels = new();
    public override string CompInspectStringExtra()
    {
        StringBuilder stringBuilder = new StringBuilder();

        CalculateRemainingRequiredItems();

        tmpRequiredLabels.Clear();
        foreach (var item in tmpRequiredSpecificItems.Where(i => i.count > 0))
        {
            tmpRequiredLabels.Add(item.Label);
        }
        if (tmpRequiredLabels.Any())
        {
            stringBuilder.AppendInNewLine("RealisticOrbitalTrade.RequiredThings".Translate() + ": " + tmpRequiredLabels.ToCommaList().CapitalizeFirst());
        }
        return stringBuilder.ToString();
    }

    private void CalculateRemainingRequiredItems()
    {
        tmpRequiredSpecificItems.Clear();
        tmpRequiredSpecificItems.AddRange(requiredSpecificItems);

        ThingOwner innerContainer = Transporter.innerContainer;
        foreach (var storedItem in innerContainer)
        {
            if (storedItem is Pawn)
            {
                continue;
            }
            int stackCount = storedItem.stackCount;
            for (int i = 0; i < tmpRequiredSpecificItems.Count; i++)
            {
                ThingDefCountWithRequirements requiredSpecificItem = tmpRequiredSpecificItems[i];
                if (requiredSpecificItem.Matches(storedItem))
                {
                    int additionalItemsNeeded = Mathf.Min(requiredSpecificItem.count, stackCount);
                    if (additionalItemsNeeded > 0)
                    {
                        tmpRequiredSpecificItems[i] = tmpRequiredSpecificItems[i].WithCount(tmpRequiredSpecificItems[i].count - additionalItemsNeeded);
                        stackCount -= additionalItemsNeeded;
                    }
                }
            }
        }
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref isToTrader, "isToTrader");
        Scribe_References.Look(ref tradeAgreement, "tradeAgreement");
        Scribe_Collections.Look(ref requiredSpecificItems, "requiredSpecificItems", LookMode.Deep);
    }

    internal bool IsRequired(Thing thing)
    {
        if (!isToTrader)
        {
            return false;
        }
        foreach (var item in requiredSpecificItems)
        {
            if (item.Matches(thing))
            {
                return true;
            }
        }
        return false;
    }

    private static readonly List<ThingDefCountWithRequirements> tmpRequiredSpecificItems = new();
    private static readonly List<Thing> tmpAllSendableItems = new();
    internal void CheckAutoload()
    {
        if (!isToTrader)
        {
            return;
        }

        if (!ShuttleAutoLoad || !Transporter.LoadingInProgressOrReadyToLaunch || !parent.Spawned)
        {
            return;
        }

        CalculateRemainingRequiredItems();

        tmpAllSendableItems.Clear();
        tmpAllSendableItems.AddRange(TradeUtility.AllLaunchableThingsForTrade(parent.Map, tradeAgreement!.tradeShip));
        tmpAllSendableItems.AddRange(TransporterUtility.ThingsBeingHauledTo(Shuttle.TransportersInGroup, parent.Map));

        foreach (var requiredItem in tmpRequiredSpecificItems)
        {
            if (requiredItem.count <= 0)
            {
                continue;
            }

            int numberOfMatches = 0;
            foreach (var sendableItem in tmpAllSendableItems)
            {
                if (requiredItem.Matches(sendableItem))
                {
                    numberOfMatches += sendableItem.stackCount;
                }
            }
            if (numberOfMatches <= 0)
            {
                continue;
            }
            TransferableOneWay transferableOneWay = new TransferableOneWay();
            foreach (var sendableItem in tmpAllSendableItems)
            {
                if (requiredItem.Matches(sendableItem))
                {
                    transferableOneWay.things.Add(sendableItem);
                }
            }
            int count = Mathf.Min(requiredItem.count, numberOfMatches);
            Transporter.AddToTheToLoadList(transferableOneWay, count);
        }
    }

    public bool AllRequiredThingsLoaded
    {
        get
        {
            CalculateRemainingRequiredItems();
            return tmpRequiredSpecificItems.Sum(t => t.count) == 0;
        }
    }
}

internal struct ThingDefCountWithRequirements : IExposable
{
    public ThingDef def;
    internal int count;
    public bool healthAffectsPrice;
    public int hitPoints;
    public int maxHitPoints;
    public bool hasQuality;
    public QualityCategory quality;

    public string Label
    {
        get
        {
            return GenLabel.ThingLabel(def, null, count) + LabelExtras();
        }
    }

    private string LabelExtras()
    {
        string text = string.Empty;
        bool reducedHealth = healthAffectsPrice && hitPoints < maxHitPoints;
        if (reducedHealth || hasQuality)
        {
            text += " (";
            if (hasQuality)
            {
                text += quality.GetLabel();
            }
            if (reducedHealth)
            {
                if (hasQuality)
                {
                    text += " ";
                }
                text += ((float)hitPoints / maxHitPoints).ToStringPercent();
            }
            text += ")";
        }
        return text;
    }

    public void ExposeData()
    {
        Scribe_Defs.Look(ref def, "def");
        Scribe_Values.Look(ref count, "count");
        Scribe_Values.Look(ref healthAffectsPrice, "healthAffectsPrice");
        Scribe_Values.Look(ref hitPoints, "hitPoints");
        Scribe_Values.Look(ref maxHitPoints, "maxHitPoints");
        Scribe_Values.Look(ref hasQuality, "hasQuality");
        Scribe_Values.Look(ref quality, "quality");
    }

    internal bool Matches(Thing thing)
    {
        QualityUtility.TryGetQuality(thing, out var thingQuality);
        return def == thing.def && count != 0 && (!healthAffectsPrice || thing.HitPoints == hitPoints) && (!hasQuality || thingQuality == quality);
    }

    internal ThingDefCountWithRequirements WithCount(int count)
    {
        return new()
        {
            def = def,
            count = count,
            healthAffectsPrice = healthAffectsPrice,
            hitPoints = hitPoints,
            hasQuality = hasQuality,
            quality = quality
        };
    }
}

[HarmonyPatch(typeof(CompShuttle), nameof(CompShuttle.IsRequired))]
internal static class Rimworld_CompShuttle_IsRequired
{
    private static void Postfix(Thing thing, CompShuttle __instance, ref bool __result)
    {
        var compTradeShuttle = __instance.parent.TryGetComp<CompTradeShuttle>();
        if (!__result && compTradeShuttle != null)
        {
            __result = compTradeShuttle.IsRequired(thing);
        }
    }
}

[HarmonyPatch(typeof(CompShuttle), "CheckAutoload")]
internal static class Rimworld_CompShuttle_CheckAutoload_SpecificItems
{
    private static void Postfix(CompShuttle __instance)
    {
        var compTradeShuttle = __instance.parent.TryGetComp<CompTradeShuttle>();
        if (compTradeShuttle != null)
        {
            compTradeShuttle.CheckAutoload();
        }
    }
}

[HarmonyPatch(typeof(CompShuttle))]
[HarmonyPatch(nameof(CompShuttle.RequiredThingsLabel), MethodType.Getter)]
internal static class Rimworld_CompShuttle_RequiredThingsLabel
{
    private static void InjectThingsLabel(CompShuttle __instance, StringBuilder stringBuilder)
    {
        var compTradeShuttle = __instance.parent.TryGetComp<CompTradeShuttle>();
        if (compTradeShuttle != null && compTradeShuttle.requiredSpecificItems.Count > 0)
        {
            foreach (var requiredItem in compTradeShuttle.requiredSpecificItems)
            {
                stringBuilder.AppendLine("  - " + requiredItem.Label.CapitalizeFirst());
            }
        }
    }

    private static readonly MethodInfo _methodInjectThingsLabel = SymbolExtensions.GetMethodInfo(() => InjectThingsLabel(new(), new()));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        try
        {
            return Utils.InjectCallBeforeReturn(instructions, _methodInjectThingsLabel, i => i.IsLdloc(), new[] {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldloc_0),
            });
        }
        catch (InjectCallBeforeReturnException e)
        {
            RealisticOrbitalTradeMod.Error("Could not patch CompShuttle.RequiredThingsLabel, IL does not match expectations");
            return e.Instructions;
        }
    }
}

[HarmonyPatch(typeof(CompShuttle))]
[HarmonyPatch(nameof(CompShuttle.AllRequiredThingsLoaded), MethodType.Getter)]
internal static class Rimworld_CompShuttle_AllRequiredThingsLoaded
{
    private static void Postfix(CompShuttle __instance, ref bool __result)
    {
        var compTradeShuttle = __instance.parent.TryGetComp<CompTradeShuttle>();
        if (__result && compTradeShuttle != null)
        {
            __result = compTradeShuttle.AllRequiredThingsLoaded;
        }
    }
}

/*
*/

/*
    private static readonly List<Thing> tmpAllSendableItems = new();
    internal void CheckAutoload()
    {
        tmpAllSendableItems.Clear();
        tmpAllSendableItems.AddRange(TransporterUtility.AllSendableItems(Shuttle.TransportersInGroup, parent.Map, autoLoot: false));
        tmpAllSendableItems.AddRange(TransporterUtility.ThingsBeingHauledTo(Shuttle.TransportersInGroup, parent.Map));

        // foreach (var transferableOneWay in exactThingsToLoad)
        // {
        //     if (tmpAllSendableItems.Contains(transferableOneWay.AnyThing))
        //     {
        //         Transporter.AddToTheToLoadList(transferableOneWay, transferableOneWay.CountToTransfer);
        //     }
        // }
    }

[HarmonyPatch(typeof(CompShuttle), nameof(CompShuttle.CompGetGizmosExtra))]
internal static class Rimworld_CompShuttle_CompGetGizmosExtra
{
    private static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, CompShuttle __instance)
    {
        var compExactThingsRequired = __instance.parent.TryGetComp<CompTradeShuttle>();
        if (compExactThingsRequired != null && compExactThingsRequired.exactThingsToLoad.Count > 0)
        {
            return values.Where(g => !(g is Command_Toggle commandToggle && commandToggle.defaultLabel == "CommandAutoloadTransporters".Translate()));
        }
        else
        {
            return values;
        }
    }
}


[HarmonyPatch(typeof(CompShuttle))]
[HarmonyPatch(nameof(CompShuttle.RequiredThingsLabel), MethodType.Getter)]
internal static class Rimworld_CompShuttle_RequiredThingsLabel
{
    private static void InjectThingsLabel(CompShuttle __instance, StringBuilder stringBuilder)
    {
        var compExactThingsRequired = __instance.parent.TryGetComp<CompTradeShuttle>();
        if (compExactThingsRequired != null && compExactThingsRequired.exactThingsToLoad.Count > 0)
        {
            for (int i = 0; i < compExactThingsRequired.exactThingsToLoad.Count; i++)
            {
                stringBuilder.AppendLine("  - " + compExactThingsRequired.exactThingsToLoad[i].AnyThing.LabelCap);
            }
        }
    }

    private static MethodInfo _methodInjectThingsLabel = SymbolExtensions.GetMethodInfo(() => InjectThingsLabel(new(), new()));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        try
        {
            return Utils.InjectCallBeforeReturn(instructions, _methodInjectThingsLabel, i => i.IsLdloc(), new[] {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldloc_0),
            });
        }
        catch (InjectCallBeforeReturnException e)
        {
            RealisticOrbitalTradeMod.Error("Could not patch CompShuttle.RequiredThingsLabel, IL does not match expectations");
            return e.Instructions;
        }
    }
}

[HarmonyPatch(typeof(CompShuttle))]
[HarmonyPatch(nameof(CompShuttle.CompInspectStringExtra))]
internal static class Rimworld_CompShuttle_CompInspectStringExtra
{
    private static List<string> tmpRequiredLabels = new List<string>();

    private static void InjectCompInspectStringExtra(CompShuttle __instance, StringBuilder stringBuilder)
    {
        var compExactThingsRequired = __instance.parent.TryGetComp<CompTradeShuttle>();
        if (compExactThingsRequired != null && compExactThingsRequired.exactThingsToLoad.Count > 0)
        {
            tmpRequiredLabels.Clear();
            for (int i = 0; i < compExactThingsRequired.exactThingsToLoad.Count; i++)
            {
                if (compExactThingsRequired.exactThingsToLoad[i].AnyThing.holdingOwner != __instance.Transporter.innerContainer)
                {
                    tmpRequiredLabels.Add(compExactThingsRequired.exactThingsToLoad[i].AnyThing.Label);
                }
            }
            if (tmpRequiredLabels.Any())
            {
                if (stringBuilder.Length == 0)
                {
                    stringBuilder.Append("Required".Translate() + ": ");
                }
                stringBuilder.AppendInNewLine(tmpRequiredLabels.ToCommaList().CapitalizeFirst());
            }
        }
    }

    private static MethodInfo _methodInjectThingsLabel = SymbolExtensions.GetMethodInfo(() => InjectCompInspectStringExtra(new(), new()));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        try
        {
            return Utils.InjectCallBeforeReturn(instructions, _methodInjectThingsLabel, i => i.IsLdloc(), new[] {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldloc_0),
            });
        }
        catch (InjectCallBeforeReturnException e)
        {
            RealisticOrbitalTradeMod.Error("Could not patch CompShuttle.CompInspectStringExtra, IL does not match expectations");
            return e.Instructions;
        }
    }
}
*/
