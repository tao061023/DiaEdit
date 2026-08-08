namespace DiaEditCore.Model.TimeTable.Trains;

/// <summary>
/// Trainの停車を一意に識別するキー。 <br/>
/// VisitCount：Train自身のRunSegmentsが定める訪問順において、同一StationIdへの訪問が
/// 何回目か（0-indexed）。列車全体の通し位置ではなく、駅ごとのローカルなカウンタである。 <br/>
/// 環状線・デルタ線折返しによる同一駅への複数回訪問を区別するために存在する。 <br/>
///
/// 生成は必ずStopKeySequenceBuilderを経由すること。VisitCountを手計算してnew StopKey(...)を
/// 直接構築しないこと（RunSegments編集によりVisitCountは変わりうるため、複数箇所で
/// 独自に算出すると規約の乖離が再発する）。
/// </summary>
// readonly record structなのでDictionaryキーとして構造的等価性がそのまま使える
public readonly record struct StopKey(StationId StationId, int VisitCount);