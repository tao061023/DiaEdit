namespace DiaEdit.DocGen.Models;

using Microsoft.CodeAnalysis.CSharp.Syntax;

public sealed record MergedTypeDeclaration(
    string Namespace,
    string TypeName,
    string? ContainingTypeName,
    string PrimaryFilePath,
    string PrimaryRelativePath,             // ← Program.csのGetFolderKeyで使用
    IReadOnlyList<TypeDeclarationSyntax> AllSyntaxes,
    EnumDeclarationSyntax? EnumSyntax);      // enumの場合はこちらのみ非null、AllSyntaxesは空