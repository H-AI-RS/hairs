// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Hairs.Assurance;
using Hairs.Language.CSharp;
using Xunit;

namespace Hairs.Tests;

/// <summary>
/// Writing machine assessments above unannotated units.
/// </summary>
public sealed class AssuranceInsertTests
{
    private static readonly CSharpAssuranceFileScanner Scanner = new();

    private const string Source =
        "using System;\n" +
        "\n" +
        "namespace N;\n" +
        "\n" +
        "/// <summary>Doc.</summary>\n" +
        "[Serializable]\n" +
        "public sealed class C\n" +
        "{\n" +
        "    /// <summary>Runs.</summary>\n" +
        "    [Obsolete(\"x\")]\n" +
        "    public void Run() { Console.WriteLine(); }\n" +
        "\n" +
        "    public int Auto { get; set; }\n" +
        "\n" +
        "    public sealed class Inner\n" +
        "    {\n" +
        "        public int Twice(int x) { return x * 2; }\n" +
        "    }\n" +
        "}\n";

    private static string Fingerprint(string text, string unit) =>
        Scanner.ScanFile(text, "x.cs").Units.Single(found => found.Unit.Name == unit).Unit.Fingerprint;

    private static AssuranceAssessment Assess(
        string text,
        string unit,
        string security = "Low",
        string? criterion = null,
        int index = 0,
        int resources = 1) => new()
        {
            Index = index,
            File = "x.cs",
            Unit = unit,
            Fingerprint = Fingerprint(text, unit),
            Origin = "AI",
            Ip = "Low",
            Security = security,
            Resources = resources,
            FalsifiedIf = criterion,
        };

    private static AssuranceInsertFileResult Apply(string text, params AssuranceAssessment[] entries) =>
        AssuranceInsertion.Apply(text, "x.cs", Scanner, entries);

    [Fact(Timeout = 600000)]
    public void A_Block_Goes_Above_The_Attributes_And_Below_The_Doc_Comment()
    {
        string formattedSource =
            "using System;\n" +
            "\n" +
            "namespace N;\n" +
            "\n" +
            "/// <summary>Doc.</summary>\n" +
            "[Serializable]\n" +
            "public sealed class C\n" +
            "{\n" +
            "    /// <summary>Runs.</summary>\n" +
            "    [Obsolete(\"x\")]\n" +
            "    public void Run() { Console.WriteLine(); }\n" +
            "\n" +
            "    public int Auto { get; set; }\n" +
            "\n" +
            "    public sealed class Inner\n" +
            "    {\n" +
            "        public int Twice(int x) { return x * 2; }\n" +
            "    }\n" +
            "}\n";

        AssuranceInsertFileResult result = Apply(
            formattedSource,
            Assess(formattedSource, "N.C", index: 0),
            Assess(formattedSource, "N.C.Run()", index: 1, resources: 0),
            Assess(formattedSource, "N.C.Inner.Twice(int)", security: "High", criterion: "an odd input is returned unchanged", index: 2, resources: 2));

        Assert.All(result.Entries, entry => Assert.True(entry.Applied, entry.Message));
        Assert.Equal(
            "using System;\n" +
            "\n" +
            "namespace N;\n" +
            "\n" +
            "/// <summary>Doc.</summary>\n" +
            "// Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF\n" +
            "// Hairs-Human:        PENDING\n" +
            "[Serializable]\n" +
            "public sealed class C\n" +
            "{\n" +
            "    /// <summary>Runs.</summary>\n" +
            "    // Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF\n" +
            "    // Hairs-Human:        PENDING\n" +
            "    [Obsolete(\"x\")]\n" +
            "    public void Run() { Console.WriteLine(); }\n" +
            "\n" +
            "    public int Auto { get; set; }\n" +
            "\n" +
            "    public sealed class Inner\n" +
            "    {\n" +
            "        // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=2; Fingerprint=TBF\n" +
            "        // Hairs-Falsified-If: an odd input is returned unchanged\n" +
            "        // Hairs-Human:        PENDING\n" +
            "        public int Twice(int x) { return x * 2; }\n" +
            "    }\n" +
            "}\n",
            result.Text);

        Assert.Equal([6, 12, 21], result.Entries.Select(entry => entry.Line!.Value));

        IReadOnlyList<AssuranceCandidate> after = AssuranceCandidates.Classify(
            new AssuranceLines(result.Text), Scanner.ScanFile(result.Text, "x.cs"));
        Assert.All(
            after.Where(candidate => candidate.IsRelevant && candidate.Unit.Name != "N.C.Inner"),
            candidate => Assert.Equal(AssuranceUnitState.AiAssessed, candidate.State));
    }

