using ExpSettings = SocialEXPFromTrade.Settings;

namespace RealisticOrbitalTrade.SocialEXPFromTrade;

/// <summary>
/// Holds experience Social Experience from Trade would otherwise have granted immediately, one
/// entry per <see cref="TradeAgreement"/> still awaiting delivery - see
/// <see cref="Patch.SocialEXPFromTrade_Patch1_Postfix"/> for where entries are added and
/// <see cref="Resolve"/> for where they're resolved. A trade's delivery can easily still be in
/// flight across a save/reload, so entries are persisted via
/// <see cref="RealisticOrbitalTradeGameComponent.ExposeDataExtra"/> (see
/// <see cref="SocialEXPFromTradeInteropMod"/> for the subscription).
/// </summary>
internal static class SocialExpGrantTracker
{
    private static readonly Dictionary<TradeAgreement, PendingSocialExpGain> _pending = [];

    /// <summary>
    /// Subscribed to <see cref="RealisticOrbitalTradeGameComponent.Created"/> - clears any
    /// state left over from a previous game session, since a new game triggers no
    /// <see cref="ExposeData"/> pass to do that itself.
    /// </summary>
    internal static void Reset() => _pending.Clear();

    internal static void Defer(
        TradeAgreement tradeAgreement,
        float buyValue,
        float sellValue,
        bool inspired
    ) =>
        _pending[tradeAgreement] = _pending.TryGetValue(tradeAgreement, out var existing)
            ? existing.Accumulate(buyValue, sellValue, inspired)
            : new PendingSocialExpGain(buyValue, sellValue, inspired);

    /// <summary>
    /// Grants (if <paramref name="successful"/>) or discards the experience deferred for
    /// <paramref name="tradeAgreement"/>. Safe to call for an agreement with nothing deferred
    /// (e.g. an ordinary trade Social Experience from Trade already granted immediately).
    /// </summary>
    internal static void Resolve(TradeAgreement tradeAgreement, bool successful)
    {
        if (!_pending.Remove(tradeAgreement, out var pending) || !successful)
        {
            return;
        }

        Grant(tradeAgreement.negotiator, pending);
    }

    private static void Grant(Pawn negotiator, PendingSocialExpGain pending)
    {
        var experience = pending.ComputeExperience(
            ExpSettings.BuyingMultiplier,
            ExpSettings.SellingMultiplier,
            ExpSettings.InspiredMultiplier
        );

        if (PawnUtility.ShouldSendNotificationAbout(negotiator) && ExpSettings.NotifyPlayer)
        {
            var displayedExperience =
                experience * negotiator.skills.GetSkill(SkillDefOf.Social).LearnRateFactor(false);
            Messages.Message(
                "SocialEXPFromTrade.Notification".Translate(
                    negotiator.LabelShort,
                    Math.Round(displayedExperience, 0)
                ),
                negotiator,
                MessageTypeDefOf.PositiveEvent
            );
        }

#if v1_5_OR_GREATER
        negotiator.skills.Learn(SkillDefOf.Social, experience, false, false);
#else
        negotiator.skills.Learn(SkillDefOf.Social, experience, false);
#endif
    }

    // Scribe can't deep/value-save a Dictionary<TradeAgreement, PendingSocialExpGain> directly -
    // TradeAgreement needs LookMode.Reference (it's ILoadReferenceable) and the struct value has
    // no Scribe-recognized representation of its own - so it's flattened to parallel lists here,
    // same as TradeAgreement's own tmpSavedPawns pattern.
    private static List<TradeAgreement> _tmpAgreements = [];
    private static List<float> _tmpBuyValues = [];
    private static List<float> _tmpSellValues = [];
    private static List<bool> _tmpInspired = [];

    /// <summary>
    /// Subscribed to <see cref="RealisticOrbitalTradeGameComponent.ExposeDataExtra"/> - Scribes
    /// <see cref="_pending"/> nested under <see cref="RealisticOrbitalTradeGameComponent"/>'s own
    /// save node.
    /// </summary>
    internal static void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            _tmpAgreements = [.. _pending.Keys];
            _tmpBuyValues = [.. _pending.Values.Select(p => p.BuyValue)];
            _tmpSellValues = [.. _pending.Values.Select(p => p.SellValue)];
            _tmpInspired = [.. _pending.Values.Select(p => p.Inspired)];
        }

        Scribe_Collections.Look(
            ref _tmpAgreements,
            "socialExpPendingAgreements",
            LookMode.Reference
        );
        Scribe_Collections.Look(ref _tmpBuyValues, "socialExpPendingBuyValues", LookMode.Value);
        Scribe_Collections.Look(ref _tmpSellValues, "socialExpPendingSellValues", LookMode.Value);
        Scribe_Collections.Look(ref _tmpInspired, "socialExpPendingInspired", LookMode.Value);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            // A save from before this integration persisted anything here has no
            // "socialExpPendingAgreements" node etc. to read, which makes Scribe_Collections.Look
            // set these to null rather than leave them at their pre-existing empty-list default.
            _tmpAgreements ??= [];
            _tmpBuyValues ??= [];
            _tmpSellValues ??= [];
            _tmpInspired ??= [];

            _pending.Clear();
            for (var i = 0; i < _tmpAgreements.Count; i++)
            {
                if (_tmpAgreements[i] == null)
                {
                    // A trade agreement destroyed/unresolvable on load (see
                    // TradeAgreement.IsSoldThingLost-style reference loss) can never resolve
                    // through RealisticOrbitalTradeGameComponent.TradeAgreementEnded either -
                    // there's nothing left to grant the deferred experience to.
                    continue;
                }
                _pending[_tmpAgreements[i]] = new PendingSocialExpGain(
                    _tmpBuyValues[i],
                    _tmpSellValues[i],
                    _tmpInspired[i]
                );
            }
            _tmpAgreements.Clear();
            _tmpBuyValues.Clear();
            _tmpSellValues.Clear();
            _tmpInspired.Clear();
        }
    }
}
