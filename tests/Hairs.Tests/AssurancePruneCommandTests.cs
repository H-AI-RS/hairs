// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Hairs.Assurance;
using Hairs.Cli.Assurance;
using Hairs.Language.CSharp;
using Xunit;

namespace Hairs.Tests;

/// <summary>
/// <c>assurance prune</c>, and the named values a component watches.
/// </summary>
public sealed class AssurancePruneCommandTests
{
    private const string Config =
        """
        {
          "schema": 1,
          "component": "Probe",
          "projects": [ "src/Probe/Probe.csproj" ],
          "namedValues": "watched",
          "spdx": { "copyright": [ "2026 HAIRS contributors" ], "license": "Apache-2.0" }
        }
        """;

    private static readonly string[] Native =
    [
        "namespace Probe;",
        "",
        "/// <summary>Values.</summary>",
        "// Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=1; Fingerprint=TBF",
        "// Hairs-Falsified-If: a path outside the component is deleted",
        "// Hairs-Human:        PENDING",
        "public static class Native",
        "{",
        "    /// <summary>The attribute list terminator.</summary>",
        "    // Hairs-AI:           Origin=AI; IP=None; Security=Critical; Resources=0; Fingerprint=TBF",
        "    // Hairs-Falsified-If: EGL_NONE is not 0x3038, so attribute lists end early",
        "    // Hairs-Human:        PENDING",
        "    public const int EGL_NONE = 0x3038;",
        "",
        "    // Hairs-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF",
        "    // Hairs-Human:        PENDING",
        "    public static readonly System.IntPtr NoDisplay = System.IntPtr.Zero;",
        "",
        "    // Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF",
        "    // Hairs-Falsified-If: a count below zero is returned",
        "    // Hairs-Human:        PENDING",
        "    public static int Count(int[] items) => items.Length + 0;",
        "",
        "    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=1; Fingerprint=TBF",
        "    // Hairs-Falsified-If: a path outside the component is deleted",
        "    // Hairs-Human:        PENDING",
        "    public static void Wipe(string path) { System.IO.File.Delete(path); }",
        "}",
    ];

    private static readonly int[] Removed = [9, 10, 11, 14, 15, 19];

    private static TemporaryComponent Component(string? config = Config)
    {
        var component = new TemporaryComponent();
        component.Project("src/Probe/Probe.csproj", assemblyName: "Probe");
        if (config is not null)
            component.Write("hairs.config.json", config);

        return component;
    }

    private static string Text(IEnumerable<int> indices, Func<int, string> ending) =>
        string.Concat(indices.Select(index => Native[index] + ending(index)));

    private static Func<int, string> Endings(string kind) => kind switch
    {
        "lf" => static _ => "\n",
        "crlf" => static _ => "\r\n",
        _ => static index => index % 2 == 0 ? "\r\n" : "\n",
    };

