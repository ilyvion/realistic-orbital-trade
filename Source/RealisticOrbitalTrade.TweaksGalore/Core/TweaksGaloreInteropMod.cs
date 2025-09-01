using System.Reflection;
using Verse;

namespace RealisticOrbitalTrade.TweaksGalore;

public class TweaksGaloreInteropMod : Mod
{
    public TweaksGaloreInteropMod(ModContentPack content)
        : base(content)
    {
        new Harmony(Constants.Id).PatchAll(Assembly.GetExecutingAssembly());

        RealisticOrbitalTradeMod.Message("\"Tweaks Galore\" interop loaded successfully!");
    }
}
