using VerseCurrent = Verse.Current;

namespace RealisticOrbitalTrade;

internal enum Standing
{
    Good,
    Blacklisted,
    Forgiven,
}

#pragma warning disable CS9113 // Parameter is unread.
internal class RealisticOrbitalTradeGameComponent(Game _game) : GameComponent
#pragma warning restore CS9113 // Parameter is unread.
{
    public static RealisticOrbitalTradeGameComponent Current =>
        VerseCurrent.Game.GetComponent<RealisticOrbitalTradeGameComponent>();
    private int _nextTradeID;

    private bool _wasLoaded;

    private List<TradeAgreement> _tradeAgreements = [];

    private int _standing;
    public Standing Standing
    {
        get => (Standing)_standing;
        set => _standing = (int)value;
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
        _nextTradeID++;
        if (_nextTradeID == int.MaxValue)
        {
            RealisticOrbitalTradeMod.Instance.LogWarning(
                "Next Trade ID is at max value. Resetting to 0. This may cause bugs."
            );
            _nextTradeID = 0;
        }
        return result;
    }

    public TradeAgreement StartTradeAgreement(TradeShip tradeShip, Pawn negotiator)
    {
        RealisticOrbitalTradeMod.Instance.LogDevMessage(() =>
            $"Starting trade agreement with {tradeShip} using {negotiator}"
        );
        var tradeAgreement = new TradeAgreement(
            tradeShip,
            negotiator,
            Settings._activeTradePausesDepartureTimer
        );
        _tradeAgreements.Add(tradeAgreement);
        return tradeAgreement;
    }

    public void EndTradeAgreement(TradeAgreement tradeAgreement)
    {
        RealisticOrbitalTradeMod.Instance.LogDevMessage(() =>
            $"Ending trade agreement with {tradeAgreement.tradeShip} using {tradeAgreement.negotiator}"
        );
        tradeAgreement.tradeShip.GetData().activeTradeAgreement = null;
        _ = _tradeAgreements.Remove(tradeAgreement);
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref _nextTradeID, "nextTradeID", 0);
        Scribe_Collections.Look(ref _tradeAgreements, "tradeAgreements", LookMode.Deep);
        Scribe_Values.Look(ref _standing, "standing");

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            _wasLoaded = true;
        }
    }
}
