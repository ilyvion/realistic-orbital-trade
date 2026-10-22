namespace RealisticOrbitalTrade.TweaksGalore;

/// <summary>
/// The main mod class for Realistic Orbital Trade's Tweaks Galore integration.
/// </summary>
public partial class TweaksGaloreInteropMod
{
    partial void Construct()
    {
        new Harmony(Constants.Id).PatchAll(Assembly.GetExecutingAssembly());

        RealisticOrbitalTradeMod.Instance.LogMessage(
            "\"Tweaks Galore\" interop loaded successfully!"
        );
    }
}
