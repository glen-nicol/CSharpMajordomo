using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

public static class SyntaxSorters
{
    public static Comparison<MemberSyntaxReference> DocumentOrder { get; } = (l, r) => l.DocumentIndex.CompareTo(r.DocumentIndex);

    public static Comparison<MemberSyntaxReference> CreateIndexOfComparison(string searchFor)
        => (lhs, rhs) =>
        {
            return ComparisonIndex(lhs).CompareTo(ComparisonIndex(rhs));

            int ComparisonIndex(MemberSyntaxReference memberSyntaxReference)
            {
                var v = memberSyntaxReference.SortableTokens.BinarySearch(searchFor);

                // sort is ASC which means first items need a lower value. 
                return v >= 0 ? 1 : 2;
            }
        };

    public static Comparison<T> ThenBy<T>(this Comparison<T> first, Comparison<T> second)
        => (lhs, rhs) =>
        {
            var v1 = first(lhs, rhs);
            if(v1 == 0)
            {
                return second(lhs, rhs);
            }

            return v1;
        };

    public static Comparer<T> ToComparer<T>(this Comparison<T> comparison) => Comparer<T>.Create(comparison);

    public static Comparison<MemberSyntaxReference> ParseTokenPriority(string configuration)
    {
        var parts = configuration.Split(new[] { ",", ";", " " }, StringSplitOptions.RemoveEmptyEntries);
        var sorter = DocumentOrder;

        // build the sorter from least important out by going in reverse direction
        for(var i = parts.Length-1; i >= 0; i--)
        {
            sorter = CreateIndexOfComparison(parts[i]).ThenBy(sorter);
        }

        return sorter;
    }

    public static IEnumerable<string> ConvertToSearchableTokens(this MemberDeclarationSyntax syntaxNode)
    {
        foreach(var m in syntaxNode.Modifiers)
        {
            yield return m.Text;
        }

        var kindString =
            syntaxNode.Kind() switch
            {
                SyntaxKind.ClassKeyword
                or SyntaxKind.ClassDeclaration => "class",
                SyntaxKind.StructKeyword
                or SyntaxKind.StructDeclaration => "struct",
                SyntaxKind.EnumKeyword
                or SyntaxKind.EnumDeclaration => "enum",
                SyntaxKind.DelegateKeyword
                or SyntaxKind.DelegateDeclaration => "delegate",
                SyntaxKind.InterfaceKeyword
                or SyntaxKind.InterfaceDeclaration => "interface",
                SyntaxKind.FieldKeyword 
                or SyntaxKind.FieldDeclaration => "field",
                SyntaxKind.PropertyKeyword
                or SyntaxKind.PropertyDeclaration => "property",
                SyntaxKind.MethodKeyword
                or SyntaxKind.MethodDeclaration => "method",
                SyntaxKind.ConstructorDeclaration => "constructor",
                SyntaxKind.DestructorDeclaration => "destructor",
                SyntaxKind.EventKeyword
                or SyntaxKind.EventDeclaration
                or SyntaxKind.EventFieldDeclaration => "event",
                _ => null,
            };

        if(kindString is not null)
        {
            yield return kindString;
        }
    }
}
