namespace DiaEditCore.Model.TimeTable.Trains;

public sealed class Train
{
    public required TrainId Id { get; set; }
    public required TimeTableSetId TimeTableSetId { get; set;}
    public required string TrainNumber { get; set; }
    public int? ServiceNumber { get; set; }
    public required ServiceRouteId ServiceRouteId { get; set; }

    public required TrainTypeId TrainTypeId { get; set; }
    public required DisplayName TrainTypeName { get; set; }
    public required DisplayName Nickname { get; set; }
    public VehicleTypeId? DefaultVehicleTypeId { get; set; }

    public TrainId? SourceTrainId { get; set; }
    public int Revision { get; set; } = 0;
    public int? SourceRevisionAtCopy { get; set; }

    public List<TrainRunSegment> RunSegments { get; set; } = new();

    private readonly Dictionary<StopKey, StopTime> _stopTimes = new();

    /// <summary>
    /// 停車情報の読み取り専用ビュー。StopKeyの追加・削除・差し替えは外部から不可能
    /// （StopKeySequenceBuilderを経由しない直接new StopKey(...)の挿入を型で防ぐ、§9.2項目9）。 <br/>
    /// StopTimeインスタンス自体のフィールド（ArrivalSeconds等）はこのスコープの対象外で、
    /// 依然として可変（将来の停車時刻編集コマンド設計時に別途検討）。
    /// </summary>
    public IReadOnlyDictionary<StopKey, StopTime> StopTimes => _stopTimes;

    /// <summary>
    /// StopTimes辞書への書き込み専用ルート。DiaEditCoreアセンブリ内
    /// （SyncRunSegmentsToTrainCommand等の正規コマンド、およびテストのフィクスチャ構築）からのみ
    /// 使用すること。ViewModel/UI層（別アセンブリ）からは参照できない。
    /// </summary>
    internal Dictionary<StopKey, StopTime> StopTimesInternal => _stopTimes;

    public bool IsProvisional { get; set; } = false;
}