    [Theory(Timeout = 600000)]
    [InlineData("lf", false)]
    [InlineData("crlf", false)]
    [InlineData("mixed", false)]
    [InlineData("lf", true)]
    [InlineData("mixed", true)]
    public void Prune_Removes_Whole_Lines_And_Keeps_Every_Other_Byte(string endings, bool byteOrderMark)
    {
        using TemporaryComponent component = Component();
        Func<int, string> ending = Endings(endings);
        string original = Text(Enumerable.Range(0, Native.Length), ending);
        string expected = Text(Enumerable.Range(0, Native.Length).Except(Removed), ending);
        component.Write("src/Probe/Native.cs", original, byteOrderMark);

        (int exit, string output, string error) = component.Run("prune", "--root", component.Root);

        Assert.True(exit == 0, output + error);
        byte[] written = component.ReadBytes("src/Probe/Native.cs");
        Assert.Equal(AssuranceSourceText.Encode(expected, byteOrderMark), written);

        var scanner = new CSharpAssuranceFileScanner(null, AssuranceExemptionPredicate.Strict, AssuranceNamedValues.Watched);
        AssuranceScannedFile before = scanner.ScanFile(original, "src/Probe/Native.cs");
        AssuranceScannedFile after = scanner.ScanFile(expected, "src/Probe/Native.cs");
        Assert.Null(AssurancePruning.CodeDifference(before, after));
        Assert.Equal(
            before.Units.Select(static unit => (unit.Unit.Name, unit.Unit.Fingerprint, unit.Unit.Exemption)),
            after.Units.Select(static unit => (unit.Unit.Name, unit.Unit.Fingerprint, unit.Unit.Exemption)));

        string text = output.Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains("Probe: 1 covered files; 2 blocks and 1 criteria removed from 1 files, 0 left in place, 0 files with a problem\n", text, StringComparison.Ordinal);
        Assert.Contains("  removed block at line 10  Probe.Native.EGL_NONE: exempt: NamedValue\n", text, StringComparison.Ordinal);
        Assert.Contains("  removed block at line 15  Probe.Native.NoDisplay: exempt: NamedValue\n", text, StringComparison.Ordinal);
        Assert.Contains("  removed criterion at line 20  Probe.Native.Count(int[]): Security=Low\n", text, StringComparison.Ordinal);
        Assert.Contains("Run: hairs assurance generate", text, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void A_Block_A_Person_Wrote_On_Is_Left_And_Reported()
    {
        using TemporaryComponent component = Component();
        string[] lines =
        [
            "namespace Probe;",
            "",
            "public static class Kept",
            "{",
            "    // Hairs-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF",
            "    // Hairs-Human:        Tester",
            "    public const int Named = 1;",
            "",
            "    // Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF",
            "    // Hairs-Falsified-If: a result other than twice the input is returned",
            "    // Hairs-Human:        STALE; Previous=Tester@123456",
            "    public static int Twice(int x) => x * 2;",
            "",
            "    // Hairs-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF",
            "    // Hairs-Human:        PENDING",
            "    public const int Gone = 2;",
            "}",
        ];
        component.Write("src/Probe/Kept.cs", string.Concat(lines.Select(static line => line + "\n")));

        (int exit, string output, _) = component.Run("prune", "--root", component.Root, "--json", "-");

        Assert.Equal(AssuranceCommand.Refused, exit);
        Assert.Equal(
            string.Concat(lines.Where(static (_, index) => index is not (13 or 14)).Select(static line => line + "\n")),
            component.Read("src/Probe/Kept.cs"));

        using JsonDocument report = JsonDocument.Parse(output);
        JsonElement root = report.RootElement;
        Assert.Equal(1, root.GetProperty("blocksRemoved").GetInt32());
        Assert.Equal(0, root.GetProperty("criteriaRemoved").GetInt32());
        Assert.Equal(2, root.GetProperty("left").GetInt32());

        JsonElement[] entries = [.. Assert.Single(root.GetProperty("files").EnumerateArray()).GetProperty("entries").EnumerateArray()];
        Assert.Equal(
            [
                ("Probe.Kept.Named", 5, "block", false),
                ("Probe.Kept.Twice(int)", 10, "criterion", false),
                ("Probe.Kept.Gone", 14, "block", true),
            ],
            entries.Select(static entry => (
                entry.GetProperty("unit").GetString(),
                entry.GetProperty("line").GetInt32(),
                entry.GetProperty("kind").GetString(),
                entry.GetProperty("removed").GetBoolean())));
        Assert.Contains("its human line reads 'Tester'", entries[0].GetProperty("reason").GetString(), StringComparison.Ordinal);
        Assert.Contains("its human line reads 'STALE; Previous=Tester@123456'", entries[1].GetProperty("reason").GetString(), StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void A_Dry_Run_Writes_Nothing()
    {
        using TemporaryComponent component = Component();
        component.Write("src/Probe/Native.cs", Text(Enumerable.Range(0, Native.Length), Endings("lf")));
        Dictionary<string, byte[]> before = Snapshot(component);

        (int exit, string output, _) = component.Run("prune", "--root", component.Root, "--dry-run");

        Assert.Equal(0, exit);
        Assert.Equal(before, Snapshot(component));
        Assert.Contains("2 blocks and 1 criteria would be removed from 1 files", output, StringComparison.Ordinal);
    }

    [Theory(Timeout = 600000)]
    [InlineData(null, "does not exist. A component opts in to written annotations by committing that file; prune writes nothing without it.")]
    [InlineData("""{ "mode": "external", "projects": [ "src/Probe/Probe.csproj" ], "namedValues": "watched" }""", "\"mode\": \"external\"")]
    public void Prune_Needs_An_Owned_Configuration(string? config, string expected)
    {
        using TemporaryComponent component = Component(config);
        component.Write("src/Probe/Native.cs", Text(Enumerable.Range(0, Native.Length), Endings("lf")));
        Dictionary<string, byte[]> before = Snapshot(component);

        (int exit, _, string error) = component.Run("prune", "--root", component.Root);

        Assert.Equal(AssuranceCommand.UsageError, exit);
        Assert.Contains(expected, error, StringComparison.Ordinal);
        Assert.Equal(before, Snapshot(component));
    }

    [Fact(Timeout = 600000)]
    public void Generate_And_Check_Are_Clean_After_Prune()
    {
        using TemporaryComponent component = Component();
        component.Write("src/Probe/Native.cs", Text(Enumerable.Range(0, Native.Length), Endings("crlf")), byteOrderMark: true);

        (int first, _, string firstError) = component.Run("generate", "--root", component.Root);
        Assert.True(first == 0, firstError);
        string? arraysBefore = AssuranceManifest.ArraysOf(component.Read("hairs.manifest.json"));

        (int pruned, string pruneOutput, string pruneError) = component.Run("prune", "--root", component.Root);
        Assert.True(pruned == 0, pruneOutput + pruneError);
        Assert.Equal(AssuranceCommand.Refused, component.Run("check", "--root", component.Root).Exit);

        (int second, _, string secondError) = component.Run("generate", "--root", component.Root);
        Assert.True(second == 0, secondError);

        (int check, string checkOutput, _) = component.Run("check", "--root", component.Root);
        Assert.True(check == 0, checkOutput);
        Assert.Equal("Probe: no violations.\n", checkOutput.Replace("\r\n", "\n", StringComparison.Ordinal));

        string manifest = component.Read("hairs.manifest.json");
        Assert.Equal(arraysBefore, AssuranceManifest.ArraysOf(manifest));
        Assert.Contains(
            "\"name\": \"Probe.Native.EGL_NONE\", \"file\": \"src/Probe/Native.cs\", \"exempt\": true, \"exemption\": \"NamedValue\"",
            manifest,
            StringComparison.Ordinal);

        string report = component.Read("CODE-ASSURANCE.md").Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains("| EnumMemberOfADeclaredVocabulary | 0 |\n| NamedValue | 2 |\n| DeclaredInSource | 0 |\n", report, StringComparison.Ordinal);
        Assert.Contains("`NamedValue` is this component's choice (`\"namedValues\": \"watched\"`)", report, StringComparison.Ordinal);

        string source = component.Read("src/Probe/Native.cs");
        Assert.Contains("// Exempt:           2\r\n", source, StringComparison.Ordinal);
        Assert.Contains("// Criteria:         2/2\r\n", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EGL_NONE is not 0x3038", source, StringComparison.Ordinal);
        Assert.Contains("a path outside the component is deleted", source, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void A_Component_That_Reviews_Named_Values_Sees_No_Difference()
    {
        using TemporaryComponent component = Component(Config.Replace("\"namedValues\": \"watched\",", string.Empty, StringComparison.Ordinal));
        component.Write("src/Probe/Native.cs", Text(Enumerable.Range(0, Native.Length), Endings("lf")));

        Assert.Equal(0, component.Run("generate", "--root", component.Root).Exit);
        string report = component.Read("CODE-ASSURANCE.md");
        Assert.Contains("| EnumMemberOfADeclaredVocabulary | 0 |\n| DeclaredInSource | 0 |\n", report, StringComparison.Ordinal);
        Assert.DoesNotContain("NamedValue", report, StringComparison.Ordinal);
        Assert.DoesNotContain("NamedValue", component.Read("hairs.manifest.json"), StringComparison.Ordinal);

        (int exit, string output, _) = component.Run("prune", "--root", component.Root, "--dry-run");
        Assert.Equal(0, exit);
        Assert.Contains("0 blocks and 1 criteria would be removed", output, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void A_File_The_Line_Model_Cannot_Index_Is_Left_As_It_Is()
    {
        var scanner = new CSharpAssuranceFileScanner(null, AssuranceExemptionPredicate.Strict, AssuranceNamedValues.Watched);
        string unannotated =
            "namespace Probe;\n\npublic static class S\n{\n    public const string Separator = @\"" + (char)0x2028 + "\";\n}\n";
        string annotated = unannotated.Replace(
            "    public const",
            "    // Hairs-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF\n    // Hairs-Human:        PENDING\n    public const",
            StringComparison.Ordinal);

        AssurancePruneFileResult bare = AssurancePruning.Apply(unannotated, "x.cs", scanner);
        Assert.Null(bare.Problem);
        Assert.Empty(bare.Entries);

        AssurancePruneFileResult refused = AssurancePruning.Apply(annotated, "x.cs", scanner);
        Assert.Contains("breaks lines on U+0085, U+2028 or U+2029", refused.Problem, StringComparison.Ordinal);
        Assert.Equal(annotated, refused.Text);
        Assert.False(refused.Changed);
    }

    [Fact(Timeout = 600000)]
    public void A_File_That_Would_Not_Scan_The_Same_Is_Left_And_Its_Reason_Reads_Once()
    {
        using TemporaryComponent component = Component();
        const string Stacked =
            "namespace Probe;\n" +
            "\n" +
            "public static class C\n" +
            "{ \n" +
            "    // Hairs-AI:           Origin=AI; IP=None; Security=Critical; Resources=0; Fingerprint=TBF\n" +
            "    // Hairs-Falsified-If: X is not 1\n" +
            "    // Hairs-Human:        PENDING\n" +
            "    // Hairs-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF\n" +
            "    // Hairs-Human:        Tester\n" +
            "    public const int X = 1;\n" +
            "}\n";
        component.Write("src/Probe/C.cs", Stacked);

        (int exit, string output, string error) = component.Run("prune", "--root", component.Root, "--json", "-");

        Assert.Equal(AssuranceCommand.Refused, exit);
        Assert.Equal(Stacked, component.Read("src/Probe/C.cs"));

        const string Reason =
            "did not scan the same after removing (Probe.C.X still carries an assurance line after its block was removed)";
        using JsonDocument report = JsonDocument.Parse(output);
        JsonElement file = Assert.Single(report.RootElement.GetProperty("files").EnumerateArray());
        Assert.Equal($"the file {Reason}; nothing was removed from it", file.GetProperty("problem").GetString());
        Assert.Equal(
            $"not removed: the file {Reason}",
            Assert.Single(file.GetProperty("entries").EnumerateArray()).GetProperty("reason").GetString());
        Assert.DoesNotContain("the file the file", error, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void Insert_Refuses_To_Assess_A_Watched_Named_Value()
    {
        using TemporaryComponent component = Component();
        const string Values = "namespace Probe;\n\npublic static class Values\n{\n    public const int EGL_NONE = 0x3038;\n}\n";
        component.Write("src/Probe/Values.cs", Values);
        string fingerprint = AssurancePlanning.Fingerprint(Values, ".EGL_NONE");
        component.Write("assess.json",
            "{ \"schema\": 1, \"assessments\": [ { \"file\": \"src/Probe/Values.cs\", \"unit\": \"Probe.Values.EGL_NONE\", " +
            $"\"fingerprint\": \"{fingerprint}\", \"origin\": \"AI\", \"ip\": \"None\", \"security\": \"None\", \"resources\": 0 }} ] }}\n");

        (int exit, string output, _) = component.Run("insert", "--root", component.Root, "--assessments", component.PathOf("assess.json"));

        Assert.Equal(AssuranceCommand.Refused, exit);
        Assert.Contains("the unit is exempt (NamedValue) and takes no annotation", output, StringComparison.Ordinal);
        Assert.Equal(Values, component.Read("src/Probe/Values.cs"));
    }

    private static Dictionary<string, byte[]> Snapshot(TemporaryComponent component) =>
        Directory.EnumerateFiles(component.Root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(component.Root, path), File.ReadAllBytes);
}
