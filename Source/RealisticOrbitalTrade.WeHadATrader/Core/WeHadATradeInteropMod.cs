using System.Reflection;
using Verse;

namespace RealisticOrbitalTrade.WeHadATrader;

public class WeHadATraderInteropMod : Mod
{
    public WeHadATraderInteropMod(ModContentPack content)
        : base(content)
    {
        new Harmony(Constants.Id).PatchAll(Assembly.GetExecutingAssembly());

        RealisticOrbitalTradeMod.Message("\"We Had a Trader?\" interop loaded successfully!");
    }
}
