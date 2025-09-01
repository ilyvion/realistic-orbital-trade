using System.Reflection;
using Verse;

namespace RealisticOrbitalTrade.DynamicTradeInterface;

public class DynamicTradeInterfaceMod : Mod
{
    public DynamicTradeInterfaceMod(ModContentPack content)
        : base(content)
    {
        new Harmony(Constants.Id).PatchAll(Assembly.GetExecutingAssembly());

        RealisticOrbitalTradeMod.Message(
            "\"Dynamic Trade Interface\" interop loaded successfully!"
        );
    }
}
