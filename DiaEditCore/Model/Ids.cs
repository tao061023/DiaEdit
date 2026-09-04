namespace DiaEditCore.Model;

/// <summary>
/// int一つだけを値として持つID型に実装させる共通インターフェース。
/// JSONシリアライズ時、ネストしたオブジェクトではなく素朴なintとして書き出すための
/// IntIdJsonConverterFactory（Serialization層）が、リフレクションを使わずこのインターフェース
/// 経由でValueを読み書きするために使う。
/// </summary>
public interface IIntId
{
    int Value { get; }
}

public readonly record struct StationId(int Value) : IIntId;
public readonly record struct FloorUnitId(int Value) : IIntId;
public readonly record struct RailId(int Value) : IIntId;
public readonly record struct NoneEndpointId(int Value) : IIntId;
public readonly record struct BoundaryPointId(int Value) : IIntId;
public readonly record struct EntryPointId(int Value) : IIntId;
public readonly record struct BufferStopId(int Value) : IIntId;
public readonly record struct PlatformId(int Value) : IIntId;
public readonly record struct SwitcherId(int Value) : IIntId;
public readonly record struct StationPathId(int Value) : IIntId;
public readonly record struct VirtualConflictObjectId(int Value) : IIntId;
public readonly record struct MainRouteId(int Value) : IIntId;
public readonly record struct StationConnectionSegmentId(int Value) : IIntId;
public readonly record struct StationConnectionId(int Value) : IIntId;
public readonly record struct ServiceRouteId(int Value) : IIntId;
public readonly record struct VehicleTypeId(int Value) : IIntId;
//public readonly record struct AttachedCarTemplateId(int Value) : IIntId;
public readonly record struct CarCompositionId(int Value) : IIntId;
public readonly record struct CarConsistId(int Value) : IIntId;
public readonly record struct CarId(int Value) : IIntId;
// public readonly record struct InsertionConfigId(int Value) : IIntId;
public readonly record struct TrainTypeId(int Value) : IIntId;
public readonly record struct TrainId(int Value) : IIntId;
public readonly record struct TrainOperationId(int Value) : IIntId;
public readonly record struct TimeTableSetId(int Value) : IIntId;
public readonly record struct DiagramRevisionId(int Value) : IIntId;
public readonly record struct TemporaryRestrictionId(int Value) : IIntId;
public readonly record struct DisplayContextId(int Value) : IIntId;