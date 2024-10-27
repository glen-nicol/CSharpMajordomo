using Microsoft.CodeAnalysis;
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
        private static ConcurrentDictionary<string, List<Accessibility>> CACHED_CONFIG = new();

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Style";

        private const string ACCESSIBILITY_ORDERING_CONFIG_KEY = "CSharpMajordomo.accessibility_ordering";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSymbolAction(c => AnalyzeSymbolAccessibility(c, c.Options.AnalyzerConfigOptionsProvider), SymbolKind.Field, SymbolKind.Property, SymbolKind.Method, SymbolKind.Event, SymbolKind.NamedType);
        }

        private static void TemplateExample(SymbolAnalysisContext context)
        {
            // TODO: Replace the following code with your own analysis, generating Diagnostic objects for any issues you find
            var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

            // Find just those named type symbols with names containing lowercase letters.
            if(namedTypeSymbol.Name.ToCharArray().Any(char.IsLower))
            {
                // For all such symbols, produce a diagnostic.
                var diagnostic = Diagnostic.Create(Rule, namedTypeSymbol.Locations[0], namedTypeSymbol.Name, namedTypeSymbol.ContainingType);

                context.ReportDiagnostic(diagnostic);
            }
        }

        private static void AnalyzeSymbolAccessibility(SymbolAnalysisContext context, AnalyzerConfigOptionsProvider optionsProvider)
        {
            if(context.Symbol.ContainingType is null)
            {
                // only concerned about members of another type. NOTE: although could arrange multiple types in a file too
                return;
            }

            foreach(var l in context.Symbol.Locations)
            {
                var options = optionsProvider.GetOptions(l.SourceTree);
                if(!options.TryGetValue(ACCESSIBILITY_ORDERING_CONFIG_KEY, out var orderConfig))
                {
                    // if not configured, nothing to do.
                    continue;
                }

                var accessibilities =
                    CACHED_CONFIG.GetOrAdd(
                        orderConfig,
                        orderConfig =>
                            orderConfig.Split(new[] { ",", ";", " " }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => Enum.TryParse<Accessibility>(s, ignoreCase: true, out var a) ? (Accessibility?)a : null)
                            .Where(a => a.HasValue)
                            .Select(a => a.Value)
                            .ToList());

                if(accessibilities.Count == 0)
                {
                    continue;
                }

                var blockAccessibility = context.Symbol.DeclaredAccessibility;
                var accessibilityIndex = accessibilities.IndexOf(blockAccessibility);
                if(accessibilityIndex == -1)
                {
                    // accessibility is not part of the configuration so we assume user doesn't care where it goes.
                    continue;
                }

                var symbolsAtSameLevel = context.Symbol.ContainingType.GetMembers().ToList();
                var symbolMemberIndex = symbolsAtSameLevel.IndexOf(context.Symbol);
                if(symbolMemberIndex > 0)
                {
                    var previousAccessIndex = PreviousMemberAccessibilityIndex(symbolMemberIndex);
                    if(previousAccessIndex > accessibilityIndex)
                    {
                        // For all such symbols, produce a diagnostic.
                        var diagnostic = Diagnostic.Create(Rule, l, context.Symbol.Name, context.Symbol.ContainingType);

                        context.ReportDiagnostic(diagnostic);
                    }
                }

                int PreviousMemberAccessibilityIndex(int index)
                {
                    if(index <= 0)
                    {
                        return -1;
                    }

                    var previousSymbol = symbolsAtSameLevel[symbolMemberIndex - 1];
                    var previousAccessibility = previousSymbol.DeclaredAccessibility;
                    var previousAccessIndex = accessibilities.IndexOf(previousAccessibility);
                    if(previousAccessIndex >= 0)
                    {
                        return previousAccessIndex;
                    }

                    return PreviousMemberAccessibilityIndex(index - 1);
                }
            }
        }
    }
}
