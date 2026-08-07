namespace DiaEditCore.Model;

/// <summary>
/// 時刻の内部表現。当日0:00を基準とした経過秒数（int）として保持する。
/// 0〜86399: 当日 0:00〜23:59:59
/// 86400以上: 24時以降の深夜帯表記（25:30 → 91800）をそのまま扱える
/// 負値: 前日からの継続列車・日跨ぎダイヤの基準ズレ（例: -300 = 前日23:55）を表現できる
///
/// 比較・演算規約（絶対時刻基準への正規化で統一）：
/// 値自体が既にTimeTableSetの基準日0時からの経過秒数として定義されているため、
/// 追加の正規化ロジックなしに単純なint比較・減算で完結する。
/// </summary>
public readonly record struct TimeOfDaySec(int Seconds) : IComparable<TimeOfDaySec>
{
    public int CompareTo(TimeOfDaySec other) => Seconds.CompareTo(other.Seconds);

    public static bool operator <(TimeOfDaySec a, TimeOfDaySec b) => a.Seconds < b.Seconds;
    public static bool operator >(TimeOfDaySec a, TimeOfDaySec b) => a.Seconds > b.Seconds;
    public static bool operator <=(TimeOfDaySec a, TimeOfDaySec b) => a.Seconds <= b.Seconds;
    public static bool operator >=(TimeOfDaySec a, TimeOfDaySec b) => a.Seconds >= b.Seconds;

    /// <summary>2つの時刻の差分（秒）。負値も許容する数直線上での単純な減算。</summary>
    public static int operator -(TimeOfDaySec a, TimeOfDaySec b) => a.Seconds - b.Seconds;

    /// <summary>秒数を加算した新しい時刻を返す。</summary>
    public static TimeOfDaySec operator +(TimeOfDaySec a, int deltaSeconds) => new(a.Seconds + deltaSeconds);

    /// <summary>秒数を減算した新しい時刻を返す。</summary>
    public static TimeOfDaySec operator -(TimeOfDaySec a, int deltaSeconds) => new(a.Seconds - deltaSeconds);
}