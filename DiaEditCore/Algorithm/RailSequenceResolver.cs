using DiaEditCore.Model;
using DiaEditCore.Model.Stations;

namespace DiaEditCore.Algorithm;

public sealed class RailSequenceResolver
{
    private readonly IReadOnlyList<Rail> _rails;

    public RailSequenceResolver(IReadOnlyList<Rail> rails)
    {
        _rails = rails;
    }

    public IReadOnlyList<RailId> Resolve(StationPath path)
    {
        var result = new List<RailId>();

        var wp = path.Waypoints;
        if (wp.Count < 2)
            return result;

        for (int i = 0; i < wp.Count - 1; i++)
        {
            var keyA = wp[i].Key();
            var keyB = wp[i + 1].Key();

            var rail = FindRailBetween(keyA, keyB);

            if (rail is null)
            {
                throw new InvalidOperationException(
                    $"StationPath {path.Id.Value} の waypoint {i} と {i + 1} を結ぶ Rail が存在しません。"
                );
            }

            result.Add(rail.Id);
        }

        return result;
    }

    private Rail? FindRailBetween((string Kind, int Id) a, (string Kind, int Id) b)
    {
        foreach (var r in _rails)
        {
            var ra = r.EndpointA.Key();
            var rb = r.EndpointB.Key();

            // 無向一致
            if ((ra == a && rb == b) || (ra == b && rb == a))
                return r;
        }

        return null;
    }
}
