namespace DiaEditCore.Model.TimeTable.Trains;

/// <summary>
/// Trainの停車を一意に識別するキー。
/// </summary>
/// <param name="StationId">停車・通過する駅の識別子</param>
/// <param name="VisitCount">その駅を何回訪れたか</param>
/// <remarks>
/// 生成は必ずStopKeySequenceBuilderを経由すること。VisitCountを手計算してnew StopKey(...)を直接構築しないこと。
/// readonly record structなのでDictionaryキーとして構造的等価性がそのまま使える
/// </remarks>
public readonly record struct StopKey(StationId StationId, int VisitCount);