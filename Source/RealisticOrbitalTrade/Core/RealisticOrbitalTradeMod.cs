[assembly: InternalsVisibleTo("RealisticOrbitalTrade.TweaksGalore")]
[assembly: InternalsVisibleTo("RealisticOrbitalTrade.WeHadATrader")]
[assembly: InternalsVisibleTo("RealisticOrbitalTrade.DynamicTradeInterface")]
[assembly: InternalsVisibleTo("RealisticOrbitalTrade.AutoSeller")]
[assembly: InternalsVisibleTo("RealisticOrbitalTrade.SocialEXPFromTrade")]
[assembly: InternalsVisibleTo("RealisticOrbitalTrade.SocialEXPFromTrade.Tests")]
[assembly: InternalsVisibleTo("RealisticOrbitalTrade.Tests")]

namespace RealisticOrbitalTrade;

internal partial class RealisticOrbitalTradeMod
{
    partial void Construct()
    {
        //Harmony.DEBUG = true;
        new Harmony(Constants.Id).PatchAll(Assembly.GetExecutingAssembly());
        //Harmony.DEBUG = false;

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
