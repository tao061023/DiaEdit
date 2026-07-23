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
}