namespace DiaEditCore.Model.Cars;

public sealed class Car
{
    public required CarId Id { get; set; }
    public required string CarType { get; set; } // "クハE234" など。表記のマスタ管理はUI側の責務。
    public int Placeholder { get; set; } = 0;
    public required bool IsPower { get; set; } // 動力車であるか。スタフの〇M〇T表記に利用。
    public required double LengthM { get; set; } // 組成単位ごとの実長
}