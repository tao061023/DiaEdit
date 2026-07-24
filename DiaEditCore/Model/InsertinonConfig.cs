namespace DiaEditCore.Model;

public sealed class InsertionConfig
{
    public required InsertionConfigId Id { get; set; }
    public required CarConsistId BaseCarConsistId { get; set; }
    public required int AfterPosition { get; set; }
    public required CarConsistId InsertedCarConsistId { get; set; }
}