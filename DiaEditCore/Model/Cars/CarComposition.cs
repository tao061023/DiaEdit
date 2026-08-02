namespace DiaEditCore.Model.Cars;
public sealed class CarComposition
{
    public required CarCompositionId Id { get; set; }
    public required string Name { get; set; }       // 例: "トウ01"
    public required int Identifier { get; set; }      // 車番表記に利用
    public required CarConsistId CarConsistId { get; set; }  // どの型（ひな形）を使うか
}