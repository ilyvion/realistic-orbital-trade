namespace RealisticOrbitalTrade.TweaksGalore;

/// <summary>
/// The main mod class for Realistic Orbital Trade's Tweaks Galore integration.
/// </summary>
public class TweaksGaloreInteropMod : Mod
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TweaksGaloreInteropMod"/> class.
    /// </summary>
    /// <param name="content">The mod content pack.</param>
    public TweaksGaloreInteropMod(ModContentPack content)
        : base(content)
    {
        new Harmony(Constants.Id).PatchAll(Assembly.GetExecutingAssembly());

        RealisticOrbitalTradeMod.Message("\"Tweaks Galore\" interop loaded successfully!");
    }
}
