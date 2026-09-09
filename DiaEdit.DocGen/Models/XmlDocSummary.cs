// Models/XmlDocSummary.cs
namespace DiaEdit.DocGen.Models;

public sealed record XmlDocSummary(
    string? Summary,
    string? Remarks,
    IReadOnlyDictionary<string, string> Params,
    string? Returns,
    IReadOnlyList<(string ExceptionType, string Description)> Exceptions);