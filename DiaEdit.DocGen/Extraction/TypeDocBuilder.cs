namespace DiaEdit.DocGen.Extraction;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DiaEdit.DocGen.Models;

public static class TypeDocBuilder
{
    public static TypeDocModel Build(MergedTypeDeclaration type)
    {
        if (type.EnumSyntax is not null)
        {
            var enumDoc = XmlDocParser.Parse(type.EnumSyntax);
            var enumMembers = type.EnumSyntax.Members
                .Select(m => new EnumMemberDocModel(
                    m.Identifier.ToString(),
                    XmlDocParser.Parse(m).Summary))
                .ToList();

            return new TypeDocModel(
                Namespace: type.Namespace,
                TypeName: type.TypeName,
                Kind: "enum",
                Summary: enumDoc.Summary,
                Remarks: enumDoc.Remarks,
                RecordParameters: Array.Empty<(string, string)>(),
                Properties: Array.Empty<PropertyDocModel>(),
                Members: Array.Empty<MemberDocModel>(),
                EnumMembers: enumMembers);
        }

        var primarySyntax = type.AllSyntaxes.FirstOrDefault();
        var kind = primarySyntax switch
        {
            RecordDeclarationSyntax r when r.ClassOrStructKeyword.Text == "struct" => "record struct",
            RecordDeclarationSyntax => "record",
            InterfaceDeclarationSyntax => "interface",
            StructDeclarationSyntax => "struct",
            _ => "class"
        };

        var typeDoc = type.AllSyntaxes
            .Select(XmlDocParser.Parse)
            .FirstOrDefault(d => d.Summary is not null)
            ?? new XmlDocSummary(null, null, new Dictionary<string, string>(), null, Array.Empty<(string, string)>());

        var recordParams = type.AllSyntaxes
            .OfType<RecordDeclarationSyntax>()
            .Select(r => r.ParameterList)
            .FirstOrDefault(p => p is not null);
        var recordParameters = SignatureFormatter.FormatRecordParameters(recordParams);

        var properties = new List<PropertyDocModel>();
        var members = new List<MemberDocModel>();

        foreach (var syntax in type.AllSyntaxes)
        {
            foreach (var member in syntax.Members)
            {
                if (!MemberFilter.ShouldInclude(member)) continue;

                var doc = XmlDocParser.Parse(member);

                switch (member)
                {
                    case PropertyDeclarationSyntax p:
                        properties.Add(new PropertyDocModel(
                            Type: p.Type.ToString(),
                            Name: p.Identifier.ToString(),
                            DefaultValue: p.Initializer?.Value.ToString(),
                            Summary: doc.Summary));
                        break;

                    case MethodDeclarationSyntax m:
                        members.Add(new MemberDocModel("method", SignatureFormatter.FormatMethod(m), doc));
                        break;

                    case ConstructorDeclarationSyntax c:
                        members.Add(new MemberDocModel("constructor", SignatureFormatter.FormatConstructor(c), doc));
                        break;

                    // フィールド宣言（プロパティでなく `public int Foo;` 形式）が将来出てきた場合はここに追加
                }
            }
        }

        return new TypeDocModel(
            Namespace: type.Namespace,
            TypeName: type.TypeName,
            Kind: kind,
            Summary: typeDoc.Summary,
            Remarks: typeDoc.Remarks,
            RecordParameters: recordParameters,
            Properties: properties,
            Members: members,
            EnumMembers: Array.Empty<EnumMemberDocModel>());
    }
}