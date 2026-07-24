namespace DiaEditCore.Model;

// readonly record structなのでDictionaryキーとして構造的等価性がそのまま使える
public readonly record struct StopKey(StationId StationId, int VisitSequence);