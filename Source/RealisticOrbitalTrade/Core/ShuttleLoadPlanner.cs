namespace RealisticOrbitalTrade.Core;

/// <summary>
/// Divides everything sold to a trader across as many shuttles as its total mass needs, so a sale
/// isn't limited to what a single shuttle can carry.
/// </summary>
internal static class ShuttleLoadPlanner
{
    private const float Epsilon = 0.0001f;

    /// <summary>
    /// One sold stack: <paramref name="Count"/> units weighing <paramref name="UnitMass"/> each.
    /// <paramref name="Id"/> is whatever the caller uses to map a planned share back to its stack.
    /// </summary>
    internal readonly record struct Line(int Id, float UnitMass, int Count);

    /// <summary>
    /// First-fit-decreasing packing of <paramref name="lines"/> into shuttles of
    /// <paramref name="capacity"/> kg each, splitting a line's count across shuttles where needed.
    /// The first shuttle has <paramref name="firstShuttleReservedMass"/> already taken up (the
    /// sold pawns always board it). Always returns at least one shuttle. Returns
    /// <see langword="null"/> if the reserved mass doesn't fit on the first shuttle, or if a
    /// single unit is heavier than a whole shuttle.
    /// </summary>
    internal static List<List<(int lineId, int count)>>? Plan(
        float capacity,
        float firstShuttleReservedMass,
        IReadOnlyList<Line> lines
    )
    {
        if (firstShuttleReservedMass > capacity + Epsilon)
        {
            return null;
        }

        List<float> freeMass = [capacity - firstShuttleReservedMass];
        List<List<(int lineId, int count)>> shuttles =
        [
            [],
        ];

        foreach (var line in lines.Where(l => l.Count > 0).OrderByDescending(l => l.UnitMass))
        {
            if (line.UnitMass > capacity + Epsilon)
            {
                return null;
            }

            var remaining = line.Count;
            while (remaining > 0)
            {
                var shuttleIndex = FindShuttleWithRoom(freeMass, line.UnitMass);
                if (shuttleIndex < 0)
                {
                    freeMass.Add(capacity);
                    shuttles.Add([]);
                    shuttleIndex = freeMass.Count - 1;
                }

                var fits =
                    line.UnitMass <= 0f
                        ? remaining
                        : (int)Math.Floor((freeMass[shuttleIndex] + Epsilon) / line.UnitMass);
                var take = Math.Min(remaining, fits);
                shuttles[shuttleIndex].Add((line.Id, take));
                freeMass[shuttleIndex] -= take * line.UnitMass;
                remaining -= take;
            }
        }

        return shuttles;
    }

    private static int FindShuttleWithRoom(List<float> freeMass, float unitMass)
    {
        for (var i = 0; i < freeMass.Count; i++)
        {
            if (freeMass[i] + Epsilon >= unitMass)
            {
                return i;
            }
        }
        return -1;
    }
}
