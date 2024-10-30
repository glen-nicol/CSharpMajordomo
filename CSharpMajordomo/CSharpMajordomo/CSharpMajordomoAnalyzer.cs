using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace CSharpMajordomo
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CSharpMajordomoAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "CSharpMajordomo";
        private static ConcurrentDictionary<string, Comparer<MemberSyntaxReference>> CACHED_CONFIG = new();

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Style";

        public const string SORT_ORDERING_CONFIG_KEY = "CSharpMajordomo.sort_order";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(
                c => AnalyzeNode(c, c.Options.AnalyzerConfigOptionsProvider), 
                SyntaxKind.ClassDeclaration, 
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
            if(!options.TryGetValue(SORT_ORDERING_CONFIG_KEY, out var orderConfig))
            {
                // if not configured, nothing to do.
                return null;
            }

            var sorter =
                CACHED_CONFIG.GetOrAdd(
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

            var indexMap = sortedSymbols.Select((m, i) => (m, i)).ToDictionary(t => t.m, t => t.i);

            var diffGroups =
                currentMembers.Select((m, i) =>
                {
                    var sortedIndex = indexMap[m];
                    return (m, diff: Math.Abs(i - sortedIndex));
                })
                .OrderByDescending(t => t.diff)
                .ToList()
                .GroupBy(t => t.diff)
                .ToList();

            // NOTE: this logic isn't awesome or 100% accurate
            var outOfPlace = diffGroups.Take(diffGroups.Count - 1).SelectMany(g => g.Select(t => t.m));

            var diagnosticProperties = ImmutableDictionary<string, string>.Empty.Add(SORT_ORDERING_CONFIG_KEY, orderConfig);

            var expectedOrderString = string.Join(", ", outOfPlace.Select(s => s.Identifier));
            return Diagnostic.Create(Rule, typeNode.IdentifierLocation() ?? typeNode.GetLocation(), diagnosticProperties, expectedOrderString);
        }
    }
}
