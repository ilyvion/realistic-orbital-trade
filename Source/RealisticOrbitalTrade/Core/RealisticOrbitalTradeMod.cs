using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("RealisticOrbitalTrade.TweaksGalore")]
[assembly: InternalsVisibleTo("RealisticOrbitalTrade.WeHadATrader")]
[assembly: InternalsVisibleTo("RealisticOrbitalTrade.DynamicTradeInterface")]
[assembly: InternalsVisibleTo("RealisticOrbitalTrade.AutoSeller")]

namespace RealisticOrbitalTrade;

internal class RealisticOrbitalTradeMod : Mod
{
#pragma warning disable CS8618 // Set by constructor
    internal static RealisticOrbitalTradeMod instance;
#pragma warning restore CS8618

    public RealisticOrbitalTradeMod(ModContentPack content) : base(content)
    {
        instance = this;

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

        GetSettings<Settings>();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        base.DoSettingsWindowContents(inRect);
        Settings.DoSettingsWindowContents(inRect);
    }

    public override string SettingsCategory()
    {
        return Content.Name;
    }

    public static void Message(string msg)
    {
        Log.Message("[Realistic Orbital Trade] " + msg);
    }

    public static void Dev(string msg)
    {
        if (Prefs.DevMode && Settings._printDevMessages)
        {
            Log.Message("[Realistic Orbital Trade][DEV] " + msg);
        }
    }

    public static void Dev(Func<string> produceMsg)
    {
        if (Prefs.DevMode && Settings._printDevMessages)
        {
            Log.Message("[Realistic Orbital Trade][DEV] " + produceMsg());
        }
    }

    public static void Warning(string msg)
    {
        Log.Warning("[Realistic Orbital Trade] " + msg);
    }

    public static void WarningOnce(string msg, int key)
    {
        Log.WarningOnce("[Realistic Orbital Trade] " + msg, Constants.MessageKeyBase + key);
    }

    public static void Error(string msg)
    {
        Log.Error("[Realistic Orbital Trade] " + msg);
    }

    public static void Exception(string msg, Exception? e = null)
    {
        Message(msg);
        if (e != null)
        {
            Log.Error(e.ToString());
        }
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class HotSwappableAttribute : Attribute
{
}
