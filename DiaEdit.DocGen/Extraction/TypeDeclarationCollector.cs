namespace DiaEdit.DocGen.Extraction;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DiaEdit.DocGen.Models;

public static class TypeDeclarationCollector
{
    public static IReadOnlyList<RawTypeDeclaration> Collect(CollectedSourceFile file)
    {
        var text = File.ReadAllText(file.AbsolutePath);
        var tree = CSharpSyntaxTree.ParseText(text, path: file.AbsolutePath);
        var root = tree.GetCompilationUnitRoot();

        var results = new List<RawTypeDeclaration>();
        CollectRecursive(root, currentNamespace: "", containingType: null, file, results);
        return results;
    }

    private static void CollectRecursive(
        SyntaxNode node, string currentNamespace, string? containingType,
        CollectedSourceFile file, List<RawTypeDeclaration> results)
    {
        foreach (var child in node.ChildNodes())
        {
            switch (child)
            {
                case FileScopedNamespaceDeclarationSyntax fileScoped:
                    CollectRecursive(fileScoped, fileScoped.Name.ToString(), containingType, file, results);
                    break;

                case NamespaceDeclarationSyntax ns:
                    CollectRecursive(ns, ns.Name.ToString(), containingType, file, results);
                    break;

                case EnumDeclarationSyntax enumDecl:
                    results.Add(new RawTypeDeclaration(
                        AbsoluteFilePath: file.AbsolutePath,
                        RelativePathFromProjectRoot: file.RelativePathFromProjectRoot,
                        Namespace: currentNamespace,
                        TypeName: enumDecl.Identifier.ToString(),
                        Syntax: null,
                        EnumSyntax: enumDecl,
                        ContainingTypeName: containingType));
                    break;

                case TypeDeclarationSyntax typeDecl: // class/record/struct/interface共通基底
                    var name = typeDecl.Identifier.ToString();
                    results.Add(new RawTypeDeclaration(
                        AbsoluteFilePath: file.AbsolutePath,
                        RelativePathFromProjectRoot: file.RelativePathFromProjectRoot,
                        Namespace: currentNamespace,
                        TypeName: name,
                        Syntax: typeDecl,
                        EnumSyntax: null,
                        ContainingTypeName: containingType));

                    CollectRecursive(typeDecl, currentNamespace, name, file, results);
                    break;
            }
        }
    }
}