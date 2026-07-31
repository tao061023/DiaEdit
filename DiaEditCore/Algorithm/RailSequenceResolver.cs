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
            var keyA = wp[i].ToObjectId();
            var keyB = wp[i + 1].ToObjectId();

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

    private Rail? FindRailBetween(ObjectId a, ObjectId b)
    {
        foreach (var r in _rails)
        {
            var ra = r.EndpointA.ToObjectId();
            var rb = r.EndpointB.ToObjectId();

            // 無向一致
            if ((ra == a && rb == b) || (ra == b && rb == a))
                return r;
        }

        return null;
    }
}