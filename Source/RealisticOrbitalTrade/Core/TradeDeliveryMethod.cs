namespace RealisticOrbitalTrade.Core;

internal abstract class TradeDeliveryMethod : IExposable
{
    public abstract ThingOwner Things { get; }

    public abstract void ExposeData();
}

internal class TradeDeliveryMethod_TransportShip(TransportShip toPlayerTransportShip)
    : TradeDeliveryMethod
{
    [Obsolete("for scribing only", true)]
    public TradeDeliveryMethod_TransportShip()
        : this(null!) { }

    public override ThingOwner Things => toPlayerTransportShip.TransporterComp.innerContainer;

    public override void ExposeData() =>
        Scribe_References.Look(ref toPlayerTransportShip, "toPlayerTransportShip");
}

internal class TradeDeliveryMethod_DropPod(TradeAgreement tradeAgreement) : TradeDeliveryMethod
{
    [Obsolete("for scribing only", true)]
    public TradeDeliveryMethod_DropPod()
        : this(null!) { }

    private TradeAgreement tradeAgreement = tradeAgreement;

    public override ThingOwner Things => tradeAgreement.thingsSoldToPlayer;

    public override void ExposeData() =>
        Scribe_References.Look(ref tradeAgreement, "tradeAgreement");
}