    [Fact(Timeout = 600000)]
    public void A_Spec_Is_Written_Between_Origin_And_IP()
    {
        AssuranceAssessment entry = Assess(Source, "N.C") with { Spec = "ADR-0007 s6" };

        string text = Apply(Source, entry).Text;

        Assert.Contains(
            "// Hairs-AI:           Origin=AI; Spec=ADR-0007 s6; IP=Low; Security=Low; Resources=1; Fingerprint=TBF\n",
            text,
            StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void A_Crlf_File_Stays_Crlf()
    {
        string crlf = Source.Replace("\n", "\r\n", StringComparison.Ordinal);

        AssuranceInsertFileResult result = Apply(crlf, Assess(crlf, "N.C.Run()"));

        Assert.True(result.Entries[0].Applied, result.Entries[0].Message);
        Assert.Equal(
            crlf.Replace(
                "    [Obsolete(\"x\")]\r\n",
                "    // Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF\r\n" +
                "    // Hairs-Human:        PENDING\r\n" +
                "    [Obsolete(\"x\")]\r\n",
                StringComparison.Ordinal),
            result.Text);
        Assert.DoesNotContain("\n", result.Text.Replace("\r\n", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void A_Mixed_File_Keeps_Every_Ending_And_The_Block_Takes_Its_Neighbours()
    {
        string mixed =
            "namespace N;\r\n" +
            "public sealed class C\r\n" +
            "{\r\n" +
            "    public void Crlf() { System.Console.WriteLine(); }\r\n" +
            "\n" +
            "    public void Lf() { System.Console.WriteLine(); }\n" +
            "}\n";

        AssuranceInsertFileResult result = Apply(
            mixed,
            Assess(mixed, "N.C.Crlf()", index: 0),
            Assess(mixed, "N.C.Lf()", index: 1));

        Assert.All(result.Entries, entry => Assert.True(entry.Applied, entry.Message));
        Assert.Equal(
            "namespace N;\r\n" +
            "public sealed class C\r\n" +
            "{\r\n" +
            "    // Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF\r\n" +
            "    // Hairs-Human:        PENDING\r\n" +
            "    public void Crlf() { System.Console.WriteLine(); }\r\n" +
            "\n" +
            "    // Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF\n" +
            "    // Hairs-Human:        PENDING\n" +
            "    public void Lf() { System.Console.WriteLine(); }\n" +
            "}\n",
            result.Text);
    }

    [Fact(Timeout = 600000)]
    public void Removing_The_Inserted_Lines_Restores_The_Original()
    {
        string text =
            "namespace N;\n" +
            "public sealed class C\n" +
            "{\n" +
            "\tpublic const string Nul = \"a" + '\0' + "b\";\n" +
            "\tpublic void Run() { System.Console.WriteLine(Nul); }\n" +
            "}";

        AssuranceInsertFileResult result = Apply(
            text,
            Assess(text, "N.C.Nul", index: 0),
            Assess(text, "N.C.Run()", index: 1));

        Assert.All(result.Entries, entry => Assert.True(entry.Applied, entry.Message));
        var lines = new AssuranceLines(result.Text);
        foreach (int line in new[] { 3, 4, 6, 7 })
            Assert.StartsWith("\t// Hairs-", lines[line], StringComparison.Ordinal);

        foreach (int line in new[] { 7, 6, 4, 3 })
            lines.RemoveRange(line, 1);

        Assert.Equal(text, lines.Render());
    }

    [Fact(Timeout = 600000)]
    public void An_Exemption_Is_Written_As_The_Escape_Hatch()
    {
        var entry = new AssuranceAssessment { File = "x.cs", Unit = "N.C.Run()", Exempt = " generated shim " };

        AssuranceInsertFileResult result = Apply(Source, entry);

        Assert.True(result.Entries[0].Applied, result.Entries[0].Message);
        Assert.Contains(
            "    // Hairs-AI:           EXEMPT=generated shim\n    // Hairs-Human:        PENDING\n    [Obsolete",
            result.Text,
            StringComparison.Ordinal);

        AssuranceCandidate run = AssuranceCandidates.Classify(
                new AssuranceLines(result.Text), Scanner.ScanFile(result.Text, "x.cs"))
            .Single(candidate => candidate.Unit.Name == "N.C.Run()");
        Assert.True(run.IsExempt);
        Assert.Equal("DeclaredInSource", run.Exemption);
    }

    [Fact(Timeout = 600000)]
    public void An_Assembly_Closed_To_The_Escape_Hatch_Refuses_An_Exemption()
    {
        var entry = new AssuranceAssessment { File = "x.cs", Unit = "N.C.Run()", Exempt = "shim" };

        AssuranceInsertFileResult result = AssuranceInsertion.Apply(
            Source, "x.cs", Scanner, [entry], closedToEscapeHatch: true, assembly: "Hairs.Probe");

        Assert.False(result.Entries[0].Applied);
        Assert.Contains("Hairs.Probe is closed to the per-unit exemption", result.Entries[0].Message, StringComparison.Ordinal);
        Assert.Equal(Source, result.Text);
    }

    public static TheoryData<string, string> Refusals => new()
    {
        {
            "High",
            "is assessed Security=High and carries no '// Hairs-Falsified-If:' line, so nothing at the declaration says what would make it wrong"
        },
        {
            "Critical",
            "is assessed Security=Critical and carries no '// Hairs-Falsified-If:' line"
        },
    };

    [Theory(Timeout = 600000)]
    [MemberData(nameof(Refusals))]
    public void A_High_Risk_Without_A_Criterion_Is_Refused(string security, string expected)
    {
        AssuranceInsertFileResult result = Apply(Source, Assess(Source, "N.C.Run()", security: security));

        Assert.False(result.Entries[0].Applied);
        Assert.Contains(expected, result.Entries[0].Message, StringComparison.Ordinal);
        Assert.Equal(Source, result.Text);
    }

    [Theory(Timeout = 600000)]
    [InlineData("None")]
    [InlineData("Low")]
    [InlineData("Medium")]
    public void A_Criterion_Below_High_Is_Refused(string security)
    {
        AssuranceInsertFileResult result = Apply(
            Source, Assess(Source, "N.C.Inner.Twice(int)", security: security, criterion: "Twice(3) is not 6"));

        Assert.False(result.Entries[0].Applied);
        Assert.Equal(
            $"is assessed Security={security} and carries a '// Hairs-Falsified-If:' line, which is written only " +
            "for High and Critical; leave falsifiedIf out",
            result.Entries[0].Message);
        Assert.Equal(Source, result.Text);

        Assert.True(Apply(Source, Assess(Source, "N.C.Inner.Twice(int)", security: security)).Entries[0].Applied);
    }

    [Theory(Timeout = 600000)]
    [InlineData("it returns Security=None", "states the field Security=None, and a falsification criterion is prose, not data")]
    [InlineData("nothing here was approved by anyone", "claims a review by saying 'approved'")]
    [InlineData("   ", "carries no criterion")]
    public void A_Criterion_That_Is_Not_Prose_About_A_Fault_Is_Refused(string criterion, string expected)
    {
        AssuranceInsertFileResult result = Apply(Source, Assess(Source, "N.C.Run()", security: "High", criterion: criterion));

        Assert.False(result.Entries[0].Applied);
        Assert.Contains(expected, result.Entries[0].Message, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void A_Value_Outside_Its_Vocabulary_Is_Refused()
    {
        AssuranceAssessment entry = Assess(Source, "N.C.Run()") with { Origin = "Human", Ip = "low", Resources = 11, Cs = 12, Cl = -1 };

        AssuranceInsertFileResult result = Apply(Source, entry);

        Assert.False(result.Entries[0].Applied);
        Assert.Equal(
            "origin 'Human' is outside its vocabulary (Original, AI, Specification, Derived, Ported, ThirdParty); " +
            "ip 'low' is outside its vocabulary (None, Low, Medium, High, Unknown); " +
            "resources 11 is not an integer 0 to 10; " +
            "CS 12 is not an integer 0 to 10; " +
            "CL -1 is not an integer 0 to 10",
            result.Entries[0].Message);
    }

    [Fact(Timeout = 600000)]
    public void Stability_Assessment_Scores_Are_Rendered()
    {
        AssuranceAssessment entry = Assess(Source, "N.C.Run()") with { Cs = 10, Cl = 0 };

        AssuranceInsertFileResult result = Apply(Source, entry);

        Assert.True(result.Entries[0].Applied);
        Assert.Contains(
            "    // Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; CS=10; CL=0; Fingerprint=TBF\n",
            result.Text,
            StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void An_Unknown_Unit_Or_A_Stale_Fingerprint_Is_Refused()
    {
        AssuranceInsertFileResult result = Apply(
            Source,
            Assess(Source, "N.C.Run()", index: 0) with { Unit = "N.C.Walk()" },
            Assess(Source, "N.C.Run()", index: 1) with { Fingerprint = "000000" });

        Assert.Contains("no code unit in this file has that name", result.Entries[0].Message, StringComparison.Ordinal);
        Assert.Contains(
            $"the assessed fingerprint 000000 is stale: the unit now computes {Fingerprint(Source, "N.C.Run()")}",
            result.Entries[1].Message,
            StringComparison.Ordinal);
        Assert.Equal(Source, result.Text);
    }

    [Fact(Timeout = 600000)]
    public void An_Exempt_Or_Annotated_Unit_Is_Refused()
    {
        string annotated = Apply(Source, Assess(Source, "N.C.Run()")).Text;

        AssuranceInsertFileResult result = Apply(
            annotated,
            Assess(annotated, "N.C.Auto", index: 0),
            Assess(annotated, "N.C.Run()", index: 1));

        Assert.Equal(
            "the unit is exempt (TrivialPropertyOrAccessor) and takes no annotation",
            result.Entries[0].Message);
        Assert.Equal("the unit already carries an annotation at line 10", result.Entries[1].Message);
        Assert.Equal(annotated, result.Text);
    }

    [Fact(Timeout = 600000)]
    public void Two_Entries_For_One_Unit_Are_Both_Refused()
    {
        AssuranceInsertFileResult result = Apply(
            Source,
            Assess(Source, "N.C.Run()", index: 0),
            Assess(Source, "N.C.Run()", index: 1, resources: 3),
            Assess(Source, "N.C", index: 2));

        Assert.False(result.Entries[0].Applied);
        Assert.False(result.Entries[1].Applied);
        Assert.Contains("the input assesses this unit 2 times", result.Entries[0].Message, StringComparison.Ordinal);
        Assert.True(result.Entries[2].Applied, result.Entries[2].Message);
    }

    [Fact(Timeout = 600000)]
    public void Every_Unit_Of_A_File_Has_A_Name_An_Insert_Can_Address()
    {
        string text =
            "namespace N;\n" +
            "public sealed partial class C\n" +
            "{\n" +
            "    public int this[int i] => i * 2;\n" +
            "\n" +
            "    public int this[string s] => s.Length * 2;\n" +
            "}\n" +
            "\n" +
            "public sealed partial class C\n" +
            "{\n" +
            "}\n";

        Assert.Equal(
            ["N.C", "N.C.this[int]", "N.C.this[string]", "N.C#2"],
            Scanner.ScanFile(text, "x.cs").Units.Select(static unit => unit.Unit.Name));

        AssuranceInsertFileResult result = Apply(
            text,
            Assess(text, "N.C.this[string]"),
            Assess(text, "N.C#2", index: 1));

        Assert.True(result.Entries[0].Applied, result.Entries[0].Message);
        Assert.Equal(6, result.Entries[0].Line);
        Assert.True(result.Entries[1].Applied, result.Entries[1].Message);
        Assert.Equal(11, result.Entries[1].Line);
    }

    public static TheoryData<string, string, string> Unrepaired => new()
    {
        {
            "    // Hairs-AI:           Origin=AI; Low\n    // Hairs-Human:        PENDING\n    public void Run() { System.Console.WriteLine(); }\n",
            "N.C.Run()",
            "malformed-block: the block at line 4 does not parse: line 4 has a field with no '=': 'Low'"
        },
        {
            "    // Hairs-Human:        PENDING\n    public void Run() { System.Console.WriteLine(); }\n",
            "N.C.Run()",
            "half-block: line 4 carries an assurance line with no '// Hairs-AI:' line above it"
        },
        {
            "    [System.Obsolete]\n    // Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF\n    // Hairs-Human:        PENDING\n    public void Run() { System.Console.WriteLine(); }\n",
            "N.C.Run()",
            "below-declaration: line 5 carries an assurance comment inside the declaration, below its first line"
        },
        {
            "    public int A; public void Run() { System.Console.WriteLine(); }\n",
            "N.C.Run()",
            "not-own-line: the declaration does not start its own line"
        },
    };

    [Theory(Timeout = 600000)]
    [MemberData(nameof(Unrepaired))]
    public void A_Unit_With_A_Broken_Block_Nearby_Is_Refused(string member, string unit, string expected)
    {
        string text = "namespace N;\npublic sealed class C\n{\n" + member + "}\n";

        AssuranceInsertFileResult result = Apply(text, Assess(text, unit));

        Assert.False(result.Entries[0].Applied);
        Assert.Equal(expected + "; a human has to repair this first", result.Entries[0].Message);
        Assert.Equal(text, result.Text);
    }

    [Fact(Timeout = 600000)]
    public void A_File_The_Line_Model_Cannot_Index_Is_Refused()
    {
        string text = "namespace N;\n// a" + (char)0x2028 + "b\npublic sealed class C { }\n";

        AssuranceInsertFileResult result = Apply(text, Assess(text, "N.C"));

        Assert.False(result.Entries[0].Applied);
        Assert.StartsWith("line-model-mismatch", result.Entries[0].Message, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void The_Command_Keeps_The_Byte_Order_Mark_And_Reports_Each_Entry()
    {
        using var component = new TemporaryComponent();
        component.Project("src/A/A.csproj", assemblyName: "Probe.A");
        component.Write("hairs.config.json", """{ "projects": [ "src/A/A.csproj" ] }""");

        string formattedSource =
            "using System;\n" +
            "\n" +
            "namespace N;\n" +
            "\n" +
            "/// <summary>Doc.</summary>\n" +
            "[Serializable]\n" +
            "public sealed class C\n" +
            "{\n" +
            "    /// <summary>Runs.</summary>\n" +
            "    [Obsolete(\"x\")]\n" +
            "    public void Run() { Console.WriteLine(); }\n" +
            "\n" +
            "    public int Auto { get; set; }\n" +
            "\n" +
            "    public sealed class Inner\n" +
            "    {\n" +
            "        public int Twice(int x) { return x * 2; }\n" +
            "    }\n" +
            "}\n";

        string crlf = formattedSource.Replace("\n", "\r\n", StringComparison.Ordinal);
        component.Write("src/A/C.cs", crlf, byteOrderMark: true);
        byte[] before = component.ReadBytes("src/A/C.cs");

        string run = Fingerprint(crlf, "N.C.Run()");
        component.Write("assess.json",
            "{ \"schema\": 1, \"assessments\": [\n" +
            $"  {{ \"file\": \"src/A/C.cs\", \"unit\": \"N.C.Run()\", \"fingerprint\": \"{run}\", \"origin\": \"AI\", \"ip\": \"Low\", \"security\": \"Low\", \"resources\": 1 }},\n" +
            "  { \"file\": \"src/A/C.cs\", \"unit\": \"N.C\", \"fingerprint\": \"000000\", \"origin\": \"AI\", \"ip\": \"Low\", \"security\": \"Low\", \"resources\": 1, \"human\": \"EB\" },\n" +
            "  { \"file\": \"src/A/Missing.cs\", \"unit\": \"N.C\", \"exempt\": \"x\" }\n" +
            "] }\n");

        (int dryExit, string dryOutput, _) = component.Run(
            "insert", "--root", component.Root, "--assessments", component.PathOf("assess.json"), "--dry-run");

        Assert.Equal(1, dryExit);
        Assert.Contains("#0 would insert  src/A/C.cs:10  N.C.Run()", dryOutput, StringComparison.Ordinal);
        Assert.Equal(before, component.ReadBytes("src/A/C.cs"));

        (int exit, string output, _) = component.Run(
            "insert", "--root", component.Root, "--assessments", component.PathOf("assess.json"));

        Assert.Equal(1, exit);
        Assert.Contains("#0 inserted  src/A/C.cs:10  N.C.Run()", output, StringComparison.Ordinal);
        Assert.Contains("#1 refused  src/A/C.cs  N.C: carries \"human\"", output, StringComparison.Ordinal);
        Assert.Contains("#2 refused  src/A/Missing.cs  N.C: the file does not exist under the component root", output, StringComparison.Ordinal);
        Assert.Contains("1 applied, 2 refused", output, StringComparison.Ordinal);

        byte[] after = component.ReadBytes("src/A/C.cs");
        Assert.Equal(before.Take(3), after.Take(3));
        Assert.Equal(
            crlf.Replace(
                "    [Obsolete(\"x\")]\r\n",
                "    // Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF\r\n" +
                "    // Hairs-Human:        PENDING\r\n" +
                "    [Obsolete(\"x\")]\r\n",
                StringComparison.Ordinal),
            Encoding.UTF8.GetString(after, 3, after.Length - 3));
    }

    [Fact(Timeout = 600000)]
    public void An_Entry_Names_Its_File_And_Its_Score_As_A_Tool_Writes_Them()
    {
        using var component = new TemporaryComponent();
        component.Project("src/A/A.csproj");
        component.Write("hairs.config.json", """{ "projects": [ "src/A/A.csproj" ] }""");

        string formattedSource =
            "using System;\n" +
            "\n" +
            "namespace N;\n" +
            "\n" +
            "/// <summary>Doc.</summary>\n" +
            "[Serializable]\n" +
            "public sealed class C\n" +
            "{\n" +
            "    /// <summary>Runs.</summary>\n" +
            "    [Obsolete(\"x\")]\n" +
            "    public void Run() { Console.WriteLine(); }\n" +
            "\n" +
            "    public int Auto { get; set; }\n" +
            "\n" +
            "    public sealed class Inner\n" +
            "    {\n" +
            "        public int Twice(int x) { return x * 2; }\n" +
            "    }\n" +
            "}\n";

        component.Write("src/A/C.cs", formattedSource);

        string run = Fingerprint(formattedSource, "N.C.Run()");
        component.Write("assess.json",
            "{ \"schema\": 1, \"assessments\": [\n" +
            $"  {{ \"file\": \"./src//A\\\\C.cs\", \"unit\": \"N.C.Run()\", \"fingerprint\": \"{run}\", \"origin\": \"AI\", \"ip\": \"Low\", \"security\": \"Low\", \"resources\": 2.0 }},\n" +
            "  { \"file\": \"src/A/C.cs\", \"unit\": \"N.C.Inner\", \"fingerprint\": \"" + Fingerprint(formattedSource, "N.C.Inner") + "\", \"origin\": \"AI\", \"ip\": \"Low\", \"security\": \"Low\", \"resources\": 2.5 }\n" +
            "] }\n");

        (int exit, string output, _) = component.Run("insert", "--root", component.Root, "--assessments", component.PathOf("assess.json"));

        Assert.Equal(1, exit);
        Assert.Contains("#0 inserted  src/A/C.cs:10  N.C.Run()", output, StringComparison.Ordinal);
        Assert.Contains("#1 refused  src/A/C.cs  N.C.Inner: \"resources\" must be an integer 0 to 10", output, StringComparison.Ordinal);
        Assert.Contains("Resources=2; Fingerprint=TBF", component.Read("src/A/C.cs"), StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void An_Exemption_Or_A_Spec_That_Claims_A_Review_Is_Refused()
    {
        AssuranceInsertFileResult exempt = Apply(
            Source, new AssuranceAssessment { File = "x.cs", Unit = "N.C.Run()", Exempt = "signed-off by EB after human review, LGTM" });
        AssuranceInsertFileResult spec = Apply(Source, Assess(Source, "N.C.Run()") with { Spec = "reviewed and approved by EB" });

        Assert.False(exempt.Entries[0].Applied);
        Assert.Contains("claims a review by saying 'signed-off'", exempt.Entries[0].Message, StringComparison.Ordinal);
        Assert.False(spec.Entries[0].Applied);
        Assert.Contains("Spec=reviewed and approved by EB claims a review by saying 'approved'", spec.Entries[0].Message, StringComparison.Ordinal);
        Assert.Equal(Source, exempt.Text);
        Assert.Equal(Source, spec.Text);
    }

    [Theory(Timeout = 600000)]
    [InlineData(null, "does not exist. A component opts in")]
    [InlineData("""{ "mode": "external", "projects": [ "src/A/A.csproj" ] }""", "\"mode\": \"external\"")]
    public void Insert_Needs_An_Owned_Config(string? config, string expected)
    {
        using var component = new TemporaryComponent();
        component.Project("src/A/A.csproj");
        component.Write("src/A/C.cs", Source);
        if (config is not null)
            component.Write("hairs.config.json", config);

        component.Write("assess.json", """{ "schema": 1, "assessments": [] }""");

        (int exit, _, string error) = component.Run(
            "insert", "--root", component.Root, "--assessments", component.PathOf("assess.json"));

        Assert.Equal(2, exit);
        Assert.Contains(expected, error, StringComparison.Ordinal);
        Assert.Equal(Source, component.Read("src/A/C.cs"));
    }

    [Theory(Timeout = 600000)]
    [InlineData("""{ "assessments": [] }""", "has no \"schema\": 1")]
    [InlineData("""{ "schema": 1, "assessments": [], "approve": true }""", "\"approve\" is not a property of an assessments file")]
    [InlineData("""{ "schema": 1 }""", "no \"assessments\" array")]
    public void A_Malformed_Assessments_File_Is_A_Usage_Error(string json, string expected)
    {
        using var component = new TemporaryComponent();
        component.Project("src/A/A.csproj");
        component.Write("hairs.config.json", """{ "projects": [ "src/A/A.csproj" ] }""");
        component.Write("assess.json", json);

        (int exit, _, string error) = component.Run(
            "insert", "--root", component.Root, "--assessments", component.PathOf("assess.json"));

        Assert.Equal(2, exit);
        Assert.Contains(expected, error, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void Insert_Command_Writes_Stability_Assessment_Scores()
    {
        using var component = new TemporaryComponent();
        component.Project("src/A/A.csproj");
        component.Write("src/A/C.cs", Source);
        component.Write("hairs.config.json", """{ "projects": [ "src/A/A.csproj" ] }""");

        string assessJson = $$"""
            {
              "schema": 1,
              "assessments": [
                {
                  "file": "src/A/C.cs",
                  "unit": "N.C.Run()",
                  "fingerprint": "{{Fingerprint(Source, "N.C.Run()")}}",
                  "origin": "AI",
                  "ip": "Low",
                  "security": "Low",
                  "resources": 1,
                  "cs": 9,
                  "cl": 1
                }
              ]
            }
            """;
        component.Write("assess.json", assessJson);

        (int exit, string output, string error) = component.Run(
            "insert", "--root", component.Root, "--assessments", component.PathOf("assess.json"));

        Assert.Equal(0, exit);
        Assert.Contains("Origin=AI; IP=Low; Security=Low; Resources=1; CS=9; CL=1; Fingerprint=TBF", component.Read("src/A/C.cs"), StringComparison.Ordinal);
    }
}
