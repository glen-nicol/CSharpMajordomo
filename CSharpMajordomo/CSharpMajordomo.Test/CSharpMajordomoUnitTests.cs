using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Threading.Tasks;
using VerifyCS = CSharpMajordomo.Test.CSharpCodeFixVerifier<
    CSharpMajordomo.CSharpMajordomoAnalyzer,
    CSharpMajordomo.CSharpMajordomoCodeFixProvider>;

namespace CSharpMajordomo.Test
{
    [TestClass]
    public class CSharpMajordomoUnitTest
    {
        //No diagnostics expected to show up
        [TestMethod]
        public async Task Requires_editor_config_to_set_member_sort()
        {
            var test =
                """
                namespace NS;

                class TypeName
                {
                    private int _x;
                    public int _y;
                }
                """;

            var noEditorConfig = new Dictionary<string, string>();
            await VerifyCS.VerifyAnalyzerAsync(noEditorConfig, test);
        }

        [TestMethod]
        public async Task Can_sort_members_public_before_private()
        {
            var test =
                """
                namespace NS;
                
                class {|#0:TypeName|}
                {
                    private int _x;
                    public int _y;
                }
                """;

            var fixtest =
                """
                namespace NS;
                
                class TypeName
                {
                    public int _y;
                    private int _x;
                }
                """;

            var expected = VerifyCS.Diagnostic(Rules.MemberSorting.Rule).WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(new() { [Rules.MemberSorting.SORT_ORDERING_CONFIG_KEY] = "public,private" }, test, expected, fixtest);
        }

        [TestMethod]
        public async Task Can_sort_members_by_syntax_type()
        {
            var test =
                """
                using System;

                namespace NS;
                
                class {|#0:TypeName|}
                {
                    public interface NestedInterface { }
                    public enum NestedEnum { }
                    public delegate void NestedDelegate();
                    public record struct NestedRecordStruct { }
                    public struct NestedStruct { }
                    public record NestedRecord { }
                    public class NestedClass { }
                    public event EventHandler _event;
                    public void Method()
                    {
                    }
                    ~TypeName()
                    {
                    }
                    public TypeName()
                    {
                    }
                    public int Property { get; set; }
                    public int _field;
                }
                """;

            // NOTE: fix is meant to be exact opposite of starting order.
            var fixtest =
                """
                using System;

                namespace NS;
                
                class TypeName
                {
                    public int _field;
                    public int Property { get; set; }
                    public TypeName()
                    {
                    }
                    ~TypeName()
                    {
                    }
                    public void Method()
                    {
                    }
                    public event EventHandler _event;
                    public record NestedRecord { }
                    public class NestedClass { }
                    public record struct NestedRecordStruct { }
                    public struct NestedStruct { }
                    public delegate void NestedDelegate();
                    public enum NestedEnum { }
                    public interface NestedInterface { }
                }
                """;

            var expected = VerifyCS.Diagnostic(Rules.MemberSorting.Rule).WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(new() { [Rules.MemberSorting.SORT_ORDERING_CONFIG_KEY] = "field, property, constructor, destructor, method, event, class, record, struct, delegate, enum, interface" }, test, expected, fixtest);
        }

        [TestMethod]
        public async Task Can_sort_members_by_member_type_and_then_public_before_private()
        {
            var test =
                """
                namespace NS;
                
                class {|#0:TypeName|}
                {
                    private int Prop1 { get; set; }
                    public int Prop2 { get; set; }
                    private int _x;
                    public int _y;
                }
                """;

            var fixtest =
                """
                namespace NS;
                
                class TypeName
                {
                    public int _y;
                    private int _x;
                    public int Prop2 { get; set; }
                    private int Prop1 { get; set; }
                }
                """;

            var expected = VerifyCS.Diagnostic(Rules.MemberSorting.Rule).WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(new() { [Rules.MemberSorting.SORT_ORDERING_CONFIG_KEY] = "field, property, public, private" }, test, expected, fixtest);
        }

