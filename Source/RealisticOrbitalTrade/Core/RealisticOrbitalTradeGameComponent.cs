using VerseCurrent = Verse.Current;

namespace RealisticOrbitalTrade;

internal enum Standing
{
    Good,
    Blacklisted,
    Forgiven,
}

internal class RealisticOrbitalTradeGameComponent : GameComponent
{
#pragma warning disable IDE0060 // Remove unused parameter
    public RealisticOrbitalTradeGameComponent(Game game)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        Created?.Invoke();
    }

    public static RealisticOrbitalTradeGameComponent Current =>
        VerseCurrent.Game.GetComponent<RealisticOrbitalTradeGameComponent>();
    internal int _nextTradeID;

    private bool _wasLoaded;

    private List<TradeAgreement> _tradeAgreements = [];

    public IReadOnlyList<TradeAgreement> ActiveTradeAgreements => _tradeAgreements;

    private int _standing;
    public Standing Standing
    {
        get => (Standing)_standing;
        set => _standing = (int)value;
    }

    private Quest? _amendsQuest;

    /// <summary>
    /// The active "make amends" quest, if any. Set directly by
    /// <see cref="Quests.QuestNode_Root_TradeShipMakeAmends"/> when the quest is generated.
    /// Cleared lazily once the quest goes historical.
    /// </summary>
    public Quest? AmendsQuest
    {
        get => Utils.GetLiveOrClear(ref _amendsQuest, q => q.Historical);
        set => _amendsQuest = value;
    }

    public int GetNextTradeId()
    {
        if (Scribe.mode == LoadSaveMode.LoadingVars && !_wasLoaded)
        {
            RealisticOrbitalTradeMod.Instance.LogWarning(
                "Getting next unique ID during LoadingVars before RealisticOrbitalTradeGameComponent was loaded. Assigning a random value."
            );
            return Rand.Int;
        }
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            RealisticOrbitalTradeMod.Instance.LogWarning(
                "Getting next unique ID during saving. This may cause bugs."
            );
        }
        var result = _nextTradeID;
        _nextTradeID = AdvanceTradeId(result);
        if (_nextTradeID == 0 && result != -1)
        {
            RealisticOrbitalTradeMod.Instance.LogWarning(
                "Next Trade ID is at max value. Resetting to 0. This may cause bugs."
            );
        }
        return result;
    }

    /// <summary>The id handed out after <paramref name="id"/>; wraps to 0 before reaching <see cref="int.MaxValue"/>.</summary>
    internal static int AdvanceTradeId(int id) => id + 1 == int.MaxValue ? 0 : id + 1;

    public TradeAgreement StartTradeAgreement(TradeShip tradeShip, Pawn negotiator)
    {
        RealisticOrbitalTradeMod.Instance.LogDevMessage(() =>
            $"Starting trade agreement with {tradeShip} using {negotiator}"
        );
        var tradeAgreement = new TradeAgreement(
            tradeShip,
            negotiator,
            Settings._activeTradePausesDepartureTimer,
            Settings._deliverItemsViaDropPod
        );
        _tradeAgreements.Add(tradeAgreement);
        return tradeAgreement;
    }

    /// <summary>
    /// Fired whenever a trade agreement is ended, once its outcome (delivered successfully, or
    /// cancelled/failed) is known - see <see cref="EndTradeAgreement"/>'s <c>successful</c>
    /// parameter. Third-party integrations that grant a reward for completing an orbital trade
    /// (e.g. Social Experience from Trade) can subscribe to this to defer that reward until
    /// delivery actually succeeds, instead of granting it the moment the trade dialog is accepted.
    /// </summary>
    public static event Action<TradeAgreement, bool>? TradeAgreementEnded;

    /// <summary>
    /// Fired once from this type's constructor whenever a new instance is created - which
    /// happens exactly once per <see cref="Game"/> session, for a freshly started game and a
    /// loaded one alike. Integrations that hold their own per-save state in static fields
    /// instead of registering a <see cref="GameComponent"/> of their own (see
    /// <see cref="ExposeDataExtra"/>) should subscribe here to reset that state before this
    /// component's <see cref="ExposeData"/> (if any - a new game triggers no Scribe pass) has a
    /// chance to reload it, so state left over from a previous session isn't carried into a new
    /// one.
    /// </summary>
    public static event Action? Created;

    /// <summary>
    /// Fired from within <see cref="ExposeData"/>, after this component's own fields have been
    /// scribed. Integrations that need to persist their own per-save state can Scribe it here,
    /// nested under this component's node, instead of registering a <see cref="GameComponent"/>
    /// of their own - a type supplied by another mod's assembly that, once that mod is removed,
    /// causes an error when loading any save made while it was still active.
    /// </summary>
    public static event Action? ExposeDataExtra;

    public void EndTradeAgreement(TradeAgreement tradeAgreement, bool successful = false)
    {
        RealisticOrbitalTradeMod.Instance.LogDevMessage(() =>
            $"Ending trade agreement with {tradeAgreement.tradeShip} using {tradeAgreement.negotiator} (successful: {successful})"
        );
        tradeAgreement.tradeShip.GetData().activeTradeAgreement = null;
        _ = _tradeAgreements.Remove(tradeAgreement);
        TradeAgreementEnded?.Invoke(tradeAgreement, successful);
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref _nextTradeID, "nextTradeID", 0);
        Scribe_Collections.Look(ref _tradeAgreements, "tradeAgreements", LookMode.Deep);
        Scribe_Values.Look(ref _standing, "standing");
        Scribe_References.Look(ref _amendsQuest, "amendsQuest");

        ExposeDataExtra?.Invoke();

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            _wasLoaded = true;
        }
    }
}
