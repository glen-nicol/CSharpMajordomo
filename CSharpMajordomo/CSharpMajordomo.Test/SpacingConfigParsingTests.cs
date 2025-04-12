using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CSharpMajordomo.Test
{
    [TestClass]
    public class SpacingConfigParsingTests
    {
        [DataTestMethod]
        [DynamicData(nameof(MemberDeclarations))]
        public void single_positive_integer_config_specifies_for_all_types(string declarationSyntax)
        {
            var getNodeSpacing = SyntaxWhitespace.ParseConfigIntoSpacing(defaultDefaultSpacing: 1, maxSpacing: 4, lineSpacingConfig: "2");

            var result = getNodeSpacing(SyntaxFactory.ParseMemberDeclaration(declarationSyntax)!);

            result.Should().Be(2);
        }

        [DataTestMethod]
        [DynamicData(nameof(MemberDeclarations))]
        public void Empty_config_uses_default_spacing(string declarationSyntax)
        {
            var getNodeSpacing = SyntaxWhitespace.ParseConfigIntoSpacing(defaultDefaultSpacing: 1, maxSpacing: 4, lineSpacingConfig: string.Empty);

            var result = getNodeSpacing(SyntaxFactory.ParseMemberDeclaration(declarationSyntax)!);

            result.Should().Be(1);
        }

        [TestMethod]
        public void Can_specify_spacing_by_node_type()
        {
            var getNodeSpacing = SyntaxWhitespace.ParseConfigIntoSpacing(defaultDefaultSpacing: 1, maxSpacing: 4, lineSpacingConfig: "field:0");

            var fieldResult = getNodeSpacing(SyntaxFactory.ParseMemberDeclaration("int fieldName;")!);
            var propertyResult = getNodeSpacing(SyntaxFactory.ParseMemberDeclaration("int PropertyName { get; }")!);

            fieldResult.Should().Be(0);
            propertyResult.Should().Be(1); // default value
        }

        [TestMethod]
        public void Can_disable_spacing_by_node_type()
        {
            var getNodeSpacing = SyntaxWhitespace.ParseConfigIntoSpacing(defaultDefaultSpacing: 1, maxSpacing: 4, lineSpacingConfig: "field:disable");

            var fieldResult = getNodeSpacing(SyntaxFactory.ParseMemberDeclaration("int fieldName;")!);
            var propertyResult = getNodeSpacing(SyntaxFactory.ParseMemberDeclaration("int PropertyName { get; }")!);

            fieldResult.Should().Be(null);
            propertyResult.Should().Be(1); // default value
        }

        [DataTestMethod]
        [DynamicData(nameof(MemberDeclarations))]
        public void Any_non_integer_disables_spacing_config(string declarationSyntax)
        {
            var getNodeSpacing = SyntaxWhitespace.ParseConfigIntoSpacing(defaultDefaultSpacing: 1, maxSpacing: 4, lineSpacingConfig: "disable");

            var result = getNodeSpacing(SyntaxFactory.ParseMemberDeclaration(declarationSyntax)!);

            result.Should().Be(null);
        }


        [DataTestMethod]
        [DynamicData(nameof(FieldAndPropertyConfigInputs))]
        public void Can_specify_mulitple_spacings_by_node_type(string configSyntax)
        {
            var getNodeSpacing = SyntaxWhitespace.ParseConfigIntoSpacing(defaultDefaultSpacing: 1, maxSpacing: 4, lineSpacingConfig: configSyntax);

            var fieldResult = getNodeSpacing(SyntaxFactory.ParseMemberDeclaration("int fieldName;")!);
            var propertyResult = getNodeSpacing(SyntaxFactory.ParseMemberDeclaration("int PropertyName { get; }")!);
            var methodResult = getNodeSpacing(SyntaxFactory.ParseMemberDeclaration("void MethodName(){}")!);

            fieldResult.Should().Be(0);
            propertyResult.Should().Be(0);
            methodResult.Should().Be(1);
        }


        private static IEnumerable<object[]> MemberDeclarations
        {
            get
            {
                yield return ["int fieldName;"];
                yield return ["int PropertyName { get; }"];
                yield return ["void MethodName(){}"];
            }
        }

        private static IEnumerable<object[]> FieldAndPropertyConfigInputs
        {
            get
            {
                yield return ["field:0, property:0"];
                yield return ["field:0,property:0"];
                yield return ["field:0,property:0  "];
                yield return [" field:0,property:0  "];
                yield return ["(field:0), property:0"];
                yield return ["(field:0),property:0"];
                yield return ["(field:0),(property:0)"];
                
            }
        }
    }
}
