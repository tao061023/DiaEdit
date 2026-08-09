using DiaEditCore.Model.Cars;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.TimeTable;
using DiaEditCore.Model.TimeTable.Trains;

namespace DiaEditCore.Model;

/// <summary>
/// 1プロジェクト1JSON方針における保存ファイルのルート集約オブジェクト。
///
/// 設計方針（v11.38確定）：
///   - SchemaVersion：将来の保存形式変更に備え、先頭にスキーマバージョンを持たせる。
///     読込時に対応できないバージョンなら明示的にエラーとする（JsonProjectFileSerializer側で実施）。
///   - 所有構造ではなくフラットなコレクション（論点H①、ValidationContextと同型）：
///     Model層のオブジェクト間関係の大半はグラフ構造（forward-reference・共有参照・多対多）であり、
///     きれいな木構造を持つのはStation→FloorUnit程度に限られる。ProjectFile用に別の集約構造
///     （マッピング変換コード）を新設すると、Model層（5章）と二重管理になり保守コストと
///     データ破損リスクが増える。「読みやすさ」はJSON整形出力＋プロパティ宣言順序で確保する。
///   - プロパティ順序は推奨実装順序（下流→上流の依存順）に揃える。
///
/// ValidationContextとの違い：
///   - ValidationContextは「検証に必要な参照の寄せ集め」であり、IReadOnlyList＋init専用。
///   - ProjectFileは「保存・読込の実体」であり、List＋setterを持つ（読込後にUIから編集されるため）。
///   - ProjectFile → ValidationContextへの変換は JsonProjectFileSerializer 側の
///     ToValidationContext() 拡張メソッドで行う（1箇所に集約し、フィールド追加時の対応漏れを防ぐ）。
/// </summary>
public sealed class ProjectFile
{
    /// <summary>
    /// 保存形式のスキーマバージョン。現バージョンは1。
    /// 読込時にJsonProjectFileSerializerが未対応バージョンを検知した場合は例外を送出する。
    /// </summary>
    public required int SchemaVersion { get; set; } = 1;

    public required ProjectSettings ProjectSettings { get; set; }

    // ── 駅構内オブジェクト ──
    public List<Station> Stations { get; set; } = new();
    public List<FloorUnit> FloorUnits { get; set; } = new();
    public List<Rail> Rails { get; set; } = new();
    public List<EntryPoint> EntryPoints { get; set; } = new();
    public List<BoundaryPoint> BoundaryPoints { get; set; } = new();
    public List<Switcher> Switchers { get; set; } = new();
    public List<BufferStop> BufferStops { get; set; } = new();
    public List<Platform> Platforms { get; set; } = new(); // ★v11.38追加：ValidationContext側の追加漏れも合わせて修正
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
