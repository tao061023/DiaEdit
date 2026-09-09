// CliOptions.cs
namespace DiaEdit.DocGen;

public sealed record CliOptions(
    string ProjectName,
    string SrcDir,
    string DocPath,
    string ManifestPath)
{
    public static CliOptions Parse(string[] args)
    {
        string? projectName = null, srcDir = null, docPath = null, manifestPath = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--project": projectName = args[++i]; break;
                case "--src": srcDir = args[++i]; break;
                case "--doc": docPath = args[++i]; break;
                case "--manifest": manifestPath = args[++i]; break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[i]}");
            }
        }

        if (projectName is null || srcDir is null || docPath is null || manifestPath is null)
            throw new ArgumentException(
                "Usage: --project <name> --src <dir> --doc <path> --manifest <path>");

        return new CliOptions(projectName, srcDir, docPath, manifestPath);
    }
}