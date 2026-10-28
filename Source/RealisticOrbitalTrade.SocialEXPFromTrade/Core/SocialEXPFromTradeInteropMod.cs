namespace RealisticOrbitalTrade.SocialEXPFromTrade;

/// <summary>
/// The main mod class for Realistic Orbital Trade's Social Experience from Trade integration.
/// </summary>
public partial class SocialEXPFromTradeInteropMod
{
    partial void Construct()
    {
        _ = PatchAll(Constants.Id);

        RealisticOrbitalTradeGameComponent.Created += SocialExpGrantTracker.Reset;
        RealisticOrbitalTradeGameComponent.ExposeDataExtra += SocialExpGrantTracker.ExposeData;
        RealisticOrbitalTradeGameComponent.TradeAgreementEnded += SocialExpGrantTracker.Resolve;

        RealisticOrbitalTradeMod.Instance.LogMessage(
            "\"Social Experience from Trade\" interop loaded successfully!"
        );
    }
}
