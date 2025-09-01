using System.Reflection;
using Verse;

namespace RealisticOrbitalTrade.AutoSeller;

public class AutoSellerMod : Mod
{
    public AutoSellerMod(ModContentPack content)
        : base(content)
    {
        new Harmony(Constants.Id).PatchAll(Assembly.GetExecutingAssembly());

        RealisticOrbitalTradeMod.Message("\"AutoSeller\" interop loaded successfully!");
    }
}
