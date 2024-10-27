using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using System;
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

            // TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            var containingType = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().First();
            var relevantMember = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<MemberDeclarationSyntax>().First();

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixResources.CodeFixTitle,
                    createChangedDocument: c => ModeMemberAsync(context.Document, containingType, relevantMember, c),
                    equivalenceKey: nameof(CodeFixResources.CodeFixTitle)),
                diagnostic);
        }

        private async Task<Document> ModeMemberAsync(Document document, TypeDeclarationSyntax typeDecl, MemberDeclarationSyntax memberDeclaration, CancellationToken cancellationToken)
        {
            // Get the symbol representing the type to be renamed.
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken);

            // Produce a new solution that has all references to that type renamed, including the declaration.
            var originalSolution = document.Project.Solution;
            var optionSet = originalSolution.Workspace.Options;


            var editor = await DocumentEditor.CreateAsync(document, cancellationToken);

            var index = typeDecl.Members.IndexOf(memberDeclaration);
            var startingNode = typeDecl.Members[index];
            var swapNode = typeDecl.Members[index - 1];

            var rewrittenType = InsertInSortedPosition(RemoveOffending(typeDecl), startingNode).NormalizeWhitespace();

            editor.ReplaceNode(typeDecl, rewrittenType);

            return editor.GetChangedDocument();

            TypeDeclarationSyntax RemoveOffending(TypeDeclarationSyntax td)
            {
                var node = td.Members[index];
                return td.RemoveNode(node, SyntaxRemoveOptions.KeepNoTrivia);
            }

            TypeDeclarationSyntax InsertInSortedPosition(TypeDeclarationSyntax td, MemberDeclarationSyntax toInsert)
            {
                var node = td.Members[index - 1];
                return td.InsertNodesBefore(node, new[] { toInsert.WithTrailingTrivia(SyntaxFactory.EndOfLine(string.Empty)) });
            }
        }
    }
}
