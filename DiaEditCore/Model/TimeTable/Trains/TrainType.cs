namespace DiaEditCore.Model.TimeTable.Trains;

public enum LineStyle { Solid, Dashed, Dotted } // 具体的な値は7章UI実装時に確定

public sealed class TrainType
{
    public required TrainTypeId Id { get; set; }
    public required DisplayName Name { get; set; }
    public required string DiagramColor { get; set; } // QColor相当。C#側では#RRGGBB文字列として保持
    public required LineStyle DiagramLineStyle { get; set; }
    public required int SortOrder { get; set; }
}