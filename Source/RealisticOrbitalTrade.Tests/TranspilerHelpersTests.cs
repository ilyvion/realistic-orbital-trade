using System.Reflection.Emit;
using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class TranspilerHelpersTests
{
    private static void Target() { }

    private static readonly MethodInfo TargetMethod =
        typeof(TranspilerHelpersTests).GetMethod(
            nameof(Target),
            BindingFlags.NonPublic | BindingFlags.Static
        ) ?? throw new MissingMethodException(nameof(TranspilerHelpersTests), nameof(Target));

    [Test]
    public static void InjectsDirectlyBeforeLastMatchWithExtrasBeforeCall()
    {
        var nop1 = new CodeInstruction(OpCodes.Nop);
        var ldc = new CodeInstruction(OpCodes.Ldc_I4_1);
        var extra = new CodeInstruction(OpCodes.Ldarg_0);
        var ret = new CodeInstruction(OpCodes.Ret);

        var result = Utils
            .InjectCallBeforeReturn(
                [nop1, ldc, new CodeInstruction(OpCodes.Ldc_I4_1), ret],
                TargetMethod,
                i => i.opcode == OpCodes.Ldc_I4_1,
                [extra]
            )
            .ToList();

        Expect.AreEqual(6, result.Count);
        Expect.AreEqual(OpCodes.Ldc_I4_1, result[1].opcode);
        Expect.AreEqual(OpCodes.Ldarg_0, result[2].opcode);
        Expect.AreEqual(OpCodes.Call, result[3].opcode);
        Expect.AreEqual(TargetMethod, (MethodInfo)result[3].operand);
        Expect.AreEqual(OpCodes.Ldc_I4_1, result[4].opcode);
        Expect.AreEqual(OpCodes.Ret, result[5].opcode);
    }

    [Test]
    public static void NoMatchThrowsWithInstructions()
    {
        var instructions = new[]
        {
            new CodeInstruction(OpCodes.Nop),
            new CodeInstruction(OpCodes.Ret),
        };
        InjectCallBeforeReturnException? caught = null;
        try
        {
            _ = Utils
                .InjectCallBeforeReturn(instructions, TargetMethod, i => i.opcode == OpCodes.Pop)
                .ToList();
        }
        catch (InjectCallBeforeReturnException e)
        {
            caught = e;
        }

        Expect.IsNotNull(caught);
        Expect.AreEqual(2, caught!.Instructions.Count());
    }

    [Test]
    public static void NotEndingInRetThrows()
    {
        InjectCallBeforeReturnException? caught = null;
        try
        {
            _ = Utils
                .InjectCallBeforeReturn(
                    [new CodeInstruction(OpCodes.Nop), new CodeInstruction(OpCodes.Throw)],
                    TargetMethod,
                    i => i.opcode == OpCodes.Nop
                )
                .ToList();
        }
        catch (InjectCallBeforeReturnException e)
        {
            caught = e;
        }

        Expect.IsNotNull(caught);
    }

    [Test]
    public static void WidenShortBranchesMapsAllShortOpcodesAndKeepsOperands()
    {
        (OpCode Short, OpCode Long)[] pairs =
        [
            (OpCodes.Br_S, OpCodes.Br),
            (OpCodes.Brfalse_S, OpCodes.Brfalse),
            (OpCodes.Brtrue_S, OpCodes.Brtrue),
            (OpCodes.Beq_S, OpCodes.Beq),
            (OpCodes.Bne_Un_S, OpCodes.Bne_Un),
            (OpCodes.Bge_S, OpCodes.Bge),
            (OpCodes.Bgt_S, OpCodes.Bgt),
            (OpCodes.Ble_S, OpCodes.Ble),
            (OpCodes.Blt_S, OpCodes.Blt),
            (OpCodes.Bge_Un_S, OpCodes.Bge_Un),
            (OpCodes.Bgt_Un_S, OpCodes.Bgt_Un),
            (OpCodes.Ble_Un_S, OpCodes.Ble_Un),
            (OpCodes.Blt_Un_S, OpCodes.Blt_Un),
            (OpCodes.Leave_S, OpCodes.Leave),
        ];
        var label = new Label();
        var input = pairs.Select(p => new CodeInstruction(p.Short, label)).ToList();
        input.Add(new CodeInstruction(OpCodes.Nop));
        input.Add(new CodeInstruction(OpCodes.Br, label));

        var result = Utils.WidenShortBranches(input).ToList();

        for (var i = 0; i < pairs.Length; i++)
        {
            Expect.AreEqual(pairs[i].Long, result[i].opcode);
            Expect.AreEqual(label, (Label)result[i].operand);
        }
        Expect.AreEqual(OpCodes.Nop, result[pairs.Length].opcode);
        Expect.AreEqual(OpCodes.Br, result[pairs.Length + 1].opcode);
    }
}
