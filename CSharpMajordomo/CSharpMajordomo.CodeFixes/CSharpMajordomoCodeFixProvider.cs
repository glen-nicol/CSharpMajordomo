using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CSharpMajordomo
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CSharpMajordomoCodeFixProvider)), Shared]
    public class CSharpMajordomoCodeFixProvider : CodeFixProvider
    {
        private static ConcurrentDictionary<string, Comparer<MemberSyntaxReference>> CACHED_CONFIG = new();

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(CSharpMajordomoAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            foreach(var diagnostic in context.Diagnostics)
            {
                var diagnosticSpan = diagnostic.Location.SourceSpan;
                if(!diagnostic.Properties.TryGetValue(CSharpMajordomoAnalyzer.SORT_ORDERING_CONFIG_KEY, out var configOrder))
                {
                    continue;
                }

                // Find the type declaration identified by the diagnostic.
                var containingType = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().First();

                var sorter = CACHED_CONFIG.GetOrAdd(configOrder, configOrder => SyntaxSorters.ParseTokenPriority(configOrder).ToComparer());

                // Register a code action that will invoke the fix.
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: CodeFixResources.CodeFixTitle,
                        createChangedDocument: c => SortMembersAsync(context, containingType, sorter, c),
                        equivalenceKey: nameof(CodeFixResources.CodeFixTitle)),
                    diagnostic);
            }
        }

        private async Task<Document> SortMembersAsync(
            CodeFixContext context,  
            TypeDeclarationSyntax typeDecl,
            Comparer<MemberSyntaxReference> sorter, 
            CancellationToken cancellationToken)
        {
            var document = context.Document;

            var editor = await DocumentEditor.CreateAsync(document, cancellationToken);

            var nodes = typeDecl.Members.Select((m, i) => new MemberSyntaxReference(m, i)).ToList();
            nodes.Sort(sorter);

            var workspace = context.Document.Project.Solution.Workspace;

            var newTypeDecl = typeDecl.WithMembers(new SyntaxList<MemberDeclarationSyntax>(nodes.Select(m => m.Member)));
            var formatted =
                Formatter.Format(
                    newTypeDecl,
                    Formatter.Annotation,
                    workspace,
                    workspace.Options);
            editor.ReplaceNode(typeDecl, formatted);

            return editor.GetChangedDocument();
            //return await Formatter.FormatAsync(editor.GetChangedDocument(), cancellationToken: context.CancellationToken);
        }
    }
}
