namespace DiaEditCore.Model;

/// <summary>
/// 時刻の内部表現。当日0:00を基準とした経過秒数（int）として保持する。
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