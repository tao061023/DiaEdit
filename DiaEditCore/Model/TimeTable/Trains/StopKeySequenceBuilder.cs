namespace DiaEditCore.Model.TimeTable.Trains;

/// <summary>
/// Train.RunSegmentsから、訪問順に対応するStopKey列を導出する唯一の生成点。 <br/>
/// StopKey.VisitCountは「駅ごとのローカルな訪問回数」であり、この規約に従ってStopKeyを
/// 生成できるのはこのクラスのみとする。 <br/>
///
/// 用途：
///   1. StopTimes書き込み側（RunSegments編集コマンド）が、新規追加・リキー時のキーを
///      本メソッドの戻り値から取得する
///   2. StopTimes読み出し側（CarConsistResolver等）が、訪問順にStopKeyを辿るために使う
/// </summary>
public static class StopKeySequenceBuilder
{
    /// <summary>
    /// train.RunSegmentsが定める訪問順（先頭駅→各RunSegmentのToStationId）に対応する
    /// StopKey列を、訪問順のまま返す。戻り値のインデックスiは「経路上でi番目の停車」を
    /// 意味するが、各StopKey自体のVisitCountは駅ごとのローカルカウンタである点に注意。
    /// </summary>
    public static List<StopKey> BuildVisitedStopKeys(Train train)
    {
        var stations = new List<StationId>();
        if (train.RunSegments.Count > 0)
        {
            stations.Add(train.RunSegments[0].FromStationId);
            foreach (var segment in train.RunSegments)
            {
                stations.Add(segment.ToStationId);
            }
        }

        var visitCounts = new Dictionary<StationId, int>();
        var keys = new List<StopKey>(stations.Count);
        foreach (var stationId in stations)
        {
            var visitCount = visitCounts.TryGetValue(stationId, out var count) ? count : 0;
            keys.Add(new StopKey(stationId, visitCount));
            visitCounts[stationId] = visitCount + 1;
        }

        return keys;
    }
}