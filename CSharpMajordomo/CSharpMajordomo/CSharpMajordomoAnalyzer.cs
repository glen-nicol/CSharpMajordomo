using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace CSharpMajordomo;

public static class Rules
{
    public static class MemberSorting
    {
        public const string DiagnosticId = "CSMD0001";
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Style";

        public const string SORT_ORDERING_CONFIG_KEY = "CSharpMajordomo.member_sort_order";

        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);
    }

    public static class InterGroupSpacing
    {
        public const string DiagnosticId = "CSMD0002";
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.InterGroupSpacingTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.InterGroupSpacingMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.InterGroupSpacingDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Style";

        public const string SPACING_COUNT_CONFIG_KEY = "CSharpMajordomo.blank_lines_between_member_groups";

        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);
    }

    public static class IntraGroupSpacing
    {
        public const string DiagnosticId = "CSMD0003";
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.IntraGroupSpacingTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.IntraGroupSpacingMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.IntraGroupSpacingDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Style";

        public const string SPACING_COUNT_CONFIG_KEY = "CSharpMajordomo.blank_lines_between_members";

        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);
    }

    public static IEnumerable<DiagnosticDescriptor> Diagnostics
    {
        get
        {
            yield return MemberSorting.Rule;
            yield return InterGroupSpacing.Rule;
            yield return IntraGroupSpacing.Rule;
        }
    }

    public static IEnumerable<string> RuleIds => Diagnostics.Select(d => d.Id);
}

public static class SpacingDiagnosticHelper
{
    public const string SIMPLIFIED_SPACING_PROPERTY = "blank_lines";
}

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class CSharpMajordomoAnalyzer : DiagnosticAnalyzer
{
    private static ConcurrentDictionary<string, Comparer<MemberSyntaxReference>> CACHED_MEMBER_SORTING_CONFIG = new();
    private static ConcurrentDictionary<string, GetSpacingForNode> CACHED_MEMBER_SPACING_CONFIG = new();

    // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
    // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Localizing%20Analyzers.md for more on localization

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = Rules.Diagnostics.ToImmutableArray();

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        //context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(
            c => AnalyzeNode(c, c.Options.AnalyzerConfigOptionsProvider), 
            SyntaxKind.ClassDeclaration, 
            SyntaxKind.RecordDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.InterfaceDeclaration, 
            SyntaxKind.EventDeclaration, 
            SyntaxKind.EventFieldDeclaration);

        context.RegisterSyntaxNodeAction(
            AnalyzeNodeWhitespace,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.InterfaceDeclaration,
            SyntaxKind.EventDeclaration,
            SyntaxKind.EventFieldDeclaration,
            SyntaxKind.FieldDeclaration,
            SyntaxKind.PropertyDeclaration,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.ConstructorDeclaration,
            SyntaxKind.DestructorDeclaration,
            SyntaxKind.DelegateDeclaration,
            SyntaxKind.EnumDeclaration,
            SyntaxKind.IndexerDeclaration,
            SyntaxKind.OperatorDeclaration,
            SyntaxKind.ConversionOperatorDeclaration);

    }

    private void AnalyzeNodeWhitespace(SyntaxNodeAnalysisContext context)
    {
        if(context.Node is not MemberDeclarationSyntax memberNode)
        {
            return;
        }

        //Debugger.Launch();

        var previous = context.Node.PreviousSibling();
        if(previous is null)
        {
            // same kind no need to adjust whitespace
            return;
        }

        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);

        var ruleToApply = 
            previous.IsKind(memberNode.Kind())
            ? GetWithinGroupSpacing()
            : GetBetweenGroupSpacing();

        // no configured spacing means it was explicitely configured to not be adjusted
        if(ruleToApply.ConfiguredSpacing is null)
        {
            return;
        }

        var blanks = SyntaxWhitespace.MaxContiguousBlankLines(previous, context.Node, context.CancellationToken);

        if(blanks != ruleToApply.ConfiguredSpacing)
        {
            var diagnosticProperties = 
                ImmutableDictionary<string, string?>.Empty
                .Add(SpacingDiagnosticHelper.SIMPLIFIED_SPACING_PROPERTY, ruleToApply.ConfiguredSpacing.Value.ToString(CultureInfo.InvariantCulture));

            context.ReportDiagnostic(
                Diagnostic.Create(
                    ruleToApply.Rule, 
                    context.Node.IdentifierLocation() ?? context.Node.GetLocation(),
                    diagnosticProperties, 
                    ruleToApply.ConfiguredSpacing, 
                    memberNode.MemberTypeNames().FirstOrDefault() ?? memberNode.Kind().ToString()));
        }

        ApplicableSpacingRule GetWithinGroupSpacing()
        {
            if (!options.TryGetValue(Rules.IntraGroupSpacing.SPACING_COUNT_CONFIG_KEY, out var intraNodeSpacingConfig))
            {
                intraNodeSpacingConfig = string.Empty;
            }

            var getIntraNodeSpacing =
               CACHED_MEMBER_SPACING_CONFIG.GetOrAdd(
                   intraNodeSpacingConfig,
                   str => SyntaxWhitespace.ParseConfigIntoSpacing(defaultDefaultSpacing: 1, maxSpacing: 4, str));

            return new(Rules.IntraGroupSpacing.Rule, getIntraNodeSpacing(memberNode));
        }

        ApplicableSpacingRule GetBetweenGroupSpacing()
        {
            if (!options.TryGetValue(Rules.InterGroupSpacing.SPACING_COUNT_CONFIG_KEY, out var interNodeSpacingConfig))
            {
                interNodeSpacingConfig = string.Empty;
            }

            var getInterNodeSpacing =
                CACHED_MEMBER_SPACING_CONFIG.GetOrAdd(
                    interNodeSpacingConfig,
                    str => SyntaxWhitespace.ParseConfigIntoSpacing(defaultDefaultSpacing: 1, maxSpacing: 4, str));

            return new(Rules.InterGroupSpacing.Rule, getInterNodeSpacing(memberNode));
        }
    }

    private sealed class ApplicableSpacingRule(DiagnosticDescriptor rule, int? spacing)
    {
        public DiagnosticDescriptor Rule { get; } = rule;

        public int? ConfiguredSpacing { get; } = spacing;
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context, AnalyzerConfigOptionsProvider optionsProvider)
    {
        if(context.Node is not TypeDeclarationSyntax typeDecl)
        {
            return;
        }

        var d = CheckForSortedMembers(typeDecl, optionsProvider);
        if(d is not null)
        {
            context.ReportDiagnostic(d);
        }
    }


    private static Diagnostic? CheckForSortedMembers(TypeDeclarationSyntax typeNode, AnalyzerConfigOptionsProvider optionsProvider)
    {
        if(typeNode.Members.Count == 0)
        {
            return null;
        }

        var options = optionsProvider.GetOptions(typeNode.SyntaxTree);
        if(!options.TryGetValue(Rules.MemberSorting.SORT_ORDERING_CONFIG_KEY, out var orderConfig))
        {
            // if not configured, nothing to do.
            return null;
        }

        var sorter =
            CACHED_MEMBER_SORTING_CONFIG.GetOrAdd(
                orderConfig,
                orderConfig => SyntaxSorters.ParseTokenPriority(orderConfig).ToComparer());

        //var model = context.Compilation.GetSemanticModel(l.SourceTree);
        var currentMembers = typeNode.Members.Select((m, i) => new MemberSyntaxReference(m, i)).ToImmutableArray();

        var sortedSymbols = currentMembers.Sort(sorter);

        if(sortedSymbols.SequenceEqual(currentMembers))
        {
            // members are sorted correctly
            return null;
        }

        var diagnosticProperties = ImmutableDictionary<string, string?>.Empty.Add(Rules.MemberSorting.SORT_ORDERING_CONFIG_KEY, orderConfig);

        return Diagnostic.Create(Rules.MemberSorting.Rule, typeNode.IdentifierLocation() ?? typeNode.GetLocation(), diagnosticProperties);
    }
}

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

