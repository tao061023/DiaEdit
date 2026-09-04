namespace DiaEditCore.Commands.Stations.FloorUnitObjects;

using DiaEditCore.Algorithm.Dependency;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Session;

/// <summary>
/// Rail.EndpointA/EndpointBを設定する「属性変更」パターンの実装。
///
/// ChangeRailAttributesCommand（Name/LengthM/SpeedLimitKph/Role専用）とスコープを分離した理由：
/// ChangeRailAttributesCommandのXMLコメントに明記の通り、EndpointA/EndpointB（接続トポロジー変更）は
/// 同コマンドのスコープ外とされている。本コマンドはその「別途設計」の第一弾として、
/// RailCreationWorkflow専用（新規Rail作成直後、両端をNoneEndpointRefから確定させる用途）に限定する。
/// 既存Railの端点差し替え（トポロジー変更）はSwitcherコマンド実装時にあわせて別途設計する
/// （DependencyResolverのグラフ更新との整合を検討する必要があるため、ChangeRailAttributesCommandの
/// コメントで示唆されている通り）。
///
/// endpointA/BFactory：RailCreationWorkflow内でTransactionCommandの各ステップが順に実行される際、
/// 端点作成コマンド（CreateBoundaryPointCommand等）のApply()が完了した"後"でなければ
/// 生成されたIdが確定しないため、StationCreationWorkflowと同じ遅延評価パターン
/// （Func&lt;RailEndpointRef&gt;によるクロージャ参照）を用いる。
/// </summary>
public sealed class AttachRailEndpointsCommand : UndoableCommand<Rail, (RailEndpointRef A, RailEndpointRef B)>
{
    private readonly Func<RailEndpointRef> _endpointAFactory;
    private readonly Func<RailEndpointRef> _endpointBFactory;

    public AttachRailEndpointsCommand(
        Rail target,
        Func<RailEndpointRef> endpointAFactory,
        Func<RailEndpointRef> endpointBFactory,
        ProjectSession session)
        : base(target, BuildAffectedIds(target, session))
    {
        _endpointAFactory = endpointAFactory;
        _endpointBFactory = endpointBFactory;
    }

    private static IReadOnlySet<ObjectId> BuildAffectedIds(Rail target, ProjectSession session)
    {
        var cache = session.GetCache();
        return DependencyResolver.ResolveAffected(
            new HashSet<ObjectId> { new RailObjectId(target.Id) }, cache);
    }

    protected override (RailEndpointRef A, RailEndpointRef B) CaptureSnapshot(Rail target) =>
        (target.EndpointA, target.EndpointB);

    protected override void Apply(Rail target)
    {
        // ファクトリの評価はここで初めて行う（TransactionCommand内の実行順序上、
        // 端点作成コマンドのApply()は必ず本コマンドのApply()より先に完了している）。
        target.EndpointA = _endpointAFactory();
        target.EndpointB = _endpointBFactory();
    }

    protected override void Restore(Rail target, (RailEndpointRef A, RailEndpointRef B) snapshot)
    {
        target.EndpointA = snapshot.A;
        target.EndpointB = snapshot.B;
    }
}