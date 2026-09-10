namespace DiaEditCore.Model.TimeTable.Trains;

/// <summary>
/// 列車の運行を表現する
/// </summary>
public sealed class Train
{
    /// <summary>
    /// 列車識別子
    /// </summary>
    public required TrainId Id { get; set; }
    /// <summary>
    /// 所属する時刻表セットの識別子
    /// </summary>
    public required TimeTableSetId TimeTableSetId { get; set;}
    /// <summary>
    /// 列車番号
    /// </summary>
    public required string TrainNumber { get; set; }
    /// <summary>
    /// 号数
    /// </summary>
    public int? ServiceNumber { get; set; }
    /// <summary>
    /// 走行する運転系統の識別子
    /// </summary>
    public required ServiceRouteId ServiceRouteId { get; set; }

    /// <summary>
    /// 列車種別の識別子
    /// </summary>
    public required TrainTypeId TrainTypeId { get; set; }
    /// <summary>
    /// 列車種別名
    /// </summary>
    /// <remarks>
    /// ReadOnly
    /// </remarks>
    public required DisplayName TrainTypeName { get; set; }
    /// <summary>
    /// 愛称
    /// </summary>
    /// <remarks>
    /// ReadOnly
    /// </remarks>
    public required DisplayName Nickname { get; set; }
    /// <summary>
    /// 基準となる列車形式の識別子
    /// </summary>
    public VehicleTypeId? DefaultVehicleTypeId { get; set; }

    /// <summary>
    /// コピー元の列車識別子
    /// </summary>
    public TrainId? SourceTrainId { get; set; }
    /// <summary>
    /// 編集回数
    /// </summary>
    public int Revision { get; set; } = 0;
    /// <summary>
    /// コピー時のコピー元列車の編集回数
    /// </summary>
    public int? SourceRevisionAtCopy { get; set; }

    /// <summary>
    /// 列車の走行実績
    /// </summary>
    public List<TrainRunSegment> RunSegments { get; set; } = new();

    private readonly Dictionary<StopKey, StopTime> _stopTimes = new();

    /// <summary>
    /// 停車情報の読み取り専用ビュー。StopKeyの追加・削除・差し替えは外部から不可能
    /// </summary>
    /// <remarks>
    /// StopTimeインスタンス自体のフィールド（ArrivalSeconds等）はこのスコープの対象外で、
    /// 依然として可変（将来の停車時刻編集コマンド設計時に別途検討）。
    /// </remarks>
    public IReadOnlyDictionary<StopKey, StopTime> StopTimes => _stopTimes;

    /// <summary>
    /// StopTimes辞書への書き込み専用ルート。DiaEditCoreアセンブリ内
    /// （SyncRunSegmentsToTrainCommand等の正規コマンド、およびテストのフィクスチャ構築）からのみ
    /// 使用すること。ViewModel/UI層（別アセンブリ）からは参照できない。
    /// </summary>
    internal Dictionary<StopKey, StopTime> StopTimesInternal => _stopTimes;

    /// <summary>
    /// 仮列車フラグ
    /// </summary>
    public bool IsProvisional { get; set; } = false;
}