public readonly struct InterNodeTextSpan(SourceText text, int firstNodeSpanEnd, int firstEndLine, int secondStartLine)
{
    public SourceText Text { get; } = text;

    public int FirstNodeSpanEnd { get; } = firstNodeSpanEnd;

    public int LineOfFirstEnd { get; } = firstEndLine;

    public int LineOfSecondStart { get; } = secondStartLine;

    public SourceText EditToHaveBlankLines(int targetLines, string endOfLine, CancellationToken cancel)
    {
        var maxLines = MaxContiguousBlankLines(cancel);
        var lines = Text.Lines;
        if(maxLines.ContiguousBlankLineCount > targetLines)
        {
            return Text.WithChanges(Enumerable.Range(maxLines.FirstLineIndex, maxLines.ContiguousBlankLineCount - targetLines).Select(i => new TextChange(lines[i].SpanIncludingLineBreak, string.Empty)));
        }

        if(maxLines.ContiguousBlankLineCount < targetLines)
        {
            return Text.WithChanges(new TextChange(new TextSpan(maxLines.InsertionStart, 0), string.Concat(Enumerable.Repeat(endOfLine, targetLines - maxLines.ContiguousBlankLineCount))));
        }

        return Text;

    }

    public MaxBlankLinesInfo MaxContiguousBlankLines(CancellationToken cancel)
    {
        // detect empty string and it does not count as a blank line
        var (text, lineOfPreviousEnd, lineOfCurrentStart) = (Text, LineOfFirstEnd, LineOfSecondStart);
        var lines = text.Lines;
        var maxStartSpanStart = 
            lineOfCurrentStart == lineOfPreviousEnd 
            ? FirstNodeSpanEnd 
            : lines[lineOfCurrentStart].Start;
        if (lineOfCurrentStart - lineOfPreviousEnd <= 1)
        {
            // -1 is used here to support adding 2 lines at once when two nodes are on the same line and need 2 lines to make 1 blank between them.
            return new(maxStartSpanStart, lineOfPreviousEnd, lineOfCurrentStart - lineOfPreviousEnd - 1);
        }


        var current = 0;
        var max = 0;
        var maxStartLine = lineOfPreviousEnd + 1;

        for (var i = lineOfPreviousEnd + 1; i < lineOfCurrentStart; i++)
        {
            cancel.ThrowIfCancellationRequested();
            if (SyntaxWhitespace.IsBlankOrWhitespace(lines[i]))
            {
                current++;
                if(current > max)
                {
                    maxStartLine = i;
                    max = current;
                }
            }
            else
            {
                current = 0;
            }
        }

        return new(maxStartSpanStart, maxStartLine, max);
    }
}

public readonly struct MaxBlankLinesInfo(int insertionStart, int lineIndex, int count)
{
    public int InsertionStart { get; } = insertionStart;

    public int FirstLineIndex { get; } = lineIndex;

    public int ContiguousBlankLineCount { get; } = count;


}