using RealisticOrbitalTrade.Comps;
using Verse.AI;

namespace RealisticOrbitalTrade;

[HotSwappable]
public class JobDriver_RenegotiateTrade : JobDriver
{
    private ThingWithComps Shuttle => (ThingWithComps)job.GetTarget(TargetIndex.A).Thing;
    private TradeAgreement? TradeAgreement => Shuttle.GetComp<CompTradeShuttle>().tradeAgreement;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedOrNull(TargetIndex.A);
        yield return Toils_Goto.Goto(TargetIndex.A, PathEndMode.Touch);

        Toil renegotiateTrade = ToilMaker.MakeToil("MakeNewToils");
        renegotiateTrade.initAction = () =>
        {
            var tradeAgreement = TradeAgreement;
            if (tradeAgreement == null)
            {
                RealisticOrbitalTradeMod.Error("TradeAgreement is null in JobDriver_RenegotiateTrade. This is a bug, can't open Dialog_RenegotiateTrade.");
                return;
            }
            Dialog_RenegotiateTrade tradeDialog = new(tradeAgreement);
            Find.WindowStack.Add(tradeDialog);
        };
        yield return renegotiateTrade;
    }
}
