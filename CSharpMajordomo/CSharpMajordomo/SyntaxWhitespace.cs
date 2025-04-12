using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CSharpMajordomo;

public delegate int? GetSpacingForNode(MemberDeclarationSyntax node);

public static class SyntaxWhitespace
{
    public static GetSpacingForNode ParseConfigIntoSpacing(int defaultDefaultSpacing, int maxSpacing, string lineSpacingConfig)
    {
        var typeCountRegex = new Regex(@"\s*\(?\s*\b(?<memberType>\w+):(?<count>[\-\w\d]+)\b\s*\)?\s*");
        var trimmed = lineSpacingConfig.Trim();
        var parts = trimmed.Split(',');

        // parse config for type:count elements
        var nodeTypeMap = parts.Select(p => Parse(p)).Where(t => t.HasValue).Select(t => t!.Value).ToDictionary(t => t.Item1.ToLowerInvariant(), t => ClampNull(t.Item2));

        // get the first number by itself to represent all types not mentioned
        var defaultSpacing = parts.Select(p => int.TryParse(p.Trim(), out var spacing) ? (int?)spacing : null).FirstOrDefault();

        if(ConfiguredForFullDisable())
        {
            return _ => null;
        }
        else
        {
            defaultSpacing ??= Clamp(defaultDefaultSpacing);
        }

        return node =>
        {
            var memberTypeNames = node.MemberTypeNames();

            var configuredTypes = memberTypeNames.Where(nodeType => nodeTypeMap.TryGetValue(nodeType, out var count) && count.HasValue);
            var disabledTypes = memberTypeNames.Where(nodeType => nodeTypeMap.ContainsKey(nodeType));

            // if any names have a valid count use that,
            // else if any are disabled then return its null value
            // finally if not configured by node type return thed default spacing.
            return configuredTypes
                .Concat(disabledTypes)
                .Select(nodeType => nodeTypeMap[nodeType])
                .DefaultIfEmpty(defaultSpacing)
                .First();
        };


        (string, int?)? Parse(string part)
        {
            var match = typeCountRegex.Match(part);
            if (match.Success)
            {
                return (match.Groups["memberType"].Value, int.TryParse(match.Groups["count"].Value, out var count) && count >= 0 ? count : null);
            }

            return null;
        }

        int Clamp(int count) => Math.Min(count, maxSpacing);

        int? ClampNull(int? count) => count is null ? null : Clamp(count.Value);

        bool ConfiguredForFullDisable()
        {
            // if there is no default value and there is an attempt at configuring something (not empty or whitespace) then we will disable all checks
            return !defaultSpacing.HasValue && nodeTypeMap.Count == 0 && trimmed.Length > 0;
        }
    }

    public static int MaxContiguousBlankLines(IEnumerable<SyntaxTrivia> trivia)
    {
        var maxLines = 0;
        var lines = 0;
        var blankCandidate = false;

        // skip until we hit an end of line trivia. Either trailing trivia or leading indentation trivia.
        foreach(var t in trivia.SkipWhile(t => !t.IsKind(SyntaxKind.EndOfLineTrivia)))
        {
            if(t.IsKind(SyntaxKind.EndOfLineTrivia) || t.IsKind(SyntaxKind.EndOfFileToken))
            {
                if(blankCandidate)
                {
                    lines++;
                }

                blankCandidate = true;
                continue;
            }

            if(!t.IsKind(SyntaxKind.WhitespaceTrivia))
            {
                blankCandidate = false;
                if(lines > maxLines)
                {
                    maxLines = lines;
                }

                lines = 0;
            }
        }

        if(lines > maxLines)
        {
            maxLines = lines;
        }

        return maxLines;
    }

    public static int MaxContiguousBlankLines(InterNodeTextSpan interText, CancellationToken cancel)
    {
        // detect empty string and it does not count as a blank line
        var (text, lineOfPreviousEnd, lineOfCurrentStart) = (interText.Text, interText.LineOfFirstEnd, interText.LineOfSecondStart);
        if (lineOfCurrentStart - lineOfPreviousEnd <= 1)
        {
            return 0;
        }

        var lines = text.Lines;

        var current = 0;
        var max = 0;

        for (var i = lineOfPreviousEnd + 1; i < lineOfCurrentStart; i++)
        {
            if (IsBlankOrWhitespace(lines[i]))
            {
                current++;
                max = Math.Max(max, current);
            }
            else
            {
                current = 0;
            }
        }

        return max;
    }

    public static InterNodeTextSpan CreateInterNodeTextSpan(SyntaxNode first, SyntaxNode second, CancellationToken cancel)
    {
        var text = first.SyntaxTree.GetText(cancel);
        var previouslineSpan = first.SyntaxTree.GetLineSpan(first.Span, cancel);
        var currentlineSpan = second.SyntaxTree.GetLineSpan(second.Span, cancel);
        var lineOfPreviousEnd = previouslineSpan.Span.End.Line;
        var lineOfCurrentStart = currentlineSpan.Span.Start.Line;

        return new(text, first.Span.End, lineOfPreviousEnd, lineOfCurrentStart);
    }

    public static async ValueTask<InterNodeTextSpan> CreateInterNodeTextSpanAsync(SyntaxNode first, SyntaxNode second, CancellationToken cancel)
    {
        var text = await first.SyntaxTree.GetTextAsync(cancel);
        var previouslineSpan = first.SyntaxTree.GetLineSpan(first.Span, cancel);
        var currentlineSpan = second.SyntaxTree.GetLineSpan(second.Span, cancel);
        var lineOfPreviousEnd = previouslineSpan.Span.End.Line;
        var lineOfCurrentStart = currentlineSpan.Span.Start.Line;

        return new(text, first.Span.End, lineOfPreviousEnd, lineOfCurrentStart);
    }

    public static int MaxContiguousBlankLines(SyntaxNode first, SyntaxNode second, CancellationToken cancel)
    {
        return MaxContiguousBlankLines(CreateInterNodeTextSpan(first, second, cancel), cancel);
    }

    public static bool IsBlankOrWhitespace(TextLine text)
    {
        if (text.Text is null)
        {
            return true;
        }

        for (var i = text.Start; i < text.End; i++)
        {
            if (!char.IsWhiteSpace(text.Text[i]))
            {
                return false;
            }
        }

        return true;
    }
}
