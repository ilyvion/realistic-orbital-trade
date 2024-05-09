using HarmonyLib;
using Verse;
using System.Reflection;

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
