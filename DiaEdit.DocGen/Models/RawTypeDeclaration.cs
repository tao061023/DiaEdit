namespace DiaEdit.DocGen.Models;

using Microsoft.CodeAnalysis.CSharp.Syntax;

public sealed record RawTypeDeclaration(
    string AbsoluteFilePath,
    string RelativePathFromProjectRoot,
    string Namespace,
    string TypeName,
    TypeDeclarationSyntax? Syntax,      // enumはnull、class/record/structは値あり
    EnumDeclarationSyntax? EnumSyntax,  // enum専用
    string? ContainingTypeName);