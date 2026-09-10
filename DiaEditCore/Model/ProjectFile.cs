namespace DiaEditCore.Model;

using DiaEditCore.Model.Stations;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.Cars;
using DiaEditCore.Model.TimeTable;
using DiaEditCore.Model.TimeTable.Trains;

/// <summary>
/// 1プロジェクト1JSON方針における保存ファイルのルート集約オブジェクト。
/// </summary>
public sealed class ProjectFile
{
    /// <summary>
    /// 保存形式のスキーマバージョン。
    /// 読込時にJsonProjectFileSerializerが未対応バージョンを検知した場合は例外を送出する。
    /// </summary>
    public required int SchemaVersion { get; set; } = 1;
    /// <summary>
    /// プロジェクト設定
    /// </summary>
    public required ProjectSettings ProjectSettings { get; set; }

    // ── 駅構内オブジェクト ──
    public List<Station> Stations { get; set; } = new();
    public List<FloorUnit> FloorUnits { get; set; } = new();
    public List<Rail> Rails { get; set; } = new();
    public List<NoneEndpoint> NoneEndpoints { get; set; } = new();
    public List<EntryPoint> EntryPoints { get; set; } = new();
    public List<BoundaryPoint> BoundaryPoints { get; set; } = new();
    public List<Switcher> Switchers { get; set; } = new();
    public List<BufferStop> BufferStops { get; set; } = new();
    public List<Platform> Platforms { get; set; } = new();
    public List<StationPath> StationPaths { get; set; } = new();

    // ── 路線網 ──
    public List<StationConnectionSegment> StationConnectionSegments { get; set; } = new();
    public List<MainRoute> MainRoutes { get; set; } = new();
    public List<StationConnection> StationConnections { get; set; } = new();
    public List<ServiceRoute> ServiceRoutes { get; set; } = new();

    // ── 車両 ──
    public List<Car> Cars { get; set; } = new();
    public List<CarConsist> CarConsists { get; set; } = new();
    public List<CarComposition> CarCompositions { get; set; } = new();
    public List<VehicleType> VehicleTypes { get; set; } = new();

    // ── 時刻表 ──
    public List<TrainType> TrainTypes { get; set; } = new();
    public List<Train> Trains { get; set; } = new();
    public List<TimeTableSet> TimeTableSets { get; set; } = new();
    public List<DiagramRevision> DiagramRevisions { get; set; } = new();
    public List<TemporaryRestriction> TemporaryRestrictions { get; set; } = new();
    public List<DisplayContext> DisplayContexts { get; set; } = new();
    public List<TrainOperation> TrainOperations { get; set; } = new();
}
