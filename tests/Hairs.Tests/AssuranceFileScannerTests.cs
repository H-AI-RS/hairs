// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using Hairs.Assurance;
using Hairs.Language.CSharp;

namespace Hairs.Tests;

/// <summary>
/// Whole-file scan tests.
/// </summary>
public sealed class AssuranceFileScannerTests
{
    private static AssuranceScannedFile Scan(string text, IEnumerable<string>? symbols = null) =>
        new CSharpAssuranceFileScanner(symbols).ScanFile(text, "x.cs");

    private static AssuranceFileUnit Unit(AssuranceScannedFile file, string name) =>
        file.Units.Single(unit => unit.Unit.Name == name);

    [Fact(Timeout = 600000)]
    public void The_File_Scan_Reports_The_Same_Units_As_The_Unit_Scanner()
    {
        IReadOnlyList<AssuranceScannedUnit> units = new CSharpAssuranceScanner()
            .Scan(AssuranceFixture.Descriptor, "x.cs");
        AssuranceScannedFile file = Scan(AssuranceFixture.Descriptor);

        Assert.Equal(units, file.Units.Select(unit => unit.Unit));
    }

    [Fact(Timeout = 600000)]
    public void Every_Unit_Kind_Is_Named()
    {
        const string text = """
            namespace N;
            public class C
            {
                public const int F = 1;
                public event System.Action? E;
                public event System.Action X { add { } remove { } }
                public C() { System.Console.WriteLine(); }
                ~C() { System.Console.WriteLine(); }
                public int P { get { return F + 1; } }
                public int this[int i] => i;
                public void M() { System.Console.WriteLine(); }
                public static C operator +(C a, C b) => b;
                public static implicit operator int(C c) => 1 + c.P;
            }
            public struct S { }
            public interface I { }
            public record R(int A);
            public record struct RS(int A);
            public enum En { A, B }
            public delegate void D();
            """;

        AssuranceScannedFile file = Scan(text);

        Assert.Equal(
            [
                ("N.C", "class"),
                ("N.C.F", "field"),
                ("N.C.E", "event field"),
                ("N.C.X", "event"),
                ("N.C.C()", "constructor"),
                ("N.C.~C()", "destructor"),
                ("N.C.P", "property"),
                ("N.C.this[]", "indexer"),
                ("N.C.M()", "method"),
                ("N.C.operator +(C, C)", "operator"),
                ("N.C.operator int(C)", "conversion"),
                ("N.S", "struct"),
                ("N.I", "interface"),
                ("N.R", "record"),
                ("N.RS", "record struct"),
                ("N.En", "enum"),
                ("N.En.A", "enum member"),
                ("N.En.B", "enum member"),
                ("N.D", "delegate"),
            ],
            file.Units.Select(unit => (unit.Unit.Name, unit.Unit.Kind)));
    }

    [Fact(Timeout = 600000)]
    public void A_Declaration_Reports_Its_Column_And_Indent()
    {
        string text = "namespace N;\n" +
            "public class C\n" +
            "{\n" +
            "\t  [System.Obsolete]\n" +
            "\t  public void M() { System.Console.WriteLine(); }\n" +
            "    public int A; public int B;\n" +
            "}\n";

        AssuranceScannedFile file = Scan(text);

        AssuranceFileUnit method = Unit(file, "N.C.M()");
        Assert.Equal(3, method.Unit.DeclarationLine);
        Assert.Equal(3, method.Unit.DeclarationColumn);
        Assert.Equal("\t  ", method.Indent);
        Assert.True(method.StartsOwnLine);

        AssuranceFileUnit second = Unit(file, "N.C.B");
        Assert.Equal(5, second.Unit.DeclarationLine);
        Assert.False(second.StartsOwnLine);
        Assert.Equal("    ", second.Indent);
        Assert.True(Unit(file, "N.C.A").StartsOwnLine);
    }

    [Fact(Timeout = 600000)]
    public void The_Annotation_Is_Found_In_The_Leading_Trivia()
    {
        string text = "namespace N;\n" +
            "/// <summary>Doc.</summary>\n" +
            "// Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF\n" +
            "// Hairs-Human:        PENDING\n" +
            "[System.Obsolete]\n" +
            "public class C { }\n";

        AssuranceFileUnit type = Unit(Scan(text), "N.C");

        Assert.Equal(2, type.AnnotationLine);
        Assert.Equal([2, 3], type.LeadingAssuranceLines);
        Assert.Equal(4, type.Unit.DeclarationLine);
    }

    [Fact(Timeout = 600000)]
    public void A_Marker_Inside_A_String_Is_Not_A_Comment()
    {
        string text = "namespace N;\n" +
            "public class C\n" +
            "{\n" +
            "    public const string F = \"\"\"\n" +
            "    // Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF\n" +
            "    // Hairs-Human:        PENDING\n" +
            "    \"\"\";\n" +
            "}\n";

        AssuranceScannedFile file = Scan(text);

        Assert.Empty(file.AssuranceCommentLines);
        Assert.All(file.Units, unit => Assert.Null(unit.AnnotationLine));
    }

    [Fact(Timeout = 600000)]
    public void A_Block_Inside_The_Header_Is_Reported_And_Not_Attached()
    {
        string text = "namespace N;\n" +
            "[System.Obsolete]\n" +
            "// Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF\n" +
            "// Hairs-Human:        PENDING\n" +
            "public class C\n" +
            "{\n" +
            "}\n";

        AssuranceScannedFile file = Scan(text);
        AssuranceFileUnit type = Unit(file, "N.C");

        Assert.Equal([2, 3], file.AssuranceCommentLines);
        Assert.Null(type.AnnotationLine);
        Assert.Empty(type.LeadingAssuranceLines);
        Assert.Equal(1, type.Unit.DeclarationLine);
        Assert.Equal(5, type.HeaderEndLine);
    }

    [Fact(Timeout = 600000)]
    public void Directives_Are_Reported_With_Their_Lines()
    {
        string text = "#nullable enable\n" +
            "namespace N;\n" +
            "#if DEBUG\n" +
            "public class C { }\n" +
            "#endif\n";

        AssuranceScannedFile file = Scan(text);

        Assert.Equal(
            [new AssuranceDirective(0, "#nullable enable"), new AssuranceDirective(2, "#if DEBUG"), new AssuranceDirective(4, "#endif")],
            file.Directives);
    }

    [Fact(Timeout = 600000)]
    public void The_Preprocessor_Symbols_Decide_What_Is_Code()
    {
        string text = "namespace N;\n#if CUSTOM\npublic class C { }\n#endif\n";

        Assert.Empty(Scan(text).Units);
        Assert.Equal("N.C", Assert.Single(Scan(text, ["CUSTOM"]).Units).Unit.Name);
    }

    [Fact(Timeout = 600000)]
    public void The_Line_Count_Is_The_Parsers()
    {
        string separator = ((char)0x2028).ToString();
        string text = "namespace N;\n// a" + separator + "b\npublic class C { }\n";

        AssuranceScannedFile file = Scan(text);

        Assert.Equal(new AssuranceLines(text).Count + 1, file.LineCount);
    }
}
