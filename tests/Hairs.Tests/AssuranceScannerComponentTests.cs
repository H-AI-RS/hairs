// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using Hairs.Assurance;
using Hairs.Language.CSharp;
using Xunit;

namespace Hairs.Tests;

/// <summary>
/// What the scanner does for CRLF checkouts, directives, repeated names,
/// top-level statements, initializers, and comments.
/// </summary>
public sealed class AssuranceScannerComponentTests
{
    private static IReadOnlyList<AssuranceScannedUnit> Units(
        string text,
        AssuranceExemptionPredicate predicate = AssuranceExemptionPredicate.OwningComponent,
        AssuranceNamedValues namedValues = AssuranceNamedValues.Reviewed) =>
        new CSharpAssuranceScanner(null, predicate, namedValues).Scan(text, "x.cs");

    private static AssuranceScannedUnit Unit(
        string text,
        string name,
        AssuranceExemptionPredicate predicate = AssuranceExemptionPredicate.OwningComponent,
        AssuranceNamedValues namedValues = AssuranceNamedValues.Reviewed) =>
        Units(text, predicate, namedValues).Single(unit => unit.Name == name);

    [Fact(Timeout = 600000)]
    public void A_Crlf_Checkout_Of_A_Literal_That_Spans_Lines_Fingerprints_As_The_Lf_Blob()
    {
        string lf =
            "namespace N;\n" +
            "\n" +
            "public static class A\n" +
            "{\n" +
            "    public const string S = @\"x\n" +
            "y\";\n" +
            "\n" +
            "    public static string R() => \"\"\"\n" +
            "        first\n" +
            "        second\n" +
            "        \"\"\";\n" +
            "\n" +
            "    public static string I(int n) => $@\"{n}\n" +
            "tail\";\n" +
            "\n" +
            "    public static int T(System.Collections.Generic.List<(int child, int inline,\n" +
            "            double offX)> list) => list.Count + 1;\n" +
            "}\n";
        string crlf = lf.Replace("\n", "\r\n", StringComparison.Ordinal);
        var files = new CSharpAssuranceFileScanner();

        Assert.Equal(
            Units(lf).Select(static unit => (unit.Name, unit.Fingerprint)),
            Units(crlf).Select(static unit => (unit.Name, unit.Fingerprint)));
        Assert.Equal(files.ScanFile(lf, "x.cs").FileFingerprint, files.ScanFile(crlf, "x.cs").FileFingerprint);
        Assert.Equal(CSharpAssuranceScanner.FingerprintOfFile(lf, "x.cs"), CSharpAssuranceScanner.FingerprintOfFile(crlf, "x.cs"));

        Assert.Equal(Unit(lf, "N.A.S").Fingerprint, Unit(lf.Replace("@\"x\n", "@\"x\r", StringComparison.Ordinal), "N.A.S").Fingerprint);

        const string Tuple = "N.A.T(System.Collections.Generic.List<(int child, int inline, double offX)>)";
        Assert.Equal(Tuple, Unit(lf, Tuple).Name);
        Assert.Equal(Tuple, Unit(crlf, Tuple).Name);
    }

    [Fact(Timeout = 600000)]
    public void Code_Under_A_Directive_Is_In_Its_Units_Fingerprint_And_The_Files()
    {
        const string Method =
            "namespace N;\n" +
            "public static class C\n" +
            "{\n" +
            "    public static int Scale(int value, int factor)\n" +
            "    {\n" +
            "#if DEBUG\n" +
            "        return value * factor;\n" +
            "#else\n" +
            "        return value * factor;\n" +
            "#endif\n" +
            "    }\n" +
            "}\n";

        string rewritten = Method.Replace(
            "#else\n        return value * factor;\n",
            "#else\n        System.IO.File.Delete(\"C:/important\"); return int.MaxValue;\n",
            StringComparison.Ordinal);
        string rewrapped = Method.Replace(
            "#else\n        return value * factor;\n",
            "#else\n        return value\n            * factor; // a comment\n",
            StringComparison.Ordinal);
        string flipped = Method.Replace("#if DEBUG", "#if !DEBUG", StringComparison.Ordinal);
        var files = new CSharpAssuranceFileScanner();

        string original = Unit(Method, "N.C.Scale(int, int)").Fingerprint;
        Assert.NotEqual(original, Unit(rewritten, "N.C.Scale(int, int)").Fingerprint);
        Assert.NotEqual(original, Unit(flipped, "N.C.Scale(int, int)").Fingerprint);
        Assert.Equal(original, Unit(rewrapped, "N.C.Scale(int, int)").Fingerprint);
        Assert.NotEqual(files.ScanFile(Method, "x.cs").FileFingerprint, files.ScanFile(rewritten, "x.cs").FileFingerprint);

        Assert.Equal(Unit(Method, "N.C").Fingerprint, Unit(rewritten, "N.C").Fingerprint);
    }

