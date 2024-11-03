using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

public static class SyntaxSorters
{
    public static readonly Regex KEYWORD_PATTERN = new("^a-z+$");

    public static Comparison<MemberSyntaxReference> DocumentOrder { get; } = (l, r) => l.DocumentIndex.CompareTo(r.DocumentIndex);

    public static Comparison<MemberSyntaxReference> Identifier { get; } = (l, r) => l.Identifier.CompareTo(r.Identifier);

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

    public static Comparison<T> Invert<T>(this Comparison<T> comparison)
    => (lhs, rhs) =>
    {
        return comparison(rhs, lhs);
    };

    public static Comparer<T> ToComparer<T>(this Comparison<T> comparison) => Comparer<T>.Create(comparison);

    public static Comparison<MemberSyntaxReference> ParseTokenPriority(string configuration)
    {
        var comparisonLayers = 
            configuration.Split(new[] { ",", ";", " " }, StringSplitOptions.RemoveEmptyEntries)
            .Select(sortField =>
            {
                var res = new char[sortField.Length];

                var length = sortField.AsSpan().Trim().ToLowerInvariant(res);
                var result = res.AsSpan().Slice(0, length);

                var inverted = false;
                if(result.Length == 0)
                {
                    return (sortField, null!);
                }

                if(result[0] == '-')
                {
                    inverted = true;
                    result = result.Slice(1);
                }

                // netstandard regex doesn't support span. If we need more than just lower case ascii a regex is worth the allocation.
                for(int i = 0; i < result.Length; i++)
                {
                    if(result[i] < 'a' || result[i] > 'z')
                    {
                        return (sortField, null!);
                    }
                }

                var comparison =
                    result switch
                    {
                        "identifier" => Identifier,
                        var o => CreateIndexOfComparison(o.ToString()),
                    };

                return (sortField, sorter: inverted ? comparison.Invert() : comparison);
            })
            .Where(t => t.sorter is not null);

        // build the sorter from least important out by going in reverse direction
        var sorter = DocumentOrder;
        foreach(var t in new Stack<(string sortField, Comparison<MemberSyntaxReference> sorter)>(comparisonLayers))
        {
            sorter = t.sorter.ThenBy(sorter);
        }

        return sorter;
    }

    public static IEnumerable<string> ConvertToSearchableTokens(this MemberDeclarationSyntax syntaxNode)
    {
        IEnumerable<string> kindStrings =
            syntaxNode.Kind() switch
            {
                SyntaxKind.ClassDeclaration => ["class"],
                SyntaxKind.RecordDeclaration => ["class", "record"],
                SyntaxKind.RecordStructDeclaration => ["struct", "record"],
                SyntaxKind.StructDeclaration => ["struct"],
                SyntaxKind.EnumDeclaration => ["enum"],
                SyntaxKind.DelegateDeclaration => ["delegate"],
                SyntaxKind.InterfaceDeclaration => ["interface"],
                SyntaxKind.FieldDeclaration => ["field"],
                SyntaxKind.PropertyDeclaration => ["property"],
                SyntaxKind.MethodDeclaration => ["method"],
                SyntaxKind.ConstructorDeclaration => ["constructor"],
                SyntaxKind.DestructorDeclaration => ["destructor"],
                SyntaxKind.EventDeclaration
                or SyntaxKind.EventFieldDeclaration => ["event"],
                _ => [],
            };

        return syntaxNode.Modifiers.Select(t => t.Text).Concat(kindStrings);
    }
}
