using System.Reflection.Emit;

namespace RealisticOrbitalTrade.Tests;

// Renumbers every local variable access in a method body, the way a build of the same code that
// declares extra locals ahead of them (e.g. a Debug build) lays them out.
internal static class ShiftedLocals
{
    internal static List<CodeInstruction> Shift(
        IEnumerable<CodeInstruction> instructions,
        int by
    ) => [.. instructions.Select(i => ShiftOne(i, by))];

    private static CodeInstruction ShiftOne(CodeInstruction instruction, int by)
    {
        OpCode opcode;
        if (instruction.IsStloc())
        {
            opcode = OpCodes.Stloc_S;
        }
        else if (instruction.opcode == OpCodes.Ldloca || instruction.opcode == OpCodes.Ldloca_S)
        {
            opcode = OpCodes.Ldloca_S;
        }
        else if (instruction.IsLdloc())
        {
            opcode = OpCodes.Ldloc_S;
        }
        else
        {
            return instruction;
        }

        return new CodeInstruction(opcode, (byte)(instruction.LocalVariableIndex() + by))
        {
            labels = instruction.labels,
            blocks = instruction.blocks,
        };
    }
}
