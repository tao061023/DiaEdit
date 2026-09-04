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
public sealed record NoneEndpointCreationSpec(FloorUnitId FloorUnitId, Point Position, string Name = "") : RailEndpointCreationSpec;

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
        List<NoneEndpoint> noneEndpoints,
        IdAllocator<NoneEndpointId> noneEndpointIds,
        List<BoundaryPoint> boundaryPoints,
        IdAllocator<BoundaryPointId> boundaryPointIds,
        List<EntryPoint> entryPoints,
        IdAllocator<EntryPointId> entryPointIds,
        List<BufferStop> bufferStops,
        IdAllocator<BufferStopId> bufferStopIds,
        ProjectSession session)
    {
        var commands = new List<Func<IUndoableCommand>>();

        // 変更：端点A・Bを先に作成する（旧実装はRailを先に作りAttachRailEndpointsCommandで後追い確定していた）
        var factoryA = AddEndpointCreationStep(
            endpointA, noneEndpoints, noneEndpointIds,
            boundaryPoints, boundaryPointIds, entryPoints, entryPointIds, bufferStops, bufferStopIds, commands);
        var factoryB = AddEndpointCreationStep(
            endpointB, noneEndpoints, noneEndpointIds,
            boundaryPoints, boundaryPointIds, entryPoints, entryPointIds, bufferStops, bufferStopIds, commands);

        var createRail = new CreateRailCommand(rails, railIds, name, lengthM, speedLimitKph, role, factoryA, factoryB);
        commands.Add(() => createRail);

        return new TransactionCommand(commands);
    }

    private static Func<RailEndpointRef> AddEndpointCreationStep(
        RailEndpointCreationSpec spec,
        List<NoneEndpoint> noneEndpoints,
        IdAllocator<NoneEndpointId> noneEndpointIds,
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
            case NoneEndpointCreationSpec n:
                var createNone = new CreateFloorUnitObjectCommand<NoneEndpointId, NoneEndpoint>(
                    noneEndpoints, noneEndpointIds,
                    id => new NoneEndpoint { Id = id, Base = new FloorUnitObjectBase { FloorUnitId = n.FloorUnitId, Position = n.Position }, Name = n.Name },
                    created => new NoneEndpointObjectId(created.Id));
                commands.Add(() => createNone);
                return () => new NoneEndpointRef(createNone.Created!.Id);

            case BoundaryPointCreationSpec b:
                var createBoundary = new CreateFloorUnitObjectCommand<BoundaryPointId, BoundaryPoint>(
                    boundaryPoints, boundaryPointIds,
                    id => new BoundaryPoint { Id = id, Base = new FloorUnitObjectBase { FloorUnitId = b.FloorUnitId, Position = b.Position }, Name = b.Name },
                    created => new BoundaryPointObjectId(created.Id));
                commands.Add(() => createBoundary);
                return () => new BoundaryPointEndpointRef(createBoundary.Created!.Id);

            case EntryPointCreationSpec e:
                var createEntry = new CreateFloorUnitObjectCommand<EntryPointId, EntryPoint>(
                    entryPoints, entryPointIds,
                    id => new EntryPoint { Id = id, Base = new FloorUnitObjectBase { FloorUnitId = e.FloorUnitId, Position = e.Position }, Name = e.Name, Type = e.Type },
                    created => new EntryPointObjectId(created.Id));
                commands.Add(() => createEntry);
                return () => new EntryPointEndpointRef(createEntry.Created!.Id);

            case BufferStopCreationSpec bs:
                var createBufferStop = new CreateFloorUnitObjectCommand<BufferStopId, BufferStop>(
                    bufferStops, bufferStopIds,
                    id => new BufferStop { Id = id, Base = new FloorUnitObjectBase { FloorUnitId = bs.FloorUnitId, Position = bs.Position }, Name = bs.Name },
                    created => new BufferStopObjectId(created.Id));
                commands.Add(() => createBufferStop);
                return () => new BufferStopEndpointRef(createBufferStop.Created!.Id);

            default:
                throw new NotSupportedException($"未知のRailEndpointCreationSpec型: {spec.GetType().Name}");
        }
    }
}