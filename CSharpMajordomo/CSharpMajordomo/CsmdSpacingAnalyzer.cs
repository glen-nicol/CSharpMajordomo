using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

namespace CSharpMajordomo;

public static class SpacingDiagnosticHelper
{
    public const string SIMPLIFIED_SPACING_PROPERTY = "blank_lines";
}

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class CsmdSpacingAnalyzer : DiagnosticAnalyzer
{
    private static ConcurrentDictionary<string, GetSpacingForNode> CACHED_MEMBER_SPACING_CONFIG = new();

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = Rules.SpacingDiagnostics.ToImmutableArray();

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        //context.EnableConcurrentExecution();

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
        if (context.Node is not MemberDeclarationSyntax memberNode)
        {
            return;
        }

        //Debugger.Launch();

        var previous = context.Node.PreviousSibling();
        if (previous is null)
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
        if (ruleToApply.ConfiguredSpacing is null)
        {
            return;
        }

        var blanks = SyntaxWhitespace.MaxContiguousBlankLines(previous, context.Node, context.CancellationToken);

        if (blanks != ruleToApply.ConfiguredSpacing)
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
}
