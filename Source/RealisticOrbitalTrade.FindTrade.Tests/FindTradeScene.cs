using MGAutoSell;
using MGAutoSell.Filter;
using MGAutoSell.HarmonyPatches;
using RealisticOrbitalTrade.Tests;
using TD_Find_Lib;
using FindTradeMod = MGAutoSell.Mod;

namespace RealisticOrbitalTrade.FindTrade.Tests;

// A TradeScene with Find / Trade set up to auto barter: no trade rules of its own, only a disabled
// placeholder so Find / Trade sells what's marked as junk, no ship already bartered with, and known
// values for Find / Trade's settings. Disposing puts back Find / Trade's state, then the scene's.
internal sealed class FindTradeScene : IDisposable
{
    private const string PlaceholderRuleName = "ROT test placeholder";

    private readonly TradeRulesGameComp comp = Current.Game.GetComponent<TradeRulesGameComp>();
    private readonly List<TradeRule> previousTradeRules;
    private readonly HashSet<ITrader> previousTraders;
    private readonly bool previousAutoTrade;
    private readonly HashSet<int> previousAutoTraderIDs;
    private readonly FindTradeSettings previousSettings = FindTradeSettings.Capture();

    internal TradeScene Scene { get; }

    internal HashSet<ITrader> Traders => comp.traders;

    // Runs on Find / Trade's and the mod's default settings unless the test passes its own.
    internal FindTradeScene(
        Map map,
        TradeScene.SettingsSnapshot? settings = null,
        FindTradeSettings? findTradeSettings = null
    )
    {
        previousTradeRules = [.. comp.tradeRules];
        previousTraders = [.. comp.traders];
        previousAutoTrade = comp.autoTrade;
        previousAutoTraderIDs = [.. comp.autoTraderIDs];

        // Find / Trade only trades when it has a rule; a disabled one sells nothing on its own.
        comp.tradeRules.Clear();
        comp.tradeRules.Add(new TradeRule(PlaceholderRuleName) { Enabled = false });
        comp.traders.Clear();
        comp.autoTrade = false;
        comp.autoTraderIDs.Clear();
        (findTradeSettings ?? FindTradeSettings.Defaults).Apply();

        Scene = new TradeScene(map, settings);
    }

    // Marks every stack of the def on the map as junk, which Find / Trade sells whole.
    internal void MarkJunk(ThingDef def)
    {
        foreach (var thing in Scene.Map.listerThings.ThingsOfDef(def))
        {
            Scene.Map.designationManager.AddDesignation(
                new Designation(thing, MGDesignatorDefOf.MGAutoSell)
            );
        }
    }

    // Adds an enabled rule that buys the def up to this many.
    internal void AddImportRule(ThingDef def, int import)
    {
        var rule = new TradeRule($"ROT test import {def.defName}")
        {
            Mode = TradeMode.Import,
            Import = import,
        };
        var query = ThingQueryMaker.MakeQuery<ThingQueryThingDef>();
        query.sel = def;
        rule.search.Children.Add(query);
        comp.tradeRules.Add(rule);
    }

    // Runs one auto barter pass with every ship in orbit, as the barter job does.
    internal void Barter() => TradeDealProcessor.DoTradeShips(Scene.Negotiator);

    public void Dispose()
    {
        comp.tradeRules.Clear();
        comp.tradeRules.AddRange(previousTradeRules);
        comp.traders.Clear();
        comp.traders.UnionWith(previousTraders);
        comp.autoTrade = previousAutoTrade;
        comp.autoTraderIDs.Clear();
        comp.autoTraderIDs.UnionWith(previousAutoTraderIDs);
        previousSettings.Apply();
        AddRuleLabelsToTradeUI.ExtraLabels.Clear();

        Scene.Dispose();
    }

    // Find / Trade's settings that change how it trades, so a test runs on known values instead of
    // whatever the player has saved, and the player's values come back afterwards.
    internal readonly record struct FindTradeSettings(bool ScanEveryStack, bool RememberManualTrade)
    {
        // Find / Trade's own defaults.
        internal static FindTradeSettings Defaults { get; } =
            new(ScanEveryStack: true, RememberManualTrade: true);

        internal static FindTradeSettings Capture() =>
            new(FindTradeMod.Settings.scanEveryStack, FindTradeMod.Settings.rememberManualTrade);

        internal void Apply()
        {
            FindTradeMod.Settings.scanEveryStack = ScanEveryStack;
            FindTradeMod.Settings.rememberManualTrade = RememberManualTrade;
        }
    }
}
