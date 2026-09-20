using RealisticOrbitalTrade.Core;
using RimTestRedux;

namespace RealisticOrbitalTrade.Tests;

[TestSuite]
internal static class ShuttleLoadPlannerTests
{
    [Test]
    public static void SaleWithinOneShuttleUsesOneShuttle()
    {
        var plan = ShuttleLoadPlanner.Plan(200f, 0f, [new(0, 1f, 150)]);

        Assert.ThatCollection(plan!).Has.Count(1);
        Assert.That(plan![0].Sum(s => s.count)).Is.EqualTo(150);
    }

    // A sale heavier than one shuttle's capacity used to be rejected outright; 1000 units at 1 kg
    // is five 200 kg shuttles.
    [Test]
    public static void SaleExceedingOneShuttleIsSplitAcrossShuttles()
    {
        var plan = ShuttleLoadPlanner.Plan(200f, 0f, [new(0, 1f, 1000)]);

        Assert.ThatCollection(plan!).Has.Count(5);
        foreach (var shuttle in plan!)
        {
            Assert.That(shuttle.Sum(s => s.count)).Is.EqualTo(200);
        }
    }

    [Test]
    public static void EveryUnitIsAssignedExactlyOnce()
    {
        var plan = ShuttleLoadPlanner.Plan(
            200f,
            0f,
            [new(0, 0.5f, 777), new(1, 3f, 91), new(2, 40f, 7)]
        );

        var totals = plan.SelectMany(s => s)
            .GroupBy(s => s.lineId)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.count));
        Assert.That(totals[0]).Is.EqualTo(777);
        Assert.That(totals[1]).Is.EqualTo(91);
        Assert.That(totals[2]).Is.EqualTo(7);
    }

    [Test]
    public static void NoShuttleIsLoadedPastCapacity()
    {
        List<ShuttleLoadPlanner.Line> lines = [new(0, 0.5f, 777), new(1, 3f, 91), new(2, 40f, 7)];
        var plan = ShuttleLoadPlanner.Plan(200f, 0f, lines);

        foreach (var shuttle in plan!)
        {
            var mass = shuttle.Sum(s => s.count * lines[s.lineId].UnitMass);
            Assert.That(mass <= 200.001f).Is.True();
        }
    }

    [Test]
    public static void ReservedMassLeavesLessRoomOnFirstShuttleOnly()
    {
        var plan = ShuttleLoadPlanner.Plan(200f, 150f, [new(0, 1f, 100)]);

        Assert.ThatCollection(plan!).Has.Count(2);
        Assert.That(plan![0].Sum(s => s.count)).Is.EqualTo(50);
        Assert.That(plan[1].Sum(s => s.count)).Is.EqualTo(50);
    }

    [Test]
    public static void ReservedMassAloneStillYieldsTheFirstShuttle()
    {
        var plan = ShuttleLoadPlanner.Plan(200f, 60f, []);

        Assert.ThatCollection(plan).Has.Count(1);
    }

    [Test]
    public static void ReservedMassBeyondOneShuttleIsInfeasible() =>
        Assert.That(ShuttleLoadPlanner.Plan(200f, 260f, []) == null).Is.True();

    [Test]
    public static void SingleUnitHeavierThanAShuttleIsInfeasible() =>
        Assert.That(ShuttleLoadPlanner.Plan(200f, 0f, [new(0, 250f, 1)]) == null).Is.True();

    [Test]
    public static void WeightlessItemsNeverForceAnExtraShuttle()
    {
        var plan = ShuttleLoadPlanner.Plan(200f, 0f, [new(0, 0f, 5000)]);

        Assert.ThatCollection(plan!).Has.Count(1);
        Assert.That(plan![0].Sum(s => s.count)).Is.EqualTo(5000);
    }

    [Test]
    public static void ZeroCountLinesAreIgnored()
    {
        var plan = ShuttleLoadPlanner.Plan(200f, 0f, [new(0, 1f, 0)]);

        Assert.ThatCollection(plan!).Has.Count(1);
        Assert.ThatCollection(plan![0]).Is.Empty();
    }

    // Sold creatures are one-unit lines, so a herd heavier than a shuttle is spread across several.
    [Test]
    public static void HeavyCreaturesAreSpreadAcrossShuttles()
    {
        List<ShuttleLoadPlanner.Line> lines = [];
        for (var i = 0; i < 20; i++)
        {
            lines.Add(new(i, 240f, 1));
        }

        var plan = ShuttleLoadPlanner.Plan(1980f, 0f, lines);

        Assert.ThatCollection(plan).Has.Count(3);
        Assert.That(plan.Sum(s => s.Count)).Is.EqualTo(20);
        Assert.That(plan.All(s => s.Count <= 8)).Is.True();
    }
}
