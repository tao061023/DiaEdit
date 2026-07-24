namespace DiaEditCore.Serialization.Validation;

/// <summary>
/// 他オブジェクトを跨いだ検証に必要な参照一式。
/// TimeTableSet実装時（5.13節）に本格的な形へ差し替える想定。
/// </summary>
public sealed class ValidationContext
{
    public IReadOnlyList<Model.FloorUnit> FloorUnits { get; init; } = Array.Empty<Model.FloorUnit>();
    public IReadOnlyList<Model.Station> Stations { get; init; } = Array.Empty<Model.Station>();
    public IReadOnlyList<Model.Rail> Rails { get; init; } = Array.Empty<Model.Rail>();
    public IReadOnlyList<Model.EntryPoint> EntryPoints { get; init; } = Array.Empty<Model.EntryPoint>();
    public IReadOnlyList<Model.BoundaryPoint> BoundaryPoints { get; init; } = Array.Empty<Model.BoundaryPoint>();
    public IReadOnlyList<Model.Switcher> Switchers { get; init; } = Array.Empty<Model.Switcher>();
    public IReadOnlyList<Model.StationPath> StationPaths { get; init; } = Array.Empty<Model.StationPath>();
    public IReadOnlyList<Model.StationConnectionSegment> StationConnectionSegments { get; init; } = Array.Empty<Model.StationConnectionSegment>();
    public IReadOnlyList<Model.MainRoute> MainRoutes { get; init; } = Array.Empty<Model.MainRoute>();
    public IReadOnlyList<Model.StationConnection> StationConnections { get; init; } = Array.Empty<Model.StationConnection>();
    public IReadOnlyList<Model.ServiceRoute> ServiceRoutes { get; init; } = Array.Empty<Model.ServiceRoute>();
    public IReadOnlyList<Model.Car> Cars { get; init; } = Array.Empty<Model.Car>();
    public IReadOnlyList<Model.CarConsist> CarConsists { get; init; } = Array.Empty<Model.CarConsist>();
    public IReadOnlyList<Model.VehicleType> VehicleTypes { get; init; } = Array.Empty<Model.VehicleType>();
    public IReadOnlyList<Model.TrainType> TrainTypes { get; init; } = Array.Empty<Model.TrainType>();
    public IReadOnlyList<Model.Train> Trains { get; init; } = Array.Empty<Model.Train>();
}