    [Fact(Timeout = 600000)]
    public void Names_Are_Unique_Within_A_File_And_Otherwise_The_Owning_Components()
    {
        const string Text =
            "namespace N;\n" +
            "public partial class P\n" +
            "{\n" +
            "    public int this[int i] => i + 1;\n" +
            "    public int this[string s, int i] => s.Length + i;\n" +
            "    public int Only { get { return 1 + 1; } }\n" +
            "}\n" +
            "public partial class P\n" +
            "{\n" +
            "}\n" +
            "public static class Box\n" +
            "{\n" +
            "    public static int Kind() => 1 + 1;\n" +
            "}\n" +
            "public static class Box<T>\n" +
            "{\n" +
            "    public static int Kind() => 2 + 2;\n" +
            "}\n";

        IReadOnlyList<AssuranceScannedUnit> units = Units(Text);

        Assert.Equal(
            [
                "N.P", "N.P.this[int]", "N.P.this[string, int]", "N.P.Only", "N.P#2",
                "N.Box", "N.Box.Kind()", "N.Box<T>", "N.Box<T>.Kind()",
            ],
            units.Select(static unit => unit.Name));
        Assert.Equal(units.Count, units.Select(static unit => unit.Name).Distinct(StringComparer.Ordinal).Count());

        Assert.Equal(units, new CSharpAssuranceFileScanner().ScanFile(Text, "x.cs").Units.Select(static unit => unit.Unit));
    }

    [Fact(Timeout = 600000)]
    public void Top_Level_Statements_Are_One_Relevant_Unit()
    {
        const string Program =
            "using System.IO;\n" +
            "\n" +
            "string target = args[0];\n" +
            "Wipe(target);\n" +
            "\n" +
            "static void Wipe(string path)\n" +
            "{\n" +
            "    File.Delete(path);\n" +
            "}\n";

        AssuranceScannedUnit unit = Assert.Single(Units(Program));
        Assert.Equal(AssuranceVocabulary.TopLevelStatements, unit.Name);
        Assert.False(unit.IsExempt);
        Assert.Equal("top-level statements", unit.Kind);
        Assert.Equal(2, unit.DeclarationLine);
        Assert.Equal(8, unit.EndLine);

        string edited = Program.Replace("File.Delete(path);", "File.Delete(path + \".bak\");", StringComparison.Ordinal);
        Assert.NotEqual(unit.Fingerprint, Assert.Single(Units(edited)).Fingerprint);

        AssuranceFileUnit scanned = Assert.Single(new CSharpAssuranceFileScanner().ScanFile(Program, "x.cs").Units);
        Assert.True(scanned.StartsOwnLine);
        Assert.Equal(2, scanned.HeaderEndLine);
    }

