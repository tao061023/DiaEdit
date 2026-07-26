using DiaEditCore.Model.Routes;

namespace DiaEditCore.Algorithm;

public sealed record EntryPointSequenceElement(
    Model.StationId FromStationId,
    Model.StationId ToStationId,
    Model.EntryPointId FromEntryPointId,
    Model.EntryPointId ToEntryPointId);

public static class EntryPointSequenceResolver
{
    /// <summary>
    /// StationConnection.segments（SCSId配列）から対応するSCS実体を引き当てて機械的に射影する。
    /// 都度導出・非保存。
    /// </summary>
    public static IReadOnlyList<EntryPointSequenceElement> Resolve(
        StationConnection sc,
        IReadOnlyList<StationConnectionSegment> allSegments)
    {
        var result = new List<EntryPointSequenceElement>(sc.Segments.Count);
        foreach (var segId in sc.Segments)
        {
            var seg = allSegments.FirstOrDefault(s => s.Id == segId);
            if (seg is null) continue; // 参照整合性エラーは別途保存時検証で検出する想定
            result.Add(new EntryPointSequenceElement(seg.FromStationId, seg.ToStationId, seg.FromEntryPointId, seg.ToEntryPointId));
        }
        return result;
    }
}