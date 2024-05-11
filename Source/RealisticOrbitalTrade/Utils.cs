using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RealisticOrbitalTrade;

internal static class Utils
{
    public static IEnumerable<CodeInstruction> InjectCallBeforeReturn(
        IEnumerable<CodeInstruction> instructions,
        MethodInfo methodToCall,
        Func<CodeInstruction, bool> matchInstruction,
        IEnumerable<CodeInstruction>? additionalInstructionsBeforeCall = null)
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
                codeMatcher.Insert((additionalInstructionsBeforeCall ?? new CodeInstruction[] { }).Concat(new[]{
                    // new CodeInstruction(OpCodes.Ldarg_0),
                    // new CodeInstruction(OpCodes.Ldloc_0),
                    new CodeInstruction(OpCodes.Call, methodToCall)
                }));

                return codeMatcher.Instructions();
            }
        }

        throw new InjectCallBeforeReturnException(codeMatcher.Instructions());
    }

    public static Quest? AmendsQuest => Find.QuestManager.QuestsListForReading.SingleOrDefault(q => q.root == QuestScriptDefOf.ROT_TradeShipMakeAmends && !q.Historical);
    public static bool IsMakingAmends => AmendsQuest != null;
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