    [Fact(Timeout = 600000)]
    public void The_Strict_Predicate_Does_Not_Exempt_Code_That_Runs()
    {
        const string Text =
            "namespace N;\n" +
            "public sealed class Gadget\n" +
            "{\n" +
            "    private readonly int _seed = Wipe(\"C:/x\");\n" +
            "    private static System.Func<int> s_hook = () => { System.IO.File.Delete(\"C:/y\"); return 0; };\n" +
            "    public int Auto { get; } = Wipe(\"C:/z\");\n" +
            "    public int Throws => throw new System.InvalidOperationException(Describe());\n" +
            "    private int _count = 0;\n" +
            "    private readonly System.Collections.Generic.List<int> _items = new();\n" +
            "    private System.DateTimeKind _kind = System.DateTimeKind.Utc | System.DateTimeKind.Local;\n" +
            "    public int Plain { get; set; } = -1;\n" +
            "    public int Fails => throw new System.InvalidOperationException(\"no\");\n" +
            "    public static class AssemblyMarker\n" +
            "    {\n" +
            "        public static int Wipe(string p) { System.IO.File.Delete(p); return 0; }\n" +
            "    }\n" +
            "    private static int Wipe(string p) { System.IO.File.Delete(p); return 0; }\n" +
            "    private static string Describe() => System.IO.File.ReadAllText(\"C:/secret\");\n" +
            "}\n";

        string[] relevant = ["N.Gadget._seed", "N.Gadget.s_hook", "N.Gadget.Auto", "N.Gadget.Throws", "N.Gadget.AssemblyMarker.Wipe(string)"];
        string[] stillExempt = ["N.Gadget._count", "N.Gadget._items", "N.Gadget._kind", "N.Gadget.Plain", "N.Gadget.Fails"];

        foreach (string name in relevant)
        {
            Assert.True(Unit(Text, name).IsExempt, $"{name} is exempt under the owning component's predicate");
            Assert.False(Unit(Text, name, AssuranceExemptionPredicate.Strict).IsExempt, $"{name} is relevant strictly");
        }

        foreach (string name in stillExempt)
        {
            AssuranceScannedUnit owning = Unit(Text, name);
            AssuranceScannedUnit strict = Unit(Text, name, AssuranceExemptionPredicate.Strict);
            Assert.True(strict.IsExempt, $"{name} is exempt strictly");
            Assert.Equal(owning.Exemption, strict.Exemption);
        }
    }

    private const string NamedValues =
        "namespace N;\n" +
        "public static class Native\n" +
        "{\n" +
        "    public const int EGL_NONE = 0x3038;\n" +
        "    private const string Library = \"libEGL\", Other = Library + \".so\";\n" +
        "    public static readonly System.Guid IID_IUnknown = new System.Guid(\"00000000-0000-0000-C000-000000000046\");\n" +
        "    internal static readonly Guid CLSID = new(0x4590f811, 0x1d3a, 0x11d0, 0x89, 0x1f, 0x00, 0xaa, 0x00, 0x4b, 0x2e, 0x24);\n" +
        "    private static readonly Guid Nothing = Guid.Empty, Also = default;\n" +
        "    public static readonly IntPtr NullHandle = IntPtr.Zero;\n" +
        "    public static readonly IntPtr Invalid = new IntPtr(-1), Topmost = (IntPtr)(-1);\n" +
        "    public static readonly global::System.UIntPtr Top = (global::System.UIntPtr)0xFFFFFFFF;\n" +
        "    public static readonly nint Minus = -1;\n" +
        "    public static readonly nuint Zero = nuint.Zero;\n" +
        "    public enum Mode : uint { Off = 0, On = 0x80000000 }\n" +
        "\n" +
        "    private static readonly System.Text.RegularExpressions.Regex Pattern = new(\"a+\");\n" +
        "    private static readonly int[] Table = [1, 2, 3];\n" +
        "    private static readonly Guid Fresh = Guid.NewGuid();\n" +
        "    private static readonly Guid Parsed = Guid.Parse(\"00000000-0000-0000-C000-000000000046\");\n" +
        "    private static readonly Guid Built = new Guid(1, 2, 3, new byte[] { 4, 5, 6, 7, 8, 9, 10, 11 });\n" +
        "    private static readonly IntPtr Handle = Open(\"x\");\n" +
        "    private static readonly IntPtr Copy = NullHandle;\n" +
        "    private static readonly IntPtr Sum = (IntPtr)(1 + 2);\n" +
        "    private static readonly IntPtr Wrapped = new IntPtr(1) { };\n" +
        "    private static readonly Guid Late;\n" +
        "    private readonly Guid _instance = new Guid(\"00000000-0000-0000-C000-000000000046\");\n" +
        "    private static readonly long Literal = 7;\n" +
        "    private static System.IntPtr Open(string name) => System.IntPtr.Zero;\n" +
        "}\n";

