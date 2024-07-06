using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using RealisticOrbitalTrade.Comps;
using Verse.AI;
using Verse.Sound;

namespace RealisticOrbitalTrade;

[HotSwappable]
internal class Dialog_RenegotiateTrade : Window
{
    private static readonly Vector2 AcceptButtonSize = new(160f, 40f);

    private static readonly Vector2 OtherBottomButtonSize = new(160f, 40f);

    private readonly TradeAgreement tradeAgreement;
    private List<Tradeable> allTradeables;

    public override Vector2 InitialSize => new(1024f, UI.screenHeight);

    private TransferableSorterDef sorter1 = TransferableSorterDefOf.Category;

    private TransferableSorterDef sorter2 = TransferableSorterDefOf.MarketValue;

    public Dialog_RenegotiateTrade(TradeAgreement tradeAgreement)
    {
        this.tradeAgreement = tradeAgreement;

        // XXX: Because some of the trade APIs rely on these being set to work. Required to be
        // before the call to tradeAgreement.AllTradeables
        TradeSession.trader = tradeAgreement.tradeShip;
        TradeSession.playerNegotiator = tradeAgreement.negotiator;

        allTradeables = tradeAgreement.AllTradeables;

        forcePause = true;
        absorbInputAroundWindow = true;

        commonSearchWidgetOffset.x += 18f;
        commonSearchWidgetOffset.y -= 18f;
    }

    private List<Tradeable>? cachedTradeables;
    private List<Tradeable> CachedTradeables
    {
        get
        {
            if (cachedTradeables == null)
            {
                CacheTradeables();
            }
            return cachedTradeables;
        }
    }

    private Tradeable? cachedCurrencyTradeable;
    private Tradeable CachedCurrencyTradeable
    {
        get
        {
            if (cachedCurrencyTradeable == null)
            {
                CacheTradeables();
            }
            return cachedCurrencyTradeable;
        }
    }

    public override void PreOpen()
    {
        base.PreOpen();
        quickSearchWidget.Reset();
    }

