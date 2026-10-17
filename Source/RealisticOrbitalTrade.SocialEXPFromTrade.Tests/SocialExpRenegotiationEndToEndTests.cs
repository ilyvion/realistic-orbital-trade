using DevTools.Testing;
using RealisticOrbitalTrade.Tests;

namespace RealisticOrbitalTrade.SocialEXPFromTrade.Tests;

// Renegotiating a sale on a landed shuttle changes the experience held back for it to match the
// renegotiated deal, and that is what's granted once the trade completes.
[TestFixture(TestType.Playing)]
internal sealed class SocialExpRenegotiationEndToEndTests
{
    private const int SteelOnMap = 400;

    [Test]
    public static IEnumerator ReducingASaleHoldsBackLessExperience()
    {
        yield return RenegotiateAndExpectHeldBackToFollow(300, 100);
    }

    [Test]
    public static IEnumerator IncreasingASaleHoldsBackMoreExperience()
    {
        yield return RenegotiateAndExpectHeldBackToFollow(100, 300);
    }

    private static IEnumerator RenegotiateAndExpectHeldBackToFollow(int soldCount, int newCount)
    {
        var steel = RimWorld.ThingDefOf.Steel;
        List<PendingSocialExpGain> grants = [];
        SocialExpGrantTracker.Reset();
        SocialExpGrantTracker.GrantOverride = (_, pending) => grants.Add(pending);
        try
        {
            using var scene = new TradeScene(Find.CurrentMap);
            scene.PlaceOnMap(steel, SteelOnMap);

            yield return scene.SellAndWaitForLanding(steel, soldCount);
            Expect.IsTrue(
                SocialExpGrantTracker.TryGetPending(scene.TradeAgreement!, out var sold),
                "Nothing was held back for the sale"
            );
            Expect.GreaterThan(sold.SellValue, 0f, "The sale held back no selling value");

            scene.RenegotiateSale(newCount);
            Expect.IsTrue(
                SocialExpGrantTracker.TryGetPending(scene.TradeAgreement!, out var renegotiated),
                "Renegotiating dropped what was held back"
            );
            Expect.AreApproximatelyEqual(
                (float)newCount / soldCount,
                renegotiated.SellValue / sold.SellValue,
                "The held-back selling value doesn't match the renegotiated amount"
            );
            Expect.AreEqual(sold.Inspired, renegotiated.Inspired);

            yield return scene.WaitForQuestToEnd();
            scene.ExpectSaleCompleted();
            Expect.AreEqual(1, grants.Count, "The completed trade didn't grant once");
            if (grants.Count == 1)
            {
                Expect.AreEqual(renegotiated, grants[0]);
            }
        }
        finally
        {
            SocialExpGrantTracker.GrantOverride = null;
            SocialExpGrantTracker.Reset();
        }
    }
}
