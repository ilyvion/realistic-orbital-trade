namespace RealisticOrbitalTrade.Core;

internal abstract class TradeDeliveryMethod : IExposable
{
    public abstract ThingOwner Things { get; }

    public abstract void ExposeData();
}

/// <summary>
/// A single delivery shuttle holding all the bought goods, as trades in saves from earlier
/// versions of the mod have.
/// </summary>
internal class TradeDeliveryMethod_TransportShip(TransportShip toPlayerTransportShip)
    : TradeDeliveryMethod
{
    [Obsolete("for scribing only", true)]
    public TradeDeliveryMethod_TransportShip()
        : this(null!) { }

    private ThingOwner<Thing>? departedShipThings;

    // Saving nulls a departed shuttle's shipThing, leaving nothing to deliver.
    public override ThingOwner Things =>
        toPlayerTransportShip.TransporterComp?.innerContainer ?? (departedShipThings ??= new());

    public override void ExposeData() =>
        Scribe_References.Look(ref toPlayerTransportShip, "toPlayerTransportShip");
}

/// <summary>
/// Bought goods waiting in the trade agreement for the next of the shuttles that deliver them one
/// after another (see <see cref="Quests.QuestPart_DeliverBoughtItemsByShuttles"/>).
/// </summary>
internal class TradeDeliveryMethod_TransportShips(TradeAgreement tradeAgreement)
    : TradeDeliveryMethod
{
    [Obsolete("for scribing only", true)]
    public TradeDeliveryMethod_TransportShips()
        : this(null!) { }

    private TradeAgreement tradeAgreement = tradeAgreement;

    public override ThingOwner Things => tradeAgreement.thingsSoldToPlayer;

    public override void ExposeData() =>
        Scribe_References.Look(ref tradeAgreement, "tradeAgreement");
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
