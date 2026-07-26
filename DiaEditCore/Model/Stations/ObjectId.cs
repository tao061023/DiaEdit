namespace DiaEditCore.Model.Stations;

public abstract record ObjectId;

public sealed record BoundaryPointObjectId(BoundaryPointId Id) : ObjectId;
public sealed record EntryPointObjectId(EntryPointId Id) : ObjectId;
public sealed record BufferStopObjectId(BufferStopId Id) : ObjectId;
public sealed record SwitcherObjectId(SwitcherId Id) : ObjectId;

public sealed record VirtualConflictObjectIdObject(VirtualConflictObjectId Id) : ObjectId;
