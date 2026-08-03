namespace DiaEditCore.Serialization.Validation;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.Cars;
using DiaEditCore.Model.TimeTable;

/// <summary>
/// 他オブジェクトを跨いだ検証に必要な参照一式。
/// </summary>
public sealed class ValidationContext
{
    public IReadOnlyList<FloorUnit> FloorUnits { get; init; } = Array.Empty<FloorUnit>();
    public IReadOnlyList<Station> Stations { get; init; } = Array.Empty<Station>();
    public IReadOnlyList<Rail> Rails { get; init; } = Array.Empty<Rail>();
    public IReadOnlyList<EntryPoint> EntryPoints { get; init; } = Array.Empty<EntryPoint>();
    public IReadOnlyList<BoundaryPoint> BoundaryPoints { get; init; } = Array.Empty<BoundaryPoint>();
    public IReadOnlyList<Switcher> Switchers { get; init; } = Array.Empty<Switcher>();
    public IReadOnlyList<BufferStop> BufferStops { get; init; } = Array.Empty<BufferStop>();
    public IReadOnlyList<Platform> Platforms { get; init; } = Array.Empty<Platform>();
    public IReadOnlyList<StationPath> StationPaths { get; init; } = Array.Empty<StationPath>();

    public IReadOnlyList<StationConnectionSegment> StationConnectionSegments { get; init; } = Array.Empty<StationConnectionSegment>();
    public IReadOnlyList<MainRoute> MainRoutes { get; init; } = Array.Empty<MainRoute>();
    public IReadOnlyList<StationConnection> StationConnections { get; init; } = Array.Empty<StationConnection>();
    public IReadOnlyList<ServiceRoute> ServiceRoutes { get; init; } = Array.Empty<ServiceRoute>();
 
    public IReadOnlyList<Car> Cars { get; init; } = Array.Empty<Car>();
    public IReadOnlyList<CarConsist> CarConsists { get; init; } = Array.Empty<CarConsist>();
    public IReadOnlyList<CarComposition> CarCompositions { get; init; } = Array.Empty<CarComposition>();
    public IReadOnlyList<VehicleType> VehicleTypes { get; init; } = Array.Empty<VehicleType>();
    // public IReadOnlyList<InsertionConfig> InsertionConfigs { get; init; } = Array.Empty<InsertionConfig>();
 
    public IReadOnlyList<TrainType> TrainTypes { get; init; } = Array.Empty<TrainType>();
    public IReadOnlyList<Train> Trains { get; init; } = Array.Empty<Train>();
    public IReadOnlyList<TimeTableSet> TimeTableSets { get; init; } = Array.Empty<TimeTableSet>();
    public IReadOnlyList<DiagramRevision> DiagramRevisions { get; init; } = Array.Empty<DiagramRevision>();
    public IReadOnlyList<TemporaryRestriction> TemporaryRestrictions { get; init; } = Array.Empty<TemporaryRestriction>();
    public IReadOnlyList<DisplayContext> DisplayContexts { get; init; } = Array.Empty<DisplayContext>();
    public IReadOnlyList<TrainOperation> TrainOperations { get; init; } = Array.Empty<TrainOperation>();
}