    [Theory(Timeout = 600000)]
    [InlineData("N.Native.EGL_NONE")]
    [InlineData("N.Native.Library, Other")]
    [InlineData("N.Native.IID_IUnknown")]
    [InlineData("N.Native.CLSID")]
    [InlineData("N.Native.Nothing, Also")]
    [InlineData("N.Native.NullHandle")]
    [InlineData("N.Native.Invalid, Topmost")]
    [InlineData("N.Native.Top")]
    [InlineData("N.Native.Minus")]
    [InlineData("N.Native.Zero")]
    [InlineData("N.Native.Mode")]
    public void A_Named_Value_Is_Watched_Where_The_Component_Says_So(string name)
    {
        foreach (AssuranceExemptionPredicate predicate in new[] { AssuranceExemptionPredicate.Strict, AssuranceExemptionPredicate.OwningComponent })
        {
            AssuranceScannedUnit reviewed = Unit(NamedValues, name, predicate);
            Assert.False(reviewed.IsExempt, $"{name} is relevant where named values are reviewed");
            Assert.Equal("None", reviewed.Exemption);

            AssuranceScannedUnit watched = Unit(NamedValues, name, predicate, AssuranceNamedValues.Watched);
            Assert.True(watched.IsExempt, $"{name} is exempt where named values are watched");
            Assert.Equal(AssuranceVocabulary.NamedValue, watched.Exemption);
            Assert.Equal(reviewed.Fingerprint, watched.Fingerprint);
        }
    }

    [Theory(Timeout = 600000)]
    [InlineData("N.Native.Pattern")]
    [InlineData("N.Native.Table")]
    [InlineData("N.Native.Fresh")]
    [InlineData("N.Native.Parsed")]
    [InlineData("N.Native.Built")]
    [InlineData("N.Native.Handle")]
    [InlineData("N.Native.Copy")]
    [InlineData("N.Native.Sum")]
    [InlineData("N.Native.Wrapped")]
    [InlineData("N.Native.Late")]
    [InlineData("N.Native._instance")]
    [InlineData("N.Native.Literal")]
    [InlineData("N.Native.Open(string)")]
    [InlineData("N.Native.Mode.On")]
    [InlineData("N.Native")]
    public void Nothing_Else_Is_A_Named_Value(string name)
    {
        foreach (AssuranceExemptionPredicate predicate in new[] { AssuranceExemptionPredicate.Strict, AssuranceExemptionPredicate.OwningComponent })
        {
            AssuranceScannedUnit reviewed = Unit(NamedValues, name, predicate);
            AssuranceScannedUnit watched = Unit(NamedValues, name, predicate, AssuranceNamedValues.Watched);

            Assert.NotEqual(AssuranceVocabulary.NamedValue, watched.Exemption);
            Assert.Equal(reviewed, watched);
        }
    }

    [Fact(Timeout = 600000)]
    public void A_Unit_Another_Case_Covers_Keeps_Its_Reason()
    {
        const string Text =
            "namespace N;\n" +
            "public static class AssemblyMarker\n" +
            "{\n" +
            "    public const int Version = 3;\n" +
            "}\n";

        Assert.Equal("InsideAssemblyMarker", Unit(Text, "N.AssemblyMarker.Version", namedValues: AssuranceNamedValues.Watched).Exemption);
        Assert.Equal(
            AssuranceVocabulary.NamedValue,
            Unit(Text, "N.AssemblyMarker.Version", AssuranceExemptionPredicate.Strict, AssuranceNamedValues.Watched).Exemption);
    }

    [Fact(Timeout = 600000)]
    public void Every_Line_Of_Every_Comment_Is_Reported()
    {
        const string Text =
            "namespace N;\n" +
            "/* HAIRS Code Assurance\n" +
            "   Human-reviewed: 3/3 */\n" +
            "/// <summary>Doc.</summary>\n" +
            "public class C // trailing\n" +
            "{\n" +
            "    const string S = \"// Annotated: 1/1\";\n" +
            "#if NEVER\n" +
            "    // Unverified: 0\n" +
            "#endif\n" +
            "}\n";

        AssuranceScannedFile file = new CSharpAssuranceFileScanner().ScanFile(Text, "x.cs");

        Assert.Equal(
            [
                (1, "/* HAIRS Code Assurance"),
                (2, "   Human-reviewed: 3/3 */"),
                (3, "/// <summary>Doc.</summary>"),
                (4, "// trailing"),
                (8, "    // Unverified: 0"),
            ],
            file.CommentLines.Select(static line => (line.Line, line.Text)));
    }
}
