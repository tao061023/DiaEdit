namespace DiaEditCore.Commands.Stations.FloorUnitObjects;

using DiaEditCore.Algorithm.Dependency;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Session;

/// <summary>
/// Platform.Name / FacingRailIds / EffectiveLength のスナップショット。
///
/// FacingRailIds（List&lt;RailId&gt;）は参照型のミュータブルなコレクションであるため、
/// DisplayName（§9.2項目31修正時に判明した問題）と同様の理由で、コンストラクタ・
/// CaptureSnapshot双方でToList()による防御的コピーを行う。呼び出し元が渡したリスト
/// インスタンスをスナップショット側がそのまま保持すると、呼び出し元の後続変更が
/// Undo用スナップショットを汚染しうるため。
/// </summary>
public sealed record PlatformSnapshot
{
    public string Name { get; }
    public IReadOnlyList<RailId> FacingRailIds { get; }
    public double? EffectiveLength { get; }

    public PlatformSnapshot(string name, IReadOnlyList<RailId> facingRailIds, double? effectiveLength)
    {
        Name = name;
        FacingRailIds = facingRailIds.ToList(); // 防御的コピー
        EffectiveLength = effectiveLength;
    }
}

/// <summary>
/// 「属性変更」パターンのPlatform向け実装（§9.2項目34）。ChangeRailAttributesCommandに続く
/// 実装例だが、Rail側4フィールドが全て値型だったのに対し、Platformは参照型コレクション
/// （FacingRailIds）を含む点が異なる。
///
/// AffectedIdsはDependencyResolver.ResolveAffectedで算出。DependencyResolverのグラフ上、
/// PlatformObjectIdは現時点で終端ノード（他オブジェクトへの波及ルール未定義）のため、
/// AffectedIdsは対象自身のみとなる。
/// </summary>
public sealed class ChangePlatformAttributesCommand : UndoableCommand<Platform, PlatformSnapshot>
{
    private readonly PlatformSnapshot _newValues;

    public ChangePlatformAttributesCommand(Platform target, PlatformSnapshot newValues, ProjectSession session)
        : base(target, BuildAffectedIds(target, session))
    {
        _newValues = newValues;
    }

    private static IReadOnlySet<ObjectId> BuildAffectedIds(Platform target, ProjectSession session)
    {
        var cache = session.GetCache();
        return DependencyResolver.ResolveAffected(
            new HashSet<ObjectId> { new PlatformObjectId(target.Id) }, cache);
    }

    protected override PlatformSnapshot CaptureSnapshot(Platform target) => new(
        target.Name,
        target.FacingRailIds,
        target.EffectiveLength);

    protected override void Apply(Platform target)
    {
        target.Name = _newValues.Name;
        target.FacingRailIds = _newValues.FacingRailIds.ToList(); // 防御的コピー
        target.EffectiveLength = _newValues.EffectiveLength;
    }

    protected override void Restore(Platform target, PlatformSnapshot snapshot)
    {
        target.Name = snapshot.Name;
        target.FacingRailIds = snapshot.FacingRailIds.ToList(); // 防御的コピー
        target.EffectiveLength = snapshot.EffectiveLength;
    }
}