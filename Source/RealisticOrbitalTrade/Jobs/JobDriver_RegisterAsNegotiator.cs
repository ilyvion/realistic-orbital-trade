using RealisticOrbitalTrade.Comps;
using Verse.AI;

namespace RealisticOrbitalTrade;

[HotSwappable]
internal class JobDriver_RegisterAsNegotiator : JobDriver
{
    private ThingWithComps Shuttle => (ThingWithComps)job.GetTarget(TargetIndex.A).Thing;
    private TradeAgreement? TradeAgreement => Shuttle.GetComp<CompTradeShuttle>()?.tradeAgreement;

    public override bool TryMakePreToilReservations(bool errorOnFailed) =>
        pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);

    protected override IEnumerable<Toil> MakeNewToils()
    {
        _ = this.FailOnDespawnedOrNull(TargetIndex.A);
        yield return Toils_Goto.Goto(TargetIndex.A, PathEndMode.Touch);

        var registerAsNegotiator = ToilMaker.MakeToil("MakeNewToils");
        registerAsNegotiator.initAction = () =>
        {
            var tradeAgreement = TradeAgreement;
            if (tradeAgreement == null)
            {
                RealisticOrbitalTradeMod.Instance.LogError(
                    "TradeAgreement is null in JobDriver_RegisterAsNegotiator. This is a bug, can't register a new negotiator."
                );
                return;
            }
            tradeAgreement.negotiator = pawn;
            tradeAgreement.NegotiatorUnavailableLetterSent = false;
            Messages.Message(
                "RealisticOrbitalTrade.RegisteredAsNegotiator".Translate(
                    pawn.LabelShort,
                    tradeAgreement.tradeShip.TraderName
                ),
                pawn,
                MessageTypeDefOf.PositiveEvent,
                historical: false
            );
        };
        yield return registerAsNegotiator;
    }
}
