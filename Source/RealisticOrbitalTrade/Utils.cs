using System.Reflection;
using System.Reflection.Emit;
using RealisticOrbitalTrade.Comps;

namespace RealisticOrbitalTrade;

internal static class Utils
{
    public static IEnumerable<CodeInstruction> InjectCallBeforeReturn(
        IEnumerable<CodeInstruction> instructions,
        MethodInfo methodToCall,
        Func<CodeInstruction, bool> matchInstruction,
        IEnumerable<CodeInstruction>? additionalInstructionsBeforeCall = null
    )
    {
        var codeMatcher = new CodeMatcher(instructions);
        codeMatcher.End();
        if (codeMatcher.Instruction.opcode != OpCodes.Ret)
        {
            throw new InjectCallBeforeReturnException(codeMatcher.Instructions());
        }
        while (codeMatcher.Pos > 0)
        {
            codeMatcher.Advance(-1);
            if (matchInstruction(codeMatcher.Instruction))
            {
                codeMatcher.Insert(
                    (additionalInstructionsBeforeCall ?? []).Concat(
                        [
                            // new CodeInstruction(OpCodes.Ldarg_0),
                            // new CodeInstruction(OpCodes.Ldloc_0),
                            new CodeInstruction(OpCodes.Call, methodToCall),
                        ]
                    )
                );

                return codeMatcher.Instructions();
            }
        }

        throw new InjectCallBeforeReturnException(codeMatcher.Instructions());
    }

    public static Quest? AmendsQuest =>
        Find.QuestManager.QuestsListForReading.SingleOrDefault(q =>
            q.root == QuestScriptDefOf.ROT_TradeShipMakeAmends && !q.Historical
        );
    public static bool IsMakingAmends => AmendsQuest != null;

    public static void AddThingToLoadToShuttle(
        ThingCountClass thingCount,
        CompTradeShuttle compTradeShuttle,
        CompShuttle compShuttle
    )
    {
        if (
            thingCount.thing.HasRequirements(
                out var healthAffectsPrice,
                out var qualityCategory,
                out var hasInnerThing
            )
        )
        {
            Thing thing = thingCount.thing.GetInnerIfMinified();
            compTradeShuttle.requiredSpecificItems.Add(
                new ThingDefCountWithRequirements
                {
                    def = thing.def,
                    stuffDef = thing.Stuff,
                    isInnerThing = hasInnerThing,
                    count = thingCount.Count,
                    healthAffectsPrice = healthAffectsPrice,
                    hitPoints = thing.HitPoints,
                    hasQuality = qualityCategory.HasValue,
                    quality = qualityCategory ?? QualityCategory.Normal,
                }
            );
        }
        else
        {
            compShuttle.requiredItems.Add(
                new ThingDefCount(thingCount.thing.def, thingCount.Count)
            );
        }
    }
}

[Serializable]
public class InjectCallBeforeReturnException : Exception
{
    public IEnumerable<CodeInstruction> Instructions { get; }

    public InjectCallBeforeReturnException(IEnumerable<CodeInstruction> instructions)
        : base()
    {
        Instructions = instructions;
    }
}

[AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method)]
public class ReloadableAttribute : Attribute { }