    public override void PostOpen()
    {
        base.PostOpen();
        CacheTradeables();

        if (!Settings._renegotiationWarningShown)
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("RealisticOrbitalTrade.RenegotiationWarning".Translate(), () =>
            {
                Settings._renegotiationWarningShown = true;
                RealisticOrbitalTradeMod.instance.WriteSettings();
            }));
        }
    }

    public override void PostClose()
    {
        base.PostClose();
        cachedTradeables = null;
        cachedCurrencyTradeable = null;
    }

    public override void DoWindowContents(Rect inRect)
    {
        UpdateCurrencyCount();
        Widgets.BeginGroup(inRect);
        inRect = inRect.AtZero();
        TransferableUIUtility.DoTransferableSorters(sorter1, sorter2, newSorter =>
        {
            sorter1 = newSorter;
            CacheTradeables();
        }, newSorter =>
        {
            sorter2 = newSorter;
            CacheTradeables();
        });
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
        Widgets.Label(
            new Rect(0f, 27f, inRect.width / 2f, inRect.height / 2f),
            "RealisticOrbitalTrade.NegotiatorTradeDialogInfo".Translate(tradeAgreement.negotiator.Name.ToStringFull,
            tradeAgreement.negotiator.GetStatValue(StatDefOf.TradePriceImprovement).ToStringPercent()));
        float num = inRect.width - 590f;
        Rect rect = new(num, 0f, inRect.width - num, 58f);
        Widgets.BeginGroup(rect);
        Text.Font = GameFont.Medium;
        Rect rect2 = new(0f, 0f, rect.width / 2f, rect.height);
        Text.Anchor = TextAnchor.UpperLeft;
        Widgets.Label(rect2, Faction.OfPlayer.Name.Truncate(rect2.width));
        Rect rect3 = new(rect.width / 2f, 0f, rect.width / 2f, rect.height);
        Text.Anchor = TextAnchor.UpperRight;
        string text = tradeAgreement.tradeShip.TraderName;
        if (Text.CalcSize(text).x > rect3.width)
        {
            Text.Font = GameFont.Small;
            text = text.Truncate(rect3.width);
        }
        Widgets.Label(rect3, text);
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperRight;
        Widgets.Label(new Rect(rect.width / 2f, 27f, rect.width / 2f, rect.height / 2f), tradeAgreement.tradeShip.TraderKind.LabelCap);
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = new Color(1f, 1f, 1f, 0.6f);
        Text.Font = GameFont.Tiny;
        Rect rect4 = new(rect.width / 2f - 100f - 30f, 0f, 200f, rect.height);
        Text.Anchor = TextAnchor.LowerCenter;
        Widgets.Label(rect4, "PositiveBuysNegativeSells".Translate());
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;
        Widgets.EndGroup();

        float num2 = 0f;
        float num3 = inRect.width - 16f;
        TradeUI.DrawTradeableRow(new Rect(0f, 58f, num3, 30f), CachedCurrencyTradeable, 1);
        GUI.color = Color.gray;
        Widgets.DrawLineHorizontal(0f, 87f, num3);
        GUI.color = Color.white;
        num2 = 30f;

        Rect mainRect = new(0f, 58f + num2, inRect.width, inRect.height - 58f - 38f - num2 - 20f);
        FillMainRect(mainRect);
        Text.Font = GameFont.Small;
        Rect rect5 = new(inRect.width / 2f - AcceptButtonSize.x / 2f, inRect.height - 55f, AcceptButtonSize.x, AcceptButtonSize.y);
        if (Widgets.ButtonText(rect5, "AcceptButton".Translate()))
        {
            if (CachedCurrencyTradeable.CountPostDealFor(Transactor.Trader) >= 0)
            {
                if (CachedCurrencyTradeable.CountPostDealFor(Transactor.Colony) >= 0)
                {
                    ResolveRenegotiation();
                }
                else
                {
                    FlashSilver();
                    SoundDefOf.ClickReject.PlayOneShotOnCamera();
                    Messages.Message("MessageColonyCannotAfford".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                }
            }
            else
            {
                FlashSilver();
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("ConfirmTraderShortFunds".Translate(), ResolveRenegotiation));
            }
            Event.current.Use();
        }
        if (Widgets.ButtonText(new Rect(rect5.x - 10f - OtherBottomButtonSize.x, rect5.y, OtherBottomButtonSize.x, OtherBottomButtonSize.y), "ResetButton".Translate()))
        {
            SoundDefOf.Tick_Low.PlayOneShotOnCamera();
            allTradeables = tradeAgreement.AllTradeables;
            CacheTradeables();
        }
        if (Widgets.ButtonText(new Rect(rect5.xMax + 10f, rect5.y, OtherBottomButtonSize.x, OtherBottomButtonSize.y), "CancelButton".Translate()))
        {
            Close();
            Event.current.Use();
        }
        Widgets.EndGroup();
    }



    private enum TransportShip
    {
        ToPlayer,
        ToTrader,
    }

    private void ResolveRenegotiation()
    {
        this.cachedCurrencyTradeable = null;
        this.cachedTradeables = null;

        var toTraderTransportShip = tradeAgreement.toTraderTransportShip!;
        var toTraderCompShuttle = toTraderTransportShip.ShuttleComp;
        var toTraderCompTransporter = toTraderTransportShip.TransporterComp;

        var tradeShipThings = Traverse.Create(tradeAgreement.tradeShip).Field<ThingOwner>("things").Value;
        var toPlayerTransportShipContainer = tradeAgreement.toPlayerTransportShip!.TransporterComp.innerContainer;
        var toTraderTransportShipContainer = toTraderCompTransporter.innerContainer;

        var toTraderShipThing = toTraderTransportShip.shipThing;
        var toTraderCompTradeShuttle = toTraderShipThing.TryGetComp<CompTradeShuttle>();

        var allOriginalTradeables = tradeAgreement.AllTradeables;
        var allNewTradeables = allTradeables.ToDictionary(k => k.ThingDef);
        var cachedCurrencyTradeable = CachedCurrencyTradeable;

        var sumTradedCount = CachedTradeables.Sum(t => Math.Abs(t.CountToTransfer));
        if (sumTradedCount == 0)
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("RealisticOrbitalTrade.DidYouMeanToCancel".Translate(), () =>
            {
                // Confirmed
                if (toTraderCompTradeShuttle.cancelTradeAction != null)
                {
                    toTraderCompTradeShuttle.cancelTradeAction.Invoke();
                    Close();
                }
                else
                {
                    RealisticOrbitalTradeMod.Error("Can't cancel trade; cancelTradeAction is null!");
                }
            }));
            return;
        }

        // Calculate all transfers
        List<(TransportShip transportShip, Tradeable tradeable, int amount)> changes = [];
        foreach (var originalTradeable in allOriginalTradeables)
        {
            var tradedThingDef = originalTradeable.ThingDef;
            var newTradeable = tradedThingDef != RimWorld.ThingDefOf.Silver ? allNewTradeables[tradedThingDef] : cachedCurrencyTradeable;

            if (originalTradeable.CountToTransfer == newTradeable.CountToTransfer)
            {
                // This trade was not modified
                continue;
            }

            // negative number: colony gets less
            // positive number: trader gets less
            var change = newTradeable.CountToTransfer - originalTradeable.CountToTransfer;
            var stillSameDirection = ((originalTradeable.CountToTransfer < 0) && (newTradeable.CountToTransfer < 0))
                || ((originalTradeable.CountToTransfer > 0) && (newTradeable.CountToTransfer > 0))
                || originalTradeable.CountToTransfer == 0
                || newTradeable.CountToTransfer == 0;
            RealisticOrbitalTradeMod.Dev(() => $"Tradeable {newTradeable.AnyThing.LabelNoCount} has change {change}(n:{newTradeable.CountToTransfer}/o:{originalTradeable.CountToTransfer}), same direction? {stillSameDirection}");
            if (stillSameDirection)
            {
                // The change is a mere reduction
                if (change > 0 && originalTradeable.CountToTransfer < 0)
                {
                    RealisticOrbitalTradeMod.Dev(() => $"(1) That means the trader gets {-change}");
                    changes.Add((TransportShip.ToTrader, newTradeable, -change));
                }
                else if (change < 0 && originalTradeable.CountToTransfer < 0)
                {
                    RealisticOrbitalTradeMod.Dev(() => $"(2) That means the trader gets {-change}");
                    changes.Add((TransportShip.ToTrader, newTradeable, -change));
                }
                else if (change > 0 && originalTradeable.CountToTransfer > 0)
                {
                    RealisticOrbitalTradeMod.Dev(() => $"(3) That means the player gets {change}");
                    changes.Add((TransportShip.ToPlayer, newTradeable, change));
                }
                else if (change < 0 && originalTradeable.CountToTransfer > 0)
                {
                    RealisticOrbitalTradeMod.Dev(() => $"(4) That means the player gets {change}");
                    changes.Add((TransportShip.ToPlayer, newTradeable, change));
                }
            }
            else
            {
                // The change "switched sides" and requires both reduction and increase
                if (change > 0)
                {
                    RealisticOrbitalTradeMod.Dev(() => $"That means the trader gets {originalTradeable.CountToTransfer}...");
                    changes.Add((TransportShip.ToTrader, newTradeable, originalTradeable.CountToTransfer));
                    RealisticOrbitalTradeMod.Dev(() => $"...and the player gets {newTradeable.CountToTransfer}");
                    changes.Add((TransportShip.ToPlayer, newTradeable, newTradeable.CountToTransfer));
                }
                else
                {
                    RealisticOrbitalTradeMod.Dev(() => $"That means the player gets {-originalTradeable.CountToTransfer}...");
                    changes.Add((TransportShip.ToPlayer, newTradeable, -originalTradeable.CountToTransfer));
                    RealisticOrbitalTradeMod.Dev(() => $"...and the trader gets {-newTradeable.CountToTransfer}");
                    changes.Add((TransportShip.ToTrader, newTradeable, -newTradeable.CountToTransfer));
                }
            }
        }
        if (changes.Count == 0)
        {
            RealisticOrbitalTradeMod.Dev(() => "No actual renegotiation took place.");
            Close();
            return;
        }
        foreach (var (transportShip, tradeable, amount) in changes)
        {
            RealisticOrbitalTradeMod.Dev(() => $"change for {transportShip}: {amount} {tradeable.AnyThing.LabelCapNoCount}");

            var tradedThingDef = tradeable.ThingDef;
            switch (transportShip)
            {
                case TransportShip.ToPlayer:
                    if (amount > 0)
                    {
                        if (tradedThingDef != RimWorld.ThingDefOf.Silver)
                        {
                            RealisticOrbitalTradeMod.Error($"Only expecting to owe the player more silver after renegotiation; but owing {tradedThingDef}?!");
                            continue;
                        }

                        // Move things out of the trade ship and to the transport ship meant for the player
                        TransferableUtility.TransferNoSplit(cachedCurrencyTradeable.thingsTrader, amount, (Thing toGive, int countToGive) =>
                        {
                            toPlayerTransportShipContainer.TryAddOrTransfer(toGive, countToGive);
                        });
                    }
                    else
                    {
                        // Move things out of the transport ship meant for the player and back to the trade ship
                        var things = tradeable.thingsTrader;
                        var leftToTransfer = -amount;
                        foreach (var thing in things.Where(t => t.holdingOwner == toPlayerTransportShipContainer))
                        {
                            var originalStackCount = thing.stackCount;
                            RealisticOrbitalTradeMod.Dev(() => $"-- Stack count: {originalStackCount}; asking to transfer {leftToTransfer}");
                            tradeShipThings.TryAddOrTransfer(thing, leftToTransfer);
                            leftToTransfer -= originalStackCount;
                            if (leftToTransfer == 0)
                            {
                                break;
                            }
                        }
                    }
                    break;

                case TransportShip.ToTrader:
                    if (amount > 0)
                    {
                        if (tradedThingDef != RimWorld.ThingDefOf.Silver)
                        {
                            RealisticOrbitalTradeMod.Error($"Only expecting to owe the trader more silver after renegotiation; but owing {tradedThingDef}?!");
                            continue;
                        }

                        TransferableUtility.TransferNoSplit(cachedCurrencyTradeable.thingsColony, amount, (Thing toGive, int countToGive) =>
                        {
                            ThingCountClass thingCount = new(toGive, countToGive);
                            tradeAgreement.thingsSoldToTrader.Add(thingCount);
                            Utils.AddThingToLoadToShuttle(thingCount, toTraderCompTradeShuttle, toTraderCompShuttle);
                        });
                    }
                    else
                    {
                        // This is the hard one. Things can be in three different states
                        // - Required to be loaded, but not loaded or being hauled yet
                        // - On the way to being loaded (i.e. being hauled)
                        // - Already loaded

                        if (tradeable.AnyThing is Pawn pawn)
                        {
                            // Remove from sold list
                            tradeAgreement.pawnsSoldToTrader.Remove(pawn);
                            // Remove from shuttle's required cargo list
                            toTraderCompShuttle.requiredPawns.Remove(pawn);

                            // If already loaded onto the shuttle, make the shuttle drop
                            // the pawn back out.
                            if (toTraderTransportShipContainer.Contains(pawn))
                            {
                                IntVec3 dropLoc = toTraderTransportShip.shipThing.Position + ShipJob_Unload.DropoffSpotOffset;
                                var unloaded = toTraderTransportShipContainer.TryDrop(pawn, dropLoc, toTraderShipThing.Map, ThingPlaceMode.Near, out var _, null, c =>
                                {
                                    if (c.Fogged(toTraderShipThing.Map))
                                    {
                                        return false;
                                    }
                                    return !pawn.Downed;
                                }, false);
                                if (!unloaded)
                                {
                                    RealisticOrbitalTradeMod.Error($"Could not unload {pawn} from transport ship after renegotiation!");
                                }
                            }

                            // Also cancel any hauling jobs featuring the pawn
                            var p = PawnsWithHaulToTransporterJob(toTraderCompTransporter, toTraderShipThing.Map)
                                .SingleOrDefault(jp => jp.carryTracker.CarriedThing == pawn || jp.CurJob.targetA.Thing == pawn);
                            p?.jobs.EndCurrentJob(JobCondition.Incompletable, false, true);
                        }
                        else
                        {
                            // Take things off the soldToTrader list
                            var leftToTakeOff = -amount;
                            do
                            {
                                ThingCountClass thingCount;
                                if (tradeable.AnyThing.HasRequirements())
                                {
                                    thingCount = tradeAgreement.thingsSoldToTrader.FirstOrDefault(t => t.thing.GetInnerIfMinified() == tradeable.AnyThing);
                                }
                                else
                                {
                                    thingCount = tradeAgreement.thingsSoldToTrader.FirstOrDefault(t => t.thing.def == tradedThingDef);
                                }
                                if (thingCount == null)
                                {
                                    break;
                                }
                                if (thingCount.Count > leftToTakeOff)
                                {
                                    thingCount.Count -= leftToTakeOff;
                                    leftToTakeOff = 0;
                                }
                                else
                                {
                                    leftToTakeOff -= thingCount.Count;
                                    tradeAgreement.thingsSoldToTrader.Remove(thingCount);
                                }
                            } while (leftToTakeOff > 0);

                            // Take things off the to-be-loaded list
                            leftToTakeOff = -amount;
                            do
                            {
                                // First, remove things from the to-be-loaded list. If we can get away 
                                // with just reducing that, we won't need to unload anything.
                                if (tradeable.AnyThing.HasRequirements())
                                {
                                    var thingDefCountMaybeNull = toTraderCompTradeShuttle.requiredSpecificItems.Cast<ThingDefCountWithRequirements?>().FirstOrDefault(t => tradeable.ThingDef == t!.Value.def);
                                    if (!thingDefCountMaybeNull.HasValue)
                                    {
                                        RealisticOrbitalTradeMod.Error($"Needed to take {-amount} off the 'requiredItems' list, but could only remove {-amount - leftToTakeOff}");
                                        break;
                                    }
                                    var thingDefCount = thingDefCountMaybeNull.Value;
                                    if (thingDefCount.count > leftToTakeOff)
                                    {
                                        thingDefCount.count -= leftToTakeOff;
                                        leftToTakeOff = 0;
                                    }
                                    else
                                    {
                                        leftToTakeOff -= thingDefCount.count;
                                        toTraderCompTradeShuttle.requiredSpecificItems.Remove(thingDefCount);
                                    }
                                }
                                else
                                {
                                    var thingDefCount = toTraderCompShuttle.requiredItems.FirstOrDefault(t => tradeable.ThingDef == t.ThingDef);
                                    if (thingDefCount == null)
                                    {
                                        RealisticOrbitalTradeMod.Error($"Needed to take {-amount} off the 'requiredItems' list, but could only remove {-amount - leftToTakeOff}");
                                        break;
                                    }
                                    var thingDefCountIndex = toTraderCompShuttle.requiredItems.IndexOf(thingDefCount);
                                    if (thingDefCount.Count > leftToTakeOff)
                                    {
                                        toTraderCompShuttle.requiredItems[thingDefCountIndex] = thingDefCount.WithCount(thingDefCount.Count - leftToTakeOff);
                                        leftToTakeOff = 0;
                                    }
                                    else
                                    {
                                        leftToTakeOff -= thingDefCount.Count;
                                        toTraderCompShuttle.requiredItems.Remove(thingDefCount);
                                    }
                                }
                            } while (leftToTakeOff > 0);

                            // Calculate how much has been loaded already
                            List<Thing> actuallyLoaded;
                            int countActuallyLoaded;
                            if (tradeable.AnyThing.HasRequirements())
                            {
                                actuallyLoaded = toTraderTransportShipContainer.Where(t => t.GetInnerIfMinified() == tradeable.AnyThing).ToList();
                                countActuallyLoaded = actuallyLoaded.Sum(t => t.stackCount);
                            }
                            else
                            {
                                actuallyLoaded = toTraderTransportShipContainer.Where(t => t.GetInnerIfMinified().def == tradeable.ThingDef).ToList();
                                countActuallyLoaded = actuallyLoaded.Sum(t => t.stackCount);
                            }

                            var countRequiredLoaded = tradeable.CountToTransferToDestination;
                            if (countActuallyLoaded > countRequiredLoaded)
                            {
                                // More items loaded than we've sold now; unload some
                                var leftToUnload = countActuallyLoaded - countRequiredLoaded;

                                IntVec3 dropLoc = toTraderTransportShip.shipThing.Position + ShipJob_Unload.DropoffSpotOffset;
                                foreach (var loaded in actuallyLoaded)
                                {
                                    var unloadAmount = Math.Min(leftToUnload, loaded.stackCount);
                                    var unloaded = toTraderTransportShipContainer.TryDrop(loaded, dropLoc, toTraderShipThing.Map, ThingPlaceMode.Near, unloadAmount, out var _, null, c =>
                                    {
                                        if (c.Fogged(toTraderShipThing.Map))
                                        {
                                            return false;
                                        }
                                        return c.GetFirstPawn(toTraderShipThing.Map) == null;
                                    });
                                    if (!unloaded)
                                    {
                                        RealisticOrbitalTradeMod.Error($"Could not (fully) unload {tradedThingDef.label} from transport ship after renegotiation!");
                                    }
                                    leftToUnload -= unloadAmount;
                                    if (leftToUnload == 0)
                                    {
                                        break;
                                    }
                                }
                            }

                            // Also cancel any hauling jobs featuring the item
                            // TODO: Calculate how much is being hauled and just cancel jobs that
                            // would put it over the top
                            if (tradeable.AnyThing.HasRequirements())
                            {
                                foreach (var p in PawnsWithHaulToTransporterJob(toTraderCompTransporter, toTraderShipThing.Map)
                                    .Where(jp => jp.carryTracker.CarriedThing == tradeable.AnyThing || jp.CurJob.targetA.Thing.GetInnerIfMinified() == tradeable.AnyThing))
                                {
                                    RealisticOrbitalTradeMod.Dev(() => $"Cancelling hauling job for {p} for {tradeable.AnyThing.LabelNoCount}");
                                    p.jobs.EndCurrentJob(JobCondition.Incompletable, false, true);
                                }
                            }
                            else
                            {
                                foreach (var p in PawnsWithHaulToTransporterJob(toTraderCompTransporter, toTraderShipThing.Map)
                                    .Where(jp => jp.carryTracker.CarriedThing?.def == tradeable.ThingDef || jp.CurJob.targetA.Thing.def == tradeable.ThingDef))
                                {
                                    RealisticOrbitalTradeMod.Dev(() => $"Cancelling hauling job for {p} for {tradeable.AnyThing.LabelNoCount}");
                                    p.jobs.EndCurrentJob(JobCondition.Incompletable, false, true);
                                }
                            }
                        }
                    }
                    break;
            }
        }

        // Immediately re-evaluate the auto-load list on the shuttle
        Traverse.Create(toTraderCompShuttle).Method("CheckAutoload").GetValue();

        SoundDefOf.ExecuteTrade.PlayOneShotOnCamera();
        Close(doCloseSound: false);
    }

    private void FillMainRect(Rect mainRect)
    {
        Text.Font = GameFont.Small;
        float height = 6f + CachedTradeables.Count * 30f;
        Rect viewRect = new(0f, 0f, mainRect.width - 16f, height);
        Widgets.BeginScrollView(mainRect, ref scrollPosition, viewRect);
        float num = 6f;
        float num2 = scrollPosition.y - 30f;
        float num3 = scrollPosition.y + mainRect.height;
        int num4 = 0;
        for (int i = 0; i < CachedTradeables.Count; i++)
        {
            if (num > num2 && num < num3)
            {
                Rect rect = new(0f, num, viewRect.width, 30f);
                TradeUI.DrawTradeableRow(rect, CachedTradeables[i], num4);
            }
            num += 30f;
            num4++;
        }
        Widgets.EndScrollView();
    }

    private static IEnumerable<Pawn> PawnsWithHaulToTransporterJob(CompTransporter compTransporter, Map map)
    {
        IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
        foreach (Pawn pawn in pawns)
        {
            if (pawn.CurJobDef == RimWorld.JobDefOf.HaulToTransporter && pawn.jobs.curDriver is JobDriver_HaulToTransporter jobDriver && compTransporter == jobDriver.Transporter) // && pawn.carryTracker.CarriedThing != null)
            {
                yield return pawn;
            }
        }
    }

    public static void FlashSilver()
    {
        // XXX: Sigh, this just feels so wrong. But it's how it's been coded in RimWorld.
        Dialog_Trade.lastCurrencyFlashTime = Time.time;
    }

    private Vector2 scrollPosition = Vector2.zero;

    private readonly QuickSearchWidget quickSearchWidget = new();
    public override QuickSearchWidget CommonSearchWidget => quickSearchWidget;
    public override void Notify_CommonSearchChanged()
    {
        CacheTradeables();
    }

    private void UpdateCurrencyCount()
    {
        float num = 0f;
        for (int i = 0; i < allTradeables.Count; i++)
        {
            Tradeable tradeable = allTradeables[i];
            if (!tradeable.IsCurrency)
            {
                num += tradeable.CurTotalCurrencyCostForSource;
            }
        }
        CachedCurrencyTradeable.ForceToSource(-CachedCurrencyTradeable.CostToInt(num));
    }

    [MemberNotNull(nameof(cachedTradeables), nameof(cachedCurrencyTradeable))]
    private void CacheTradeables()
    {
        var toPlayerTransportShipContainer = tradeAgreement.toPlayerTransportShip!.TransporterComp.innerContainer;
        var toTraderTransportShipContainer = tradeAgreement.toTraderTransportShip!.TransporterComp.innerContainer;

        cachedCurrencyTradeable = allTradeables.FirstOrDefault((Tradeable x) => x.IsCurrency);
        if (cachedCurrencyTradeable == null)
        {
            // If a trade is 100% even (i.e. no exchange of silver) we need to add it ourselves.
            cachedCurrencyTradeable = new Tradeable();
            allTradeables.Add(cachedCurrencyTradeable);
        }
        else
        {
            cachedCurrencyTradeable.thingsColony.Clear();
            cachedCurrencyTradeable.thingsTrader.Clear();
        }
        foreach (var item in tradeAgreement.ColonyThingsTraderWillingToBuy().Concat(toTraderTransportShipContainer)
            .Where(t => t.def == RimWorld.ThingDefOf.Silver))
        {
            cachedCurrencyTradeable.AddThing(item, Transactor.Colony);
        }
        foreach (var item in tradeAgreement.tradeShip.Goods.Concat(toPlayerTransportShipContainer)
            .Where(t => t.def == RimWorld.ThingDefOf.Silver))
        {
            cachedCurrencyTradeable.AddThing(item, Transactor.Trader);
        }

        cachedTradeables = (from tr in allTradeables
                            where !tr.IsCurrency && (tr.TraderWillTrade || !TradeSession.trader.TraderKind.hideThingsNotWillingToTrade)
                            where quickSearchWidget.filter.Matches(tr.Label)
                            orderby (!tr.TraderWillTrade) ? (-1) : 0 descending
                            select tr).ThenBy((Tradeable tr) => tr, sorter1.Comparer).ThenBy((Tradeable tr) => tr, sorter2.Comparer).ThenBy(TransferableUIUtility.DefaultListOrderPriority)
            .ThenBy((Tradeable tr) => tr.ThingDef.label)
            .ThenBy((Tradeable tr) => tr.AnyThing.TryGetQuality(out var qc) ? ((int)qc) : (-1))
            .ThenBy((Tradeable tr) => tr.AnyThing.HitPoints)
            .ToList();
        quickSearchWidget.noResultsMatched = !cachedTradeables.Any();
    }
}
