using RealisticOrbitalTrade.Comps;
using Verse.AI;
using RwThingDefOf = RimWorld.ThingDefOf;

namespace RealisticOrbitalTrade;

[HotSwappable]
internal class JobDriver_BuyMoreTime : JobDriver
{
    private ThingWithComps Shuttle => (ThingWithComps)job.GetTarget(TargetIndex.A).Thing;
    private CompTradeShuttle? CompTradeShuttle => Shuttle.GetComp<CompTradeShuttle>();

    public override bool TryMakePreToilReservations(bool errorOnFailed) =>
        pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);

    protected override IEnumerable<Toil> MakeNewToils()
    {
        _ = this.FailOnDespawnedOrNull(TargetIndex.A);
        yield return Toils_Goto.Goto(TargetIndex.A, PathEndMode.Touch);

        var buyMoreTime = ToilMaker.MakeToil("MakeNewToils");
        buyMoreTime.initAction = () =>
        {
            var compTradeShuttle = CompTradeShuttle;
            var extendDepartureTimeAction = compTradeShuttle?.extendDepartureTimeAction;
            if (compTradeShuttle?.tradeAgreement == null || extendDepartureTimeAction == null)
            {
                RealisticOrbitalTradeMod.Instance.LogError(
                    "extendDepartureTimeAction is null in JobDriver_BuyMoreTime. This is a bug, can't buy more time."
                );
                return;
            }

            var shuttle = Shuttle;
            var map = pawn.Map;
            var cost = compTradeShuttle.NextBuyMoreTimeCost;
            var ticks = Settings._buyMoreTimeTicksPerPurchase;
            var tradeShip = compTradeShuttle.tradeAgreement.tradeShip;

            if (!TradeUtility.ColonyHasEnoughSilver(map, cost))
            {
                Messages.Message(
                    "MessageColonyCannotAfford".Translate(),
                    MessageTypeDefOf.RejectInput,
                    historical: false
                );
                return;
            }

            Find.WindowStack.Add(
                Dialog_MessageBox.CreateConfirmation(
                    "RealisticOrbitalTrade.ConfirmBuyMoreTime".Translate(
                        ticks.ToStringTicksToPeriod(),
                        ((float)cost).ToStringMoney()
                    ),
                    () =>
                    {
                        TradeUtility.LaunchThingsOfType(RwThingDefOf.Silver, cost, map, tradeShip);
                        extendDepartureTimeAction(ticks);
                        compTradeShuttle.timesBoughtMoreTime++;
                        Messages.Message(
                            "RealisticOrbitalTrade.BoughtMoreTime".Translate(
                                ticks.ToStringTicksToPeriod(),
                                ((float)cost).ToStringMoney()
                            ),
                            shuttle,
                            MessageTypeDefOf.PositiveEvent,
                            historical: false
                        );
                    }
                )
            );
        };
        yield return buyMoreTime;
    }
}
