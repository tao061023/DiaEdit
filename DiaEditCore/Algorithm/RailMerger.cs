using DiaEditCore.Model;
using DiaEditCore.Model.Stations;

namespace DiaEditCore.Algorithm;

public enum RailEnd { A, B }

/// <summary>
/// name統合の判定結果。両方非空かつ異なる場合はユーザー選択が必要（呼び出し側UI層の責務）。
/// </summary>
public abstract record MergeNameResolution;
public sealed record MergeNameResolved(string Name) : MergeNameResolution;
public sealed record MergeNameConflict(string NameA, string NameB) : MergeNameResolution;

/// <summary>
/// 「N=2の自動Rail統合」の純粋関数実装。2本のRailのうち、収束点側の2端点は破棄し、
/// 反対側の2端点をそのまま新Railの両端に据える（PortIndex等を含むRailEndpointRefを一切改変しない）。
/// 収束点自体はもはやSwitcherを形成しないため、収束点側の端点情報は意味を失う。
/// </summary>
public static class RailMerger
{
    /// <summary>
    /// nameの統合方針のみを判定する（副作用なし）。空文字列でない方を優先し、
    /// 両方非空かつ異なる場合は呼び出し側（UI層）にユーザー選択を促す。
    /// </summary>
    public static MergeNameResolution ResolveName(string nameA, string nameB)
    {
        var aEmpty = string.IsNullOrEmpty(nameA);
        var bEmpty = string.IsNullOrEmpty(nameB);

        if (aEmpty && bEmpty) return new MergeNameResolved("");
        if (aEmpty) return new MergeNameResolved(nameB);
        if (bEmpty) return new MergeNameResolved(nameA);
        if (nameA == nameB) return new MergeNameResolved(nameA);

        return new MergeNameConflict(nameA, nameB);
    }

    /// <summary>
    /// railA・railBを、それぞれの収束側端点（convergingSideA/B）を破棄する形で1本のRailへ統合する。
    /// 呼び出し前提：railA.Role == railB.Role == RailRole.Normal
    /// nameが両方非空かつ異なる場合は resolvedName に確定済みの文字列を明示的に渡すこと
    /// （ResolveNameがMergeNameConflictを返したケースをユーザー選択で解決した後の値）。
    /// </summary>
    public static Rail MergeAtConvergence(
        Rail railA, RailEnd convergingSideA,
        Rail railB, RailEnd convergingSideB,
        RailId newId,
        string resolvedName)
    {
        var keptA = convergingSideA == RailEnd.A ? railA.EndpointB : railA.EndpointA;
        var keptB = convergingSideB == RailEnd.A ? railB.EndpointB : railB.EndpointA;

        return new Rail
        {
            Id = newId,
            Name = resolvedName,
            LengthM = railA.LengthM + railB.LengthM,
            SpeedLimitKph = Math.Min(railA.SpeedLimitKph, railB.SpeedLimitKph),
            Role = railA.Role,
            EndpointA = keptA,
            EndpointB = keptB,
            ControlPoints = new(),
        };
    }
}