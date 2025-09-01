namespace RealisticOrbitalTrade.WeHadATrader;

/// <summary>
/// The main mod class for Realistic Orbital Trade's We Had a Trader? integration.
/// </summary>
public class WeHadATraderInteropMod : IlyvionMod
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WeHadATraderInteropMod"/> class.
    /// </summary>
    /// <param name="content">The mod content pack.</param>
    public WeHadATraderInteropMod(ModContentPack content)
        : base(content)
    {
        new Harmony(Constants.Id).PatchAll(Assembly.GetExecutingAssembly());

        RealisticOrbitalTradeMod.Instance.LogMessage(
            "\"We Had a Trader?\" interop loaded successfully!"
        );
    }
}