        [TestMethod]
        public async Task sorting_equivalent_members_maintains_document_order()
        {
            var test =
                """
                namespace NS;
                
                class {|#0:TypeName|}
                {
                    private int _x1;
                    public int _y;
                    private int _x2;
                }
                """;

            var fixtest =
                """
                namespace NS;
                
                class TypeName
                {
                    public int _y;
                    private int _x1;
                    private int _x2;
                }
                """;

            var expected = VerifyCS.Diagnostic(Rules.MemberSorting.Rule).WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(new() { [Rules.MemberSorting.SORT_ORDERING_CONFIG_KEY] = "field, public, private" }, test, expected, fixtest);
        }

        [TestMethod]
        public async Task Can_sort_readonly_fields_first()
        {
            var test =
                """
                namespace NS;
                
                class {|#0:TypeName|}
                {
                    private int _x2;
                    private readonly int _x1;
                }
                """;

            var fixtest =
                """
                namespace NS;
                
                class TypeName
                {
                    private readonly int _x1;
                    private int _x2;
                }
                """;

            var expected = VerifyCS.Diagnostic(Rules.MemberSorting.Rule).WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(new() { [Rules.MemberSorting.SORT_ORDERING_CONFIG_KEY] = "field, private, readonly" }, test, expected, fixtest);
        }

        [TestMethod]
        public async Task Can_sort_static_members_first()
        {
            var test =
                """
                namespace NS;
                
                class {|#0:TypeName|}
                {
                    public void Method2(){}
                    public static void Method(){}
                    public int _x2;
                    public static int _x1;
                }
                """;

            var fixtest =
                """
                namespace NS;
                
                class TypeName
                {
                    public static int _x1;
                    public int _x2;
                    public static void Method(){}
                    public void Method2(){}
                }
                """;

            var expected = VerifyCS.Diagnostic(Rules.MemberSorting.Rule).WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(new() { [Rules.MemberSorting.SORT_ORDERING_CONFIG_KEY] = "field, method, static" }, test, expected, fixtest);
        }

        [TestMethod]
        public async Task Can_sort_static_readonly_members_first()
        {
            var test =
                """
                namespace NS;
                
                class {|#0:TypeName|}
                {
                    private static int _x2;
                    private static readonly int _x1;
                }
                """;

            var fixtest =
                """
                namespace NS;
                
                class TypeName
                {
                    private static readonly int _x1;
                    private static int _x2;
                }
                """;

            var expected = VerifyCS.Diagnostic(Rules.MemberSorting.Rule).WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(new() { [Rules.MemberSorting.SORT_ORDERING_CONFIG_KEY] = "field, static, readonly" }, test, expected, fixtest);
        }

        [TestMethod]
        public async Task Can_sort_by_member_identifier()
        {
            var test =
                """
                namespace NS;
                
                class {|#0:TypeName|}
                {
                    private static int _x2;
                    private static int _x3;
                    private static int _x1;
                }
                """;

            var fixtest =
                """
                namespace NS;
                
                class TypeName
                {
                    private static int _x1;
                    private static int _x2;
                    private static int _x3;
                }
                """;

            var expected = VerifyCS.Diagnostic(Rules.MemberSorting.Rule).WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(new() { [Rules.MemberSorting.SORT_ORDERING_CONFIG_KEY] = "identifier" }, test, expected, fixtest);
        }

        [TestMethod]
        public async Task Can_sort_by_member_identifier_descending()
        {
            var test =
                """
                namespace NS;
                
                class {|#0:TypeName|}
                {
                    private static int _x2;
                    private static int _x3;
                    private static int _x1;
                }
                """;

            var fixtest =
                """
                namespace NS;
                
                class TypeName
                {
                    private static int _x3;
                    private static int _x2;
                    private static int _x1;
                }
                """;

            var expected = VerifyCS.Diagnostic(Rules.MemberSorting.Rule).WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(new() { [Rules.MemberSorting.SORT_ORDERING_CONFIG_KEY] = "-identifier" }, test, expected, fixtest);
        }
    }
}
