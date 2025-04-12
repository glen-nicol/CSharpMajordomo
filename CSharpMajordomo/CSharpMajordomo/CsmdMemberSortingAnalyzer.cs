using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace CSharpMajordomo;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class CsmdMemberSortingAnalyzer : DiagnosticAnalyzer
{
    private static ConcurrentDictionary<string, Comparer<MemberSyntaxReference>> CACHED_MEMBER_SORTING_CONFIG = new();

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray<DiagnosticDescriptor>.Empty.Add(Rules.MemberSorting.Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(
            c => AnalyzeNode(c, c.Options.AnalyzerConfigOptionsProvider), 
            SyntaxKind.ClassDeclaration, 
            SyntaxKind.RecordDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.InterfaceDeclaration, 
            SyntaxKind.EventDeclaration, 
            SyntaxKind.EventFieldDeclaration);
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
