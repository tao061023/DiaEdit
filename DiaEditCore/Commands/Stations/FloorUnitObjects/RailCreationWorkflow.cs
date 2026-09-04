namespace DiaEditCore.Commands.Stations.FloorUnitObjects;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Session;

/// <summary>
/// Rail作成時の各端点の生成方法を指定する判別共用体。
///
/// §7.1確定仕様「駅構内オブジェクト新規配置」：独立配置操作はRail・Platform・SignalSetの3種のみで、
/// BoundaryPoint/EntryPoint/BufferStopはRail端点クリック→属性タブでの端点種別選択から生成される
/// （Switcherは収束検出・テンプレート配置の別導線のため、本ワークフローの選択肢に含めない）。
/// </summary>
public abstract record RailEndpointCreationSpec;

/// <summary>端点を作成せず未接続（NoneEndpointRef）のままにする。</summary>
public sealed record NoneEndpointCreationSpec : RailEndpointCreationSpec;

public sealed record BoundaryPointCreationSpec(FloorUnitId FloorUnitId, Point Position, string Name = "") : RailEndpointCreationSpec;

public sealed record EntryPointCreationSpec(FloorUnitId FloorUnitId, Point Position, EntryPointType Type, string Name = "") : RailEndpointCreationSpec;

public sealed record BufferStopCreationSpec(FloorUnitId FloorUnitId, Point Position, string Name = "") : RailEndpointCreationSpec;

/// <summary>
/// 「Rail作成＝両端点オブジェクトの作成と等価」（Tao様確認済み、v13.7セッション）という業務理解に基づく
/// 複合コマンド。StationCreationWorkflow（Station＋FloorUnit）と同じ設計パターンを踏襲する：
/// CreateRailCommandと各端点用Create*Commandを独立に実行した後、最後にAttachRailEndpointsCommandで
/// Rail.EndpointA/Bへ確定させる。この4ステップ（Rail／端点A／端点B／アタッチ）を1つの
/// TransactionCommandに束ねることで、「両端未接続のRailが宙に浮いた状態のまま保存される」
/// （n≥1制約と同種の中間不整合状態）をUndo単位のレベルで発生させない。
///
/// Switcherはこのワークフローの対象外（コンストラクタ引数に含めない）。既存端点への接続
/// （新規作成ではなく既存BoundaryPoint/EntryPoint/BufferStop/Switcherへ繋ぐ導線）も対象外とし、
/// 別ワークフローとして将来切り出す（Tao様確認済み）。
/// </summary>
public static class RailCreationWorkflow
{
    public static TransactionCommand CreateRailWithEndpoints(
        List<Rail> rails,
        IdAllocator<RailId> railIds,
        string name,
        double lengthM,
        double speedLimitKph,
        RailRole role,
        RailEndpointCreationSpec endpointA,
        RailEndpointCreationSpec endpointB,
        List<BoundaryPoint> boundaryPoints,
        IdAllocator<BoundaryPointId> boundaryPointIds,
        List<EntryPoint> entryPoints,
        IdAllocator<EntryPointId> entryPointIds,
        List<BufferStop> bufferStops,
        IdAllocator<BufferStopId> bufferStopIds,
        ProjectSession session)
    {
        var createRail = new CreateRailCommand(rails, railIds, name, lengthM, speedLimitKph, role);

        var commands = new List<Func<IUndoableCommand>> { () => createRail };

        var factoryA = AddEndpointCreationStep(
            endpointA, boundaryPoints, boundaryPointIds, entryPoints, entryPointIds, bufferStops, bufferStopIds, commands);
        var factoryB = AddEndpointCreationStep(
            endpointB, boundaryPoints, boundaryPointIds, entryPoints, entryPointIds, bufferStops, bufferStopIds, commands);

        commands.Add(() => new AttachRailEndpointsCommand(createRail.Created!, factoryA, factoryB, session));

        return new TransactionCommand(commands);
    }

    /// <summary>
    /// specに応じた端点作成コマンドをcommandsへ追加し（NoneEndpointCreationSpecの場合は追加しない）、
    /// AttachRailEndpointsCommandが後で評価するためのFunc&lt;RailEndpointRef&gt;を返す。
    /// </summary>
    private static Func<RailEndpointRef> AddEndpointCreationStep(
        RailEndpointCreationSpec spec,
        List<BoundaryPoint> boundaryPoints,
        IdAllocator<BoundaryPointId> boundaryPointIds,
        List<EntryPoint> entryPoints,
        IdAllocator<EntryPointId> entryPointIds,
        List<BufferStop> bufferStops,
        IdAllocator<BufferStopId> bufferStopIds,
        List<Func<IUndoableCommand>> commands)
    {
        switch (spec)
        {
            case NoneEndpointCreationSpec:
                return () => new NoneEndpointRef();

            case BoundaryPointCreationSpec b:
                var createBoundary = new CreateBoundaryPointCommand(boundaryPoints, boundaryPointIds, b.FloorUnitId, b.Position, b.Name);
                commands.Add(() => createBoundary);
                return () => new BoundaryPointEndpointRef(createBoundary.Created!.Id);

            case EntryPointCreationSpec e:
                var createEntry = new CreateEntryPointCommand(entryPoints, entryPointIds, e.FloorUnitId, e.Position, e.Type, e.Name);
                commands.Add(() => createEntry);
                return () => new EntryPointEndpointRef(createEntry.Created!.Id);

            case BufferStopCreationSpec bs:
                var createBufferStop = new CreateBufferStopCommand(bufferStops, bufferStopIds, bs.FloorUnitId, bs.Position, bs.Name);
                commands.Add(() => createBufferStop);
                return () => new BufferStopEndpointRef(createBufferStop.Created!.Id);

            default:
                throw new NotSupportedException($"未知のRailEndpointCreationSpec型: {spec.GetType().Name}");
        }
    }
}