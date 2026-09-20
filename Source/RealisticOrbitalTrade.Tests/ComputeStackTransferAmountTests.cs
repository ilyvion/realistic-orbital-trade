using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class ComputeStackTransferAmountTests
{
    // Regression guard: the silver-return loop used to subtract the full stack count instead of
    // the amount actually transferred and only broke on an exact zero, so a stack larger than the
    // remaining amount owed drove leftToTransfer negative and caused later stacks to be
    // transferred with a negative count.
    [Test]
    public static void StackLargerThanRemainingOnlyTakesWhatIsOwed()
    {
        var (countToTransfer, remainingToTransfer, done) =
            TradeAgreement.ComputeStackTransferAmount(leftToTransfer: 5, stackCount: 20);

        Expect.AreEqual(5, countToTransfer);
        Expect.AreEqual(0, remainingToTransfer);
        Expect.IsTrue(done);
    }

    [Test]
    public static void StackSmallerThanRemainingTakesWholeStackAndContinues()
    {
        var (countToTransfer, remainingToTransfer, done) =
            TradeAgreement.ComputeStackTransferAmount(leftToTransfer: 20, stackCount: 5);

        Expect.AreEqual(5, countToTransfer);
        Expect.AreEqual(15, remainingToTransfer);
        Expect.IsFalse(done);
    }

    [Test]
    public static void ExactlyMatchingStackFinishesCleanly()
    {
        var (countToTransfer, remainingToTransfer, done) =
            TradeAgreement.ComputeStackTransferAmount(leftToTransfer: 5, stackCount: 5);

        Expect.AreEqual(5, countToTransfer);
        Expect.AreEqual(0, remainingToTransfer);
        Expect.IsTrue(done);
    }
}
