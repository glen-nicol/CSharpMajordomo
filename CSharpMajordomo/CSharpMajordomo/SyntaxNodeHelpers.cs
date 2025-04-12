using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

public static class SyntaxNodeHelpers
{
    public static string IdentifierName(this SyntaxNode node)
    {
        return node switch
        {
            DelegateDeclarationSyntax d => UnnamedHandler(d.Identifier.Text, "delegate"),
            FieldDeclarationSyntax f => UnnamedHandler(IdentifierName(f.Declaration), "field"),
            PropertyDeclarationSyntax p => UnnamedHandler(p.Identifier.Text, "property"),
            ConstructorDeclarationSyntax c => "constructor",
            DestructorDeclarationSyntax d => "destructor",
            MethodDeclarationSyntax m => UnnamedHandler(m.Identifier.Text, "method"),
            IndexerDeclarationSyntax i => "indexer", // could include parameters
            OperatorDeclarationSyntax o => UnnamedHandler(o.NextTokenAfterOperatorKeyword()?.ToString() ?? o.FirstIdentifierChild()?.Identifier.Text, "operator"),
            ConversionOperatorDeclarationSyntax c => UnnamedHandler(c.NextTokenAfterOperatorKeyword()?.ToString() ?? c.FirstIdentifierChild()?.Identifier.Text, "conversion"),
            EventFieldDeclarationSyntax e => UnnamedHandler(IdentifierName(e.Declaration), "event"),
            EventDeclarationSyntax e => UnnamedHandler(e.Identifier.Text, "event"),
            EnumDeclarationSyntax e => UnnamedHandler(e.Identifier.Text, "enum"),
            InterfaceDeclarationSyntax i => UnnamedHandler(i.Identifier.Text, "interface"),
            ClassDeclarationSyntax c => UnnamedHandler(c.Identifier.Text, "class"),
            RecordDeclarationSyntax r => UnnamedHandler(r.Identifier.Text, "record"),
            StructDeclarationSyntax s => UnnamedHandler(s.Identifier.Text, "struct"),
            VariableDeclarationSyntax v => v.Variables.FirstOrDefault(v => v.Identifier.Span.Length > 0)?.Identifier.Text ?? string.Empty,
            _ => $"Unknown declaration on line: {node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}",
        };

        string UnnamedHandler(string? id, string typeName)
        {
            return id?.Length > 0
                ? id
                : $"{typeName} on line: {node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}";
        }
    }

    public static Location? IdentifierLocation(this SyntaxNode? node)
    {
        return node switch
        {
            DelegateDeclarationSyntax d => d.Identifier.GetLocation(),
            FieldDeclarationSyntax f => IdentifierLocation(f.Declaration),
            PropertyDeclarationSyntax p => p.Identifier.GetLocation(),
            ConstructorDeclarationSyntax c => c.Identifier.GetLocation(),
            DestructorDeclarationSyntax d => d.Identifier.GetLocation(),
            MethodDeclarationSyntax m => m.Identifier.GetLocation(),
            IndexerDeclarationSyntax i => i.ThisKeyword.GetLocation(),
            OperatorDeclarationSyntax o => o.NextTokenAfterOperatorKeyword()?.GetLocation() ?? IdentifierLocation(o.FirstIdentifierChild()) ?? o.OperatorKeyword.GetLocation(),
            ConversionOperatorDeclarationSyntax c => c.NextTokenAfterOperatorKeyword()?.GetLocation() ?? IdentifierLocation(c.FirstIdentifierChild()) ?? c.OperatorKeyword.GetLocation(),
            EventFieldDeclarationSyntax e => e.Declaration.GetLocation(),
            EventDeclarationSyntax e => e.Identifier.GetLocation(),
            EnumDeclarationSyntax e => e.Identifier.GetLocation(),
            InterfaceDeclarationSyntax i => i.Identifier.GetLocation(),
            ClassDeclarationSyntax c => c.Identifier.GetLocation(),
            RecordDeclarationSyntax r => r.Identifier.GetLocation(),
            StructDeclarationSyntax s => s.Identifier.GetLocation(),
            VariableDeclarationSyntax v => v.Variables.Select(v => v.Identifier.GetLocation()).FirstOrDefault(),
            IdentifierNameSyntax i => i.Identifier.GetLocation(),
            _ => null,
        };
    }

    public static IdentifierNameSyntax? FirstIdentifierChild(this SyntaxNode node)
    {
        return node.ChildNodes().OfType<IdentifierNameSyntax>().FirstOrDefault();
    }

    public static SyntaxNodeOrToken? NextTokenAfterOperatorKeyword(this SyntaxNode node)
    {
        return node.ChildNodesAndTokens().SkipWhile(s => !s.IsToken || !s.AsToken().IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.OperatorKeyword)).Skip(1).FirstOrDefault();
    }

    public static SyntaxNode? PreviousSibling(this SyntaxNode node)
    {
        if(node.FullSpan.Start <= 0)
        {
            return null;
        }

        var previous = node.Parent?.FindNode(new(node.FullSpan.Start - 1, 1));
        var n = previous;
        while(n is not null && n.Parent != node.Parent)
        {
            n = n.Parent;
        }

        return n;
    }
}
