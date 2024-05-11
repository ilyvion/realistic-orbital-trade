using System.Collections.Generic;
using RimWorld;
using Verse;

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
    public static RealisticOrbitalTradeGameComponent Current => VerseCurrent.Game.GetComponent<RealisticOrbitalTradeGameComponent>();
    private int _nextTradeID;

    private bool _wasLoaded;

    private List<TradeAgreement> _tradeAgreements = new();

    private int _standing;
    public Standing Standing { get => (Standing)_standing; set => _standing = (int)value; }

    public RealisticOrbitalTradeGameComponent(Game _)
    {
    }

    public int GetNextTradeId()
    {
        if (Scribe.mode == LoadSaveMode.LoadingVars && !_wasLoaded)
        {
            RealisticOrbitalTradeMod.Warning("Getting next unique ID during LoadingVars before RealisticOrbitalTradeGameComponent was loaded. Assigning a random value.");
            return Rand.Int;
        }
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            RealisticOrbitalTradeMod.Warning("Getting next unique ID during saving. This may cause bugs.");
        }
        int result = _nextTradeID;
        _nextTradeID++;
        if (_nextTradeID == int.MaxValue)
        {
            RealisticOrbitalTradeMod.Warning("Next Trade ID is at max value. Resetting to 0. This may cause bugs.");
            _nextTradeID = 0;
        }
        return result;
    }

    public TradeAgreement StartTradeAgreement(TradeShip tradeShip, Pawn negotiator)
    {
        var tradeAgreement = new TradeAgreement(tradeShip, negotiator, Settings.ActiveTradePausesDepartureTimer);
        _tradeAgreements.Add(tradeAgreement);
        return tradeAgreement;
    }

    public void EndTradeAgreement(TradeAgreement tradeAgreement)
    {
        tradeAgreement.tradeShip.GetData().activeTradeAgreement = null;
        _tradeAgreements.Remove(tradeAgreement);
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
