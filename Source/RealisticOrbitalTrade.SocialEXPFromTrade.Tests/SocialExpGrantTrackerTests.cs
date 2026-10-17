using DevTools.Testing;
using RealisticOrbitalTrade.SocialEXPFromTrade.Patch;

namespace RealisticOrbitalTrade.SocialEXPFromTrade.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class SocialExpGrantTrackerTests
{
    private static List<PendingSocialExpGain> Grants = [];

    private static TradeAgreement NewAgreement() => new() { negotiator = new Pawn() };

    private static void Begin()
    {
        SocialExpGrantTracker.Reset();
        Grants = [];
        SocialExpGrantTracker.GrantOverride = (_, pending) => Grants.Add(pending);
    }

    private static void End()
    {
        SocialExpGrantTracker.GrantOverride = null;
        SocialExpGrantTracker.Reset();
    }

    [Test]
    public static void DeferTwiceAccumulatesIntoOneEntry()
    {
        Begin();
        try
        {
            var agreement = NewAgreement();
            SocialExpGrantTracker.Defer(agreement, 1f, 2f, false);
            SocialExpGrantTracker.Defer(agreement, 3f, 4f, true);

            Expect.AreEqual(1, SocialExpGrantTracker.PendingCount);
            Expect.IsTrue(SocialExpGrantTracker.TryGetPending(agreement, out var pending));
            Expect.AreEqual(4f, pending.BuyValue);
            Expect.AreEqual(6f, pending.SellValue);
            Expect.IsTrue(pending.Inspired);
        }
        finally
        {
            End();
        }
    }

    [Test]
    public static void UnsuccessfulResolveDiscardsWithoutGranting()
    {
        Begin();
        try
        {
            var agreement = NewAgreement();
            SocialExpGrantTracker.Defer(agreement, 1f, 2f, false);

            SocialExpGrantTracker.Resolve(agreement, false);

            Expect.AreEqual(0, SocialExpGrantTracker.PendingCount);
            Expect.AreEqual(0, Grants.Count);
        }
        finally
        {
            End();
        }
    }

    [Test]
    public static void ResolveOfUnknownAgreementDoesNothing()
    {
        Begin();
        try
        {
            SocialExpGrantTracker.Resolve(NewAgreement(), true);

            Expect.AreEqual(0, Grants.Count);
        }
        finally
        {
            End();
        }
    }

    [Test]
    public static void SecondSuccessfulResolveDoesNotGrantTwice()
    {
        Begin();
        try
        {
            var agreement = NewAgreement();
            SocialExpGrantTracker.Defer(agreement, 1f, 2f, false);

            SocialExpGrantTracker.Resolve(agreement, true);
            SocialExpGrantTracker.Resolve(agreement, true);

            Expect.AreEqual(1, Grants.Count);
            Expect.AreEqual(1f, Grants[0].BuyValue);
            Expect.AreEqual(2f, Grants[0].SellValue);
        }
        finally
        {
            End();
        }
    }

    [Test]
    public static void ResetEmptiesPending()
    {
        Begin();
        try
        {
            SocialExpGrantTracker.Defer(NewAgreement(), 1f, 2f, false);
            SocialExpGrantTracker.Defer(NewAgreement(), 1f, 2f, false);

            SocialExpGrantTracker.Reset();

            Expect.AreEqual(0, SocialExpGrantTracker.PendingCount);
        }
        finally
        {
            End();
        }
    }

    [Test]
    public static void ShouldDeferTruthTable()
    {
        var agreement = NewAgreement();

        Expect.IsTrue(SocialEXPFromTrade_Patch1_Postfix.ShouldDefer(agreement, true, false));
        Expect.IsFalse(SocialEXPFromTrade_Patch1_Postfix.ShouldDefer(null, true, false));
        Expect.IsFalse(SocialEXPFromTrade_Patch1_Postfix.ShouldDefer(agreement, false, false));
        Expect.IsFalse(SocialEXPFromTrade_Patch1_Postfix.ShouldDefer(agreement, true, true));
    }

    [Test]
    public static void MissingStateSkipsTheirPostfixWithoutDeferring()
    {
        Begin();
        try
        {
            Rimworld_TradeDeal_TryExecute.PendingTradeAgreement = NewAgreement();
            var result = true;

            Expect.IsFalse(SocialEXPFromTrade_Patch1_Postfix.Prefix(null, ref result));
            Expect.AreEqual(0, SocialExpGrantTracker.PendingCount);
            Expect.IsNull(Rimworld_TradeDeal_TryExecute.PendingTradeAgreement);
        }
        finally
        {
            Rimworld_TradeDeal_TryExecute.PendingTradeAgreement = null;
            End();
        }
    }
}
