using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;

public record MemberSyntaxReference
{
    public MemberDeclarationSyntax Member { get; }

    public int DocumentIndex { get;  }

    public ImmutableArray<string> SortableTokens { get; }

    public string Identifier { get; }

    public MemberSyntaxReference(MemberDeclarationSyntax member, int documentIndex)
    {
        Member = member;
        DocumentIndex = documentIndex;
        SortableTokens = member.ConvertToSearchableTokens().OrderBy(s => s).ToImmutableArray();
        var id = string.Empty;
        Identifier = member.IdentifierName();
    }
}
