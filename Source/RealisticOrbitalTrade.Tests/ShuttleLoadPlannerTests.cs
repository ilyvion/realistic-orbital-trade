using DevTools.Testing;
using RealisticOrbitalTrade.Core;

namespace RealisticOrbitalTrade.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class ShuttleLoadPlannerTests
{
    [Test]
    public static void SaleWithinOneShuttleUsesOneShuttle()
    {
        var plan = ShuttleLoadPlanner.Plan(200f, 0f, [new(0, 1f, 150)]);

        Expect.AreEqual(1, plan!.Count);
        Expect.AreEqual(150, plan![0].Sum(s => s.count));
    }

    // A sale heavier than one shuttle's capacity used to be rejected outright; 1000 units at 1 kg
    // is five 200 kg shuttles.
    [Test]
    public static void SaleExceedingOneShuttleIsSplitAcrossShuttles()
    {
        var plan = ShuttleLoadPlanner.Plan(200f, 0f, [new(0, 1f, 1000)]);

        Expect.AreEqual(5, plan!.Count);
        foreach (var shuttle in plan!)
        {
            Expect.AreEqual(200, shuttle.Sum(s => s.count));
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

        Expect.IsNotNull(plan);
        var totals = (plan ?? [])
            .SelectMany(s => s)
            .GroupBy(s => s.lineId)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.count));
        Expect.AreEqual(777, totals[0]);
        Expect.AreEqual(91, totals[1]);
        Expect.AreEqual(7, totals[2]);
    }

    [Test]
    public static void NoShuttleIsLoadedPastCapacity()
    {
        List<ShuttleLoadPlanner.Line> lines = [new(0, 0.5f, 777), new(1, 3f, 91), new(2, 40f, 7)];
        var plan = ShuttleLoadPlanner.Plan(200f, 0f, lines);

        foreach (var shuttle in plan!)
        {
            var mass = shuttle.Sum(s => s.count * lines[s.lineId].UnitMass);
            Expect.IsTrue(mass <= 200.001f);
        }
    }

    [Test]
    public static void ReservedMassLeavesLessRoomOnFirstShuttleOnly()
    {
        var plan = ShuttleLoadPlanner.Plan(200f, 150f, [new(0, 1f, 100)]);

        Expect.AreEqual(2, plan!.Count);
        Expect.AreEqual(50, plan![0].Sum(s => s.count));
        Expect.AreEqual(50, plan[1].Sum(s => s.count));
    }

    [Test]
    public static void ReservedMassAloneStillYieldsTheFirstShuttle()
    {
        var plan = ShuttleLoadPlanner.Plan(200f, 60f, []);

        Expect.AreEqual(1, plan!.Count);
    }

    [Test]
    public static void ReservedMassBeyondOneShuttleIsInfeasible() =>
        Expect.IsTrue(ShuttleLoadPlanner.Plan(200f, 260f, []) == null);

    [Test]
    public static void SingleUnitHeavierThanAShuttleIsInfeasible() =>
        Expect.IsTrue(ShuttleLoadPlanner.Plan(200f, 0f, [new(0, 250f, 1)]) == null);

    [Test]
    public static void WeightlessItemsNeverForceAnExtraShuttle()
    {
        var plan = ShuttleLoadPlanner.Plan(200f, 0f, [new(0, 0f, 5000)]);

        Expect.AreEqual(1, plan!.Count);
        Expect.AreEqual(5000, plan![0].Sum(s => s.count));
    }

    [Test]
    public static void ZeroCountLinesAreIgnored()
    {
        var plan = ShuttleLoadPlanner.Plan(200f, 0f, [new(0, 1f, 0)]);

        Expect.AreEqual(1, plan!.Count);
        Expect.IsEmpty(plan![0]);
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

        Expect.AreEqual(3, plan!.Count);
        Expect.AreEqual(20, plan.Sum(s => s.Count));
        Expect.IsTrue(plan.All(s => s.Count <= 8));
    }
}
