namespace RealisticOrbitalTrade.SocialEXPFromTrade;

/// <summary>
/// The main mod class for Realistic Orbital Trade's Social Experience from Trade integration.
/// </summary>
public class SocialEXPFromTradeInteropMod : IlyvionMod
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SocialEXPFromTradeInteropMod"/> class.
    /// </summary>
    /// <param name="content">The mod content pack.</param>
    public SocialEXPFromTradeInteropMod(ModContentPack content)
        : base(content)
    {
        new Harmony(Constants.Id).PatchAll(Assembly.GetExecutingAssembly());

        RealisticOrbitalTradeGameComponent.Created += SocialExpGrantTracker.Reset;
        RealisticOrbitalTradeGameComponent.ExposeDataExtra += SocialExpGrantTracker.ExposeData;
        RealisticOrbitalTradeGameComponent.TradeAgreementEnded += SocialExpGrantTracker.Resolve;

        RealisticOrbitalTradeMod.Instance.LogMessage(
            "\"Social Experience from Trade\" interop loaded successfully!"
        );
    }
}
