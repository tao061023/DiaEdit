namespace DiaEditCore.Commands.Stations;

using DiaEditCore.Algorithm.Dependency;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;

/// <summary>
/// Rail.Name / LengthM / SpeedLimitKph / Role のスナップショット。
///
/// スコープについて（v12.12確定）：Rail.EndpointA/EndpointB（接続トポロジー変更）とControlPoints
/// （線路形状編集）はこのコマンドのスコープ外とする。前者はSwitcher等のコマンド実装時に、
/// DependencyResolverのグラフ更新との整合を含めて別途設計する。後者はUI上「フォーム一括保存」
/// ではなく「制御点の個別追加・移動」という異なる編集単位になるため、専用コマンドとして
/// 将来切り出す（6.1節・6.2節参照）。
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
/// 6.1節「属性変更」パターンのRail向け実装。ChangeStationAttributesCommand（v12.10）に続く
/// 2例目であり、値型のみで構成されるフィールド集合に対する適用例となる。
///
/// AffectedIdsは対象自身のObjectIdからDependencyResolver.ResolveAffectedで算出する
/// （6.1節の属性変更パターンの規約通り）。DependencyResolverのグラフ上、RailObjectIdは
/// 現時点で終端ノード（他オブジェクトへの波及ルールが未定義）のため、AffectedIdsは
/// 対象自身のみとなる。
/// </summary>
public sealed class ChangeRailAttributesCommand : UndoableCommand<Rail, RailSnapshot>
{
    private readonly RailSnapshot _newValues;

    public ChangeRailAttributesCommand(Rail target, RailSnapshot newValues, TimeTableSetCache cache)
        : base(target, DependencyResolver.ResolveAffected(
            new HashSet<ObjectId> { new RailObjectId(target.Id) }, cache))
    {
        _newValues = newValues;
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