using RimTestRedux;

namespace RealisticOrbitalTrade.Tests;

[TestSuite]
internal static class ComputeStackTransferAmountTests
{
    // Regression guard: the silver-return loop used to subtract the full stack count instead of
    // the amount actually transferred and only broke on an exact zero, so a stack larger than the
    // remaining amount owed drove leftToTransfer negative and caused later stacks to be
    // transferred with a negative count.
    [Test]
    public static void StackLargerThanRemainingOnlyTakesWhatIsOwed()
    {
        var (countToTransfer, remainingToTransfer, done) =
            Dialog_RenegotiateTrade.ComputeStackTransferAmount(leftToTransfer: 5, stackCount: 20);

        Assert.That(countToTransfer).Is.EqualTo(5);
        Assert.That(remainingToTransfer).Is.EqualTo(0);
        Assert.That(done).Is.True();
    }

    [Test]
    public static void StackSmallerThanRemainingTakesWholeStackAndContinues()
    {
        var (countToTransfer, remainingToTransfer, done) =
            Dialog_RenegotiateTrade.ComputeStackTransferAmount(leftToTransfer: 20, stackCount: 5);

        Assert.That(countToTransfer).Is.EqualTo(5);
        Assert.That(remainingToTransfer).Is.EqualTo(15);
        Assert.That(done).Is.False();
    }

    [Test]
    public static void ExactlyMatchingStackFinishesCleanly()
    {
        var (countToTransfer, remainingToTransfer, done) =
            Dialog_RenegotiateTrade.ComputeStackTransferAmount(leftToTransfer: 5, stackCount: 5);

        Assert.That(countToTransfer).Is.EqualTo(5);
        Assert.That(remainingToTransfer).Is.EqualTo(0);
        Assert.That(done).Is.True();
    }
}
