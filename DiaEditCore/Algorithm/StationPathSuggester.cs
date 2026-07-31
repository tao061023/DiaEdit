using DiaEditCore.Model;
using DiaEditCore.Model.Stations;

namespace DiaEditCore.Algorithm;

/// <summary>
/// 駅単位で構内探索を行い、StationPath の候補 waypoint 列を返す。
/// 保存は行わず、UI に一時提示するだけ。
/// </summary>
public sealed class StationPathSuggester
{
    private readonly StationId _stationId;

    private readonly IReadOnlyDictionary<RailId, Rail> _rails;
    private readonly IReadOnlyDictionary<BoundaryPointId, BoundaryPoint> _bps;
    private readonly IReadOnlyDictionary<EntryPointId, EntryPoint> _eps;
    private readonly IReadOnlyDictionary<BufferStopId, BufferStop> _bufferStops;
    private readonly IReadOnlyDictionary<SwitcherId, Switcher> _switchers;

    private readonly IReadOnlyDictionary<FloorUnitId, StationId> _floorUnitToStation;

    public StationPathSuggester(
        StationId stationId,
        IReadOnlyDictionary<RailId, Rail> rails,
        IReadOnlyDictionary<BoundaryPointId, BoundaryPoint> bps,
        IReadOnlyDictionary<EntryPointId, EntryPoint> eps,
        IReadOnlyDictionary<BufferStopId, BufferStop> bufferStops,
        IReadOnlyDictionary<SwitcherId, Switcher> switchers,
        IReadOnlyDictionary<FloorUnitId, StationId> floorUnitToStation)
    {
        _stationId = stationId;

        _rails = rails;
        _bps = bps;
        _eps = eps;
        _bufferStops = bufferStops;
        _switchers = switchers;
        _floorUnitToStation = floorUnitToStation;
    }

    /// <summary>
    /// 入力：BoundaryPointEndpointRef または EntryPointEndpointRef
    /// 出力：候補 StationPathWaypoint 配列のリスト
    /// </summary>
    public IReadOnlyList<StationPathWaypoint[]> Suggest(RailEndpointRef start)
    {
        var results = new List<StationPathWaypoint[]>();
        var visited = new HashSet<RailEndpointRef>();

        DFS(start, isStart: true, new List<StationPathWaypoint>(), visited, results);

        return results;
    }

    private void DFS(
        RailEndpointRef current,
        bool isStart,
        List<StationPathWaypoint> path,
        HashSet<RailEndpointRef> visited,
        List<StationPathWaypoint[]> results)
    {
        // 1. ループ防止
        if (!visited.Add(current))
            return;

        // 2. 駅跨ぎ防止（駅単位インスタンスなので stationId が一致しない FloorUnit は探索しない）
        if (GetStationId(current) != _stationId)
        {
            visited.Remove(current);
            return;
        }

        // 3. 終端判定（開始点は対象外）
        if (!isStart && IsTerminal(current))
        {
            var terminalWp = ToWaypoint(current);
            var resultPath = terminalWp is not null
                ? path.Append(terminalWp).ToArray()
                : path.ToArray(); // BufferStop等、waypoint化できない終端は空配列のまま候補として残す

            results.Add(resultPath);

            visited.Remove(current);
            return;
        }

        // 4. waypoint 追加（開始点自身は path に含めない）
        StationPathWaypoint? waypoint = null;
        if (!isStart)
        {
            waypoint = ToWaypoint(current);
            if (waypoint is not null)
                path.Add(waypoint);
        }

        // 5. 隣接 endpoint を列挙（Rail経由 ＋ Switcher内部ポート間遷移）
        foreach (var nextEp in GetAdjacent(current))
        {
            if (!CanTraverseSwitcher(current, nextEp))
                continue;

            DFS(nextEp, isStart: false, path, visited, results);
        }

        // 6. 戻りがけ処理
        if (waypoint is not null)
            path.RemoveAt(path.Count - 1);

        visited.Remove(current);
    }

    private bool IsTerminal(RailEndpointRef ep)
    {
        return ep is BoundaryPointEndpointRef
            || ep is EntryPointEndpointRef
            || ep is BufferStopEndpointRef;
    }

    private StationPathWaypoint? ToWaypoint(RailEndpointRef ep)
    {
        return ep switch
        {
            BoundaryPointEndpointRef b => new BoundaryPointWaypoint(b.Id),
            EntryPointEndpointRef e => new EntryPointWaypoint(e.Id),
            SwitcherEndpointRef s => new SwitcherWaypoint(s.Id),
            BufferStopEndpointRef => null,
            _ => null
        };
    }

    private StationId GetStationId(RailEndpointRef ep)
    {
        FloorUnitId floorUnitId = ep switch
        {
            BoundaryPointEndpointRef b => _bps[b.Id].Base.FloorUnitId,
            EntryPointEndpointRef e => _eps[e.Id].Base.FloorUnitId,
            BufferStopEndpointRef bs => _bufferStops[bs.Id].Base.FloorUnitId,
            SwitcherEndpointRef s => _switchers[s.Id].Base.FloorUnitId,
            _ => throw new InvalidOperationException()
        };

        return _floorUnitToStation[floorUnitId];
    }

    /// <summary>
    /// current から到達可能な次の endpoint を列挙する。
    /// ・Rail経由の隣接endpoint
    /// ・Switcher自身のポート間遷移（Railを介さない、Switcher内部の接続）
    /// </summary>
    private IEnumerable<RailEndpointRef> GetAdjacent(RailEndpointRef current)
    {
        // Rail経由の隣接
        foreach (var rail in _rails.Values)
        {
            if (Equals(rail.EndpointA, current))
                yield return rail.EndpointB;

            if (Equals(rail.EndpointB, current))
                yield return rail.EndpointA;
        }

        // Switcher内部でのポート間遷移
        if (current is SwitcherEndpointRef sw && _switchers.TryGetValue(sw.Id, out var switcher))
        {
            for (int port = 0; port < switcher.PortCount; port++)
            {
                if (port == sw.PortIndex)
                    continue;

                yield return new SwitcherEndpointRef(sw.Id, port);
            }
        }
    }

    private bool CanTraverseSwitcher(RailEndpointRef from, RailEndpointRef to)
    {
        // Switcher を通らない場合は常に true
        if (from is not SwitcherEndpointRef && to is not SwitcherEndpointRef)
            return true;

        // 片側のみ Switcher の場合は許可
        if (from is SwitcherEndpointRef && to is not SwitcherEndpointRef)
            return true;
        if (to is SwitcherEndpointRef && from is not SwitcherEndpointRef)
            return true;

        // 両端が同一 Switcher の場合のみ物理構造チェック
        if (from is SwitcherEndpointRef f && to is SwitcherEndpointRef t)
        {
            if (f.Id != t.Id)
                return true;

            var sw = _switchers[f.Id];
            var pair = SwitcherRoutingExtensions.Normalize(f.PortIndex, t.PortIndex);
            return sw.GetTraversablePairs().Contains(pair);
        }

        return true;
    }
}