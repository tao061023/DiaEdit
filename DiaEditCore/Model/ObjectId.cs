namespace DiaEditCore.Model;

public abstract record ObjectId;

public sealed record StationObjectId(StationId Id) : ObjectId;
public sealed record FloorUnitObjectId(FloorUnitId Id) : ObjectId;
public sealed record PlatformObjectId(PlatformId Id) : ObjectId;
public sealed record BoundaryPointObjectId(BoundaryPointId Id) : ObjectId;
public sealed record EntryPointObjectId(EntryPointId Id) : ObjectId;
public sealed record BufferStopObjectId(BufferStopId Id) : ObjectId;
public sealed record SwitcherObjectId(SwitcherId Id) : ObjectId;
public sealed record StationPathObjectId(StationPathId Id) : ObjectId;
public sealed record VirtualConflictObjectIdObject(VirtualConflictObjectId Id) : ObjectId;
public sealed record RailObjectId(RailId Id) : ObjectId;
public sealed record StationConnectionSegmentObjectId(StationConnectionSegmentId Id) : ObjectId;
public sealed record MainRouteObjectId(MainRouteId Id) : ObjectId;
public sealed record StationConnectionObjectId(StationConnectionId Id) : ObjectId;
public sealed record TemporaryRestrictionObjectId(TemporaryRestrictionId Id) : ObjectId;
public sealed record TrainObjectId(TrainId Id) : ObjectId;