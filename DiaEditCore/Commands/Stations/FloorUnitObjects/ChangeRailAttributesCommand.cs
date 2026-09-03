namespace DiaEditCore.Commands.Stations.FloorUnitObjects;

using DiaEditCore.Algorithm.Dependency;
using DiaEditCore.Commands;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Session;

/// <summary>
/// Rail.Name / LengthM / SpeedLimitKph / Role のスナップショット。
///
/// スコープについて：Rail.EndpointA/EndpointB（接続トポロジー変更）とControlPoints
/// （線路形状編集）はこのコマンドのスコープ外とする。前者はSwitcher等のコマンド実装時に、
/// DependencyResolverのグラフ更新との整合を含めて別途設計する。後者はUI上「フォーム一括保存」
/// ではなく「制御点の個別追加・移動」という異なる編集単位になるため、専用コマンドとして
/// 将来切り出す。
///
/// 4フィールドは全て値型（string／double／enum）であり、DisplayNameのような参照型が絡まないため、
/// StationSnapshotで必要だったClone()経由の防御的コピーは不要（単純代入で「不変な値」規約を満たす）。
/// </summary>
public sealed record RailSnapshot(
    string Name,
    double LengthM,
    double SpeedLimitKph,
    RailRole Role);

/// <summary>
/// 「属性変更」パターンのRail向け実装。ChangeStationAttributesCommandに続く
/// 2例目であり、値型のみで構成されるフィールド集合に対する適用例となる。
///
/// AffectedIdsは対象自身のObjectIdからDependencyResolver.ResolveAffectedで算出する。
/// DependencyResolverのグラフ上、RailObjectIdは現時点で終端ノード（他オブジェクトへの波及ルールが未定義）のため、
/// AffectedIdsは対象自身のみとなる。
///
/// session.GetCache()は一度だけ呼び出し、以降はローカル変数に保持したTimeTableSetCacheをそのままDependencyResolverへ渡す
/// （コンストラクタ実行中に複数回GetCache()を呼ぶと、その間にdirty化されるケースは無いはずだが、
/// 同一コンストラクタ内で参照するキャッシュ内容が呼び出しごとにブレる可能性を構造的に排除するため）。
/// </summary>
public sealed class ChangeRailAttributesCommand : UndoableCommand<Rail, RailSnapshot>
{
    private readonly RailSnapshot _newValues;

    public ChangeRailAttributesCommand(Rail target, RailSnapshot newValues, ProjectSession session)
        : base(target, BuildAffectedIds(target, session))
    {
        _newValues = newValues;
    }

    private static IReadOnlySet<ObjectId> BuildAffectedIds(Rail target, ProjectSession session)
    {
        var cache = session.GetCache();
        return DependencyResolver.ResolveAffected(
            new HashSet<ObjectId> { new RailObjectId(target.Id) }, cache);
    }

    protected override RailSnapshot CaptureSnapshot(Rail target) => new(
        target.Name,
        target.LengthM,
        target.SpeedLimitKph,
        target.Role);

    protected override void Apply(Rail target)
    {
        target.Name = _newValues.Name;
        target.LengthM = _newValues.LengthM;
        target.SpeedLimitKph = _newValues.SpeedLimitKph;
        target.Role = _newValues.Role;
    }

    protected override void Restore(Rail target, RailSnapshot snapshot)
    {
        target.Name = snapshot.Name;
        target.LengthM = snapshot.LengthM;
        target.SpeedLimitKph = snapshot.SpeedLimitKph;
        target.Role = snapshot.Role;
    }
}