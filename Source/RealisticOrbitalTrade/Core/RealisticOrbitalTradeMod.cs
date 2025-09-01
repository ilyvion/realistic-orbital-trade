[assembly: InternalsVisibleTo("RealisticOrbitalTrade.TweaksGalore")]
[assembly: InternalsVisibleTo("RealisticOrbitalTrade.WeHadATrader")]
[assembly: InternalsVisibleTo("RealisticOrbitalTrade.DynamicTradeInterface")]
[assembly: InternalsVisibleTo("RealisticOrbitalTrade.AutoSeller")]

namespace RealisticOrbitalTrade;

internal partial class RealisticOrbitalTradeMod
{
    partial void Construct()
    {
        // if (!ModsConfig.RoyaltyActive)
        // {
        //     Error("Realistic Orbital Trade requires the Royalty DLC to be active to work. The mod's functionality has therefore been disabled and trade will behave as in vanilla.");

        // }
        // else
        // {
        //Harmony.DEBUG = true;
        new Harmony(Constants.Id).PatchAll(Assembly.GetExecutingAssembly());
        //Harmony.DEBUG = false;
        //}

        _ = GetSettings<Settings>();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        base.DoSettingsWindowContents(inRect);
        Settings.DoSettingsWindowContents(inRect);
    }

    protected override bool HasSettings => true;
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
internal sealed class HotSwappableAttribute : Attribute { }
