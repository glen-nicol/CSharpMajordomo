using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace CSharpMajordomo;

public static class Rules
{
    // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
    // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Localizing%20Analyzers.md for more on localization

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

    public static IEnumerable<DiagnosticDescriptor> SpacingDiagnostics
    {
        get
        {
            yield return InterGroupSpacing.Rule;
            yield return IntraGroupSpacing.Rule;
        }
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
