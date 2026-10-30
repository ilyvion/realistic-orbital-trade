using System.Reflection.Emit;
using System.Text;
using DevTools.Testing;
using RealisticOrbitalTrade.Patch;

namespace RealisticOrbitalTrade.Tests;

// The transpilers on vanilla methods find the locals they use from the IL around them, so they
// keep patching the right local when a build lays the method's locals out differently.
[TestFixture(TestType.MainMenu)]
internal sealed class VanillaTranspilerLocalsTests
{
    private static readonly int[] Shifts = [0, 1];

    private static readonly MethodInfo RectWidthGetter = AccessTools.PropertyGetter(
        typeof(Rect),
        nameof(Rect.width)
    );

    private static readonly MethodInfo TextWordWrapSetter = AccessTools.PropertySetter(
        typeof(Text),
        nameof(Text.WordWrap)
    );

    private static List<CodeInstruction> Original(MethodBase method, int shift) =>
        ShiftedLocals.Shift(PatchProcessor.GetOriginalInstructions(method), shift);

    private static ILGenerator NewGenerator() =>
        new DynamicMethod("Generator", typeof(void), Type.EmptyTypes).GetILGenerator();

    private static int IndexOfCall(List<CodeInstruction> instructions, string methodName)
    {
        var matches = instructions
            .Select((instruction, index) => (instruction, index))
            .Where(p => p.instruction.operand is MethodInfo m && m.Name == methodName)
            .ToList();
        Expect.AreEqual(1, matches.Count);
        return matches[0].index;
    }

    private static int LocalStoredAfter(
        List<CodeInstruction> instructions,
        Func<CodeInstruction, bool> predicate
    ) => instructions[instructions.FindIndex(i => predicate(i)) + 1].LocalVariableIndex();

    private static int IndexWhere(List<CodeInstruction> instructions, Func<int, bool> predicate) =>
        Enumerable.Range(0, instructions.Count - 1).First(predicate);

    private static void ExpectStores(CodeInstruction instruction, int index)
    {
        Expect.IsTrue(instruction.IsStloc());
        Expect.AreEqual(index, instruction.LocalVariableIndex());
    }

    [Test]
    public static void RequiredThingsLabelPassesTheStringBuilderLocal()
    {
        foreach (var shift in Shifts)
        {
            var original = Original(
                AccessTools.PropertyGetter(
                    typeof(CompShuttle),
                    nameof(CompShuttle.RequiredThingsLabel)
                ),
                shift
            );
            var stringBuilder = LocalStoredAfter(
                original,
                i => i.CallsConstructorOf(typeof(StringBuilder))
            );

            var result = Rimworld_CompShuttle_RequiredThingsLabel.Transpiler(original).ToList();

            var call = IndexOfCall(result, "InjectThingsLabel");
            Expect.AreEqual(OpCodes.Ldarg_0, result[call - 2].opcode);
            Expect.IsTrue(result[call - 1].LoadsLocal(stringBuilder));
            Expect.IsTrue(result[call + 1].LoadsLocal(stringBuilder));
        }
    }

    [Test]
    public static void DoThingRowReplacesTheLabelLocal()
    {
        foreach (var shift in Shifts)
        {
            var original = Original(
                AccessTools.Method(typeof(ITab_ContentsBase), "DoThingRow"),
                shift
            );
            var wordWrap = original.FindIndex(i => i.Calls(TextWordWrapSetter));
            var label = original[wordWrap - 2].LocalVariableIndex();

            var result = Rimworld_ITab_ContentsBase_DoThingRow_Reverse
                .TranspileDoThingRow(original)
                .ToList();

            var call = IndexOfCall(result, "FixLabel");
            Expect.IsTrue(result[call - 4].LoadsLocal(label));
            ExpectStores(result[call + 1], label);
        }
    }

    [Test]
    public static void DrawTradeableRowShrinksTheWidthLocal()
    {
        foreach (var shift in Shifts)
        {
            var original = Original(
                AccessTools.Method(typeof(TradeUI), nameof(TradeUI.DrawTradeableRow)),
                shift
            );
            var width = LocalStoredAfter(original, i => i.Calls(RectWidthGetter));

            var result = Rimworld_TradeUI_DrawTradeableRow
                .Transpiler(original, NewGenerator())
                .ToList();

            var call = IndexOfCall(result, "DrawMassColumnAndShrinkWidth");
            Expect.IsTrue(result[call - 1].LoadsLocal(width));
            ExpectStores(result[call + 1], width);
        }
    }

    [Test]
    public static void DialogTradeDrawsThresholdRowWithTheCurrencyRowWidthLocal()
    {
        foreach (var shift in Shifts)
        {
            var original = Original(
                AccessTools.Method(typeof(Dialog_Trade), nameof(Dialog_Trade.DoWindowContents)),
                shift
            );
            var subtract16 = original.FindIndex(i =>
                i.opcode == OpCodes.Ldc_R4 && i.operand is float f && Math.Abs(f - 16f) < 0.1f
            );
            var width = original[subtract16 + 2].LocalVariableIndex();
            // currencyRowYShift = 30f;
            var set30 = IndexWhere(
                original,
                k => original[k].LoadsConstant(30.0) && original[k + 1].IsStloc()
            );
            var yShift = original[set30 + 1].LocalVariableIndex();

            var result = Rimworld_Dialog_Trade_DoWindowContents
                .Transpiler(original, NewGenerator())
                .ToList();

            var call = IndexOfCall(result, "DrawThresholdRow");
            Expect.IsTrue(result[call - 1].LoadsLocal(width));
            Expect.IsTrue(result[call + 1].IsStloc());
            var shiftAmount = result[call + 1].LocalVariableIndex();

            // currencyRowYShift = 30f + shiftAmount;
            var shifted30 = IndexWhere(
                result,
                k => result[k].LoadsConstant(30.0) && result[k + 1].LoadsLocal(shiftAmount)
            );
            Expect.AreEqual(OpCodes.Add, result[shifted30 + 2].opcode);
            ExpectStores(result[shifted30 + 3], yShift);
        }
    }
}
