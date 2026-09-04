namespace DiaEditCore.Commands.Stations.FloorUnitObjects;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Session;

/// <summary>
/// 「新規登録（Create）」パターンのRail向け実装。
///
/// v13.9変更（NoneEndpoint実体化に伴う再設計）：EndpointA/Bは常に確定済みの参照として生成する
/// （旧実装のNoneEndpointRef仮置き→AttachRailEndpointsCommandによる後続上書き、という
/// 2段階方式は廃止）。TransactionCommand内での実行順序を「端点作成→Rail作成」に入れ替えたことで、
/// Railが一度も無効な参照を持たない（構造的防止の原則により忠実な）状態を実現する。
///
/// endpointA/BFactory：RailCreationWorkflow内でTransactionCommandの各ステップが順に実行される際、
/// 端点作成コマンド（Create*Command）のApply()が完了した"後"でなければ生成されたIdが確定しない
/// ため、StationCreationWorkflow・旧AttachRailEndpointsCommandと同じ遅延評価パターン
/// （Func&lt;RailEndpointRef&gt;によるクロージャ参照）を用いる。
///
/// ID採番はCreateStationCommandと同じ方針（セッション中は最大IdValue+1、欠番は詰めない）。
/// AffectedIdsは新規登録パターンの規約通り空集合。
/// </summary>
public sealed class CreateRailCommand : UndoableCommand<List<Rail>, Rail?>
{
    private readonly IdAllocator<RailId> _idAllocator;
    private readonly string _name;
    private readonly double _lengthM;
    private readonly double _speedLimitKph;
    private readonly RailRole _role;
    private readonly Func<RailEndpointRef> _endpointAFactory;
    private readonly Func<RailEndpointRef> _endpointBFactory;

    /// <summary>Execute()実行後、生成されたRailを呼び出し元が参照するためのプロパティ。</summary>
    public Rail? Created { get; private set; }

    public CreateRailCommand(
        List<Rail> rails,
        IdAllocator<RailId> idAllocator,
        string name,
        double lengthM,
        double speedLimitKph,
        RailRole role,
        Func<RailEndpointRef> endpointAFactory,
        Func<RailEndpointRef> endpointBFactory)
        : base(rails, new HashSet<ObjectId>()) // 新規登録：AffectedIdsは空集合
    {
        _idAllocator = idAllocator;
        _name = name;
        _lengthM = lengthM;
        _speedLimitKph = speedLimitKph;
        _role = role;
        _endpointAFactory = endpointAFactory;
        _endpointBFactory = endpointBFactory;
    }

    protected override IReadOnlySet<ObjectId> ComputeAffectedIdsAfterApply(List<Rail> target) =>
        Created is not null
            ? new HashSet<ObjectId> { new RailObjectId(Created.Id) }
            : new HashSet<ObjectId>();

    protected override Rail? CaptureSnapshot(List<Rail> target) => null;

    protected override void Apply(List<Rail> target)
    {
        if (Created is not null)
        {
            // Redo経路：初回Execute時に生成・保持したインスタンスを再挿入する。
            // AllocateNextIdもendpointファクトリも呼び直さない（§9.1項目23と同じ理由）。
            target.Add(Created);
            return;
        }

        Created = new Rail
        {
            Id = _idAllocator.AllocateNext(),
            Name = _name,
            LengthM = _lengthM,
            SpeedLimitKph = _speedLimitKph,
            Role = _role,
            // ファクトリの評価はここで初めて行う（TransactionCommand内の実行順序上、
            // 端点作成コマンドのApply()は必ず本コマンドのApply()より先に完了している）。
            EndpointA = _endpointAFactory(),
            EndpointB = _endpointBFactory(),
        };
        target.Add(Created);
    }

    protected override void Restore(List<Rail> target, Rail? snapshot)
    {
        if (Created is not null)
        {
            target.Remove(Created);
        }
    }
}