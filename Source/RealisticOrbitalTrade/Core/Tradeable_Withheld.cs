using RealisticOrbitalTrade.Comps;

namespace RealisticOrbitalTrade;

/// <summary>
/// A tradeable part of whose colony stock is still needed by other trade agreements (see
/// <see cref="WithholdCommittedStock"/>); that part can't be sold again.
/// </summary>
internal class Tradeable_Withheld : Tradeable
{
    /// <summary>How much of the colony stock other trade agreements still need.</summary>
    public int withheldCount;

    /// <summary>
    /// How much of each colony stack other trade agreements have reserved or a hauler has claimed
    /// for them; a sale takes from the rest of the stock first.
    /// </summary>
    public Dictionary<Thing, int> claimedStacks = [];

    internal int ClaimedElsewhere(Thing thing) =>
        claimedStacks.TryGetValue(thing, out var count) ? Math.Min(count, thing.stackCount) : 0;

    public override int CountHeldBy(Transactor trans)
    {
        var held = base.CountHeldBy(trans);
        return trans == Transactor.Colony ? Math.Max(0, held - withheldCount) : held;
    }

    public override void ResolveTrade()
    {
        if (claimedStacks.Count == 0 || ActionToDo != TradeAction.PlayerSells)
        {
            base.ResolveTrade();
            return;
        }

        var (allocation, _) = TradeAgreement.AllocateUnsoldStock(
            thingsColony.Select(thing => (thing, thing.stackCount)),
            CountToTransferToDestination,
            ClaimedElsewhere
        );
        foreach (var (thing, count) in allocation)
        {
            TradeSession.trader.GiveSoldThingToTrader(thing, count, TradeSession.playerNegotiator);
        }
    }

    /// <summary>
    /// Takes the colony stock that trade agreements other than <paramref name="except"/> still
    /// need out of <paramref name="tradeables"/>: sold pawns are removed, other stock is withheld
    /// from what can be sold, and a tradeable left with nothing to trade or agreed on is removed.
    /// </summary>
    internal static void WithholdCommittedStock(List<Tradeable> tradeables, TradeAgreement? except)
    {
        var otherAgreements =
            Current
                .Game?.GetComponent<RealisticOrbitalTradeGameComponent>()
                ?.ActiveTradeAgreements.Where(agreement => agreement != except)
                .ToList()
            ?? [];
        if (otherAgreements.Count == 0)
        {
            return;
        }

        var soldPawns = otherAgreements
            .SelectMany(agreement => agreement.pawnsSoldToTrader)
            .OfType<Pawn>()
            .ToHashSet();
        var claimedStacks = StacksClaimedByTrades(otherAgreements);

        for (var i = tradeables.Count - 1; i >= 0; i--)
        {
            var tradeable = tradeables[i];
            if (tradeable is Tradeable_Pawn)
            {
                _ = tradeable.thingsColony.RemoveAll(thing => soldPawns.Contains(thing));
            }
            else if (tradeable.thingsColony.Count > 0)
            {
                var withheldCount = otherAgreements.Sum(agreement =>
                    agreement.StillNeededFromColony(tradeable)
                );
                if (withheldCount > 0)
                {
                    tradeable = WithWithheld(tradeable, withheldCount, claimedStacks);
                    tradeables[i] = tradeable;
                }
            }

            if (
                tradeable.thingsTrader.Count == 0
                && tradeable.CountToTransfer == 0
                && (
                    tradeable.thingsColony.Count == 0
                    || (
                        tradeable is Tradeable_Withheld { withheldCount: > 0 } withheld
                        && withheld.CountHeldBy(Transactor.Colony) == 0
                    )
                )
            )
            {
                tradeables.RemoveAt(i);
            }
        }
    }

    private static Tradeable WithWithheld(
        Tradeable tradeable,
        int withheldCount,
        Dictionary<Thing, int> claimedStacks
    )
    {
        if (tradeable is not Tradeable_Withheld withheld)
        {
            if (tradeable.GetType() != typeof(Tradeable))
            {
                return tradeable;
            }
            withheld = new Tradeable_Withheld
            {
                thingsColony = tradeable.thingsColony,
                thingsTrader = tradeable.thingsTrader,
            };
            withheld.ForceTo(tradeable.CountToTransfer);
        }
        withheld.withheldCount = withheldCount;
        withheld.claimedStacks = claimedStacks;
        return withheld;
    }

    /// <summary>
    /// How much of each colony stack <paramref name="agreements"/> have reserved, or a hauler
    /// loading one of their shuttles has claimed and not yet picked up.
    /// </summary>
    private static Dictionary<Thing, int> StacksClaimedByTrades(List<TradeAgreement> agreements)
    {
        Dictionary<Thing, int> claimed = [];
        void add(Thing? thing, int count)
        {
            if (thing != null && count > 0)
            {
                claimed[thing] =
                    (claimed.TryGetValue(thing, out var existing) ? existing : 0) + count;
            }
        }

        foreach (var agreement in agreements)
        {
            foreach (var reserved in agreement.reservedTraderThings)
            {
                add(reserved.thing, reserved.Count);
            }
        }

        var maps = agreements.Select(agreement => agreement.tradeShip?.Map).OfType<Map>();
        foreach (var map in maps.Distinct())
        {
            foreach (var pawn in map.mapPawns.AllPawnsSpawned)
            {
                var agreement = JobDriver_HaulMultipleToTransporter
                    .HaulingTransporter(pawn)
                    ?.TryGetComp<CompTradeShuttle>()
                    ?.tradeAgreement;
                if (agreement == null || !agreements.Contains(agreement))
                {
                    continue;
                }
                foreach (
                    var (thing, count) in JobDriver_HaulMultipleToTransporter.ClaimedThings(pawn)
                )
                {
                    add(thing, count);
                }
            }
        }

        return claimed;
    }
}
