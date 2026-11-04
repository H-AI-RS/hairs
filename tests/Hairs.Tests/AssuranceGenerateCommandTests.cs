// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Hairs.Assurance;
using Hairs.Cli.Assurance;
using Xunit;

namespace Hairs.Tests;

/// <summary>
/// <c>assurance generate</c>, <c>check</c> and <c>status</c> through the command.
/// </summary>
public sealed class AssuranceGenerateCommandTests
{
    private const string Config =
        """
        {
          "schema": 1,
          "component": "Probe",
          "projects": [ "src/Probe/Probe.csproj" ],
          "spdx": { "copyright": [ "2026 HAIRS contributors" ], "license": "Apache-2.0" }
        }
        """;

    private const string Counter =
        "namespace Probe;\n" +
        "\n" +
        "// Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF\n" +
        "// Hairs-Human:        PENDING\n" +
        "public sealed class Counter\n" +
        "{\n" +
        "    public int Count { get; set; }\n" +
        "}\n";

    private static TemporaryComponent Component(string? config = Config)
    {
        var component = new TemporaryComponent();
        component.Project("src/Probe/Probe.csproj", assemblyName: "Probe");
        if (config is not null)
            component.Write("hairs.config.json", config);

        return component;
    }

    [Theory(Timeout = 600000)]
    [InlineData(null, "does not exist. A component opts in to written annotations by committing that file; generate writes nothing without it.")]
    [InlineData("""{ "mode": "external", "projects": [ "src/Probe/Probe.csproj" ], "spdx": { "copyright": [ "x" ], "license": "MIT" } }""", "\"mode\": \"external\"")]
    [InlineData("""{ "projects": [ "src/Probe/Probe.csproj" ] }""", "$.spdx is required")]
    public void Generate_Needs_An_Owned_Configuration_With_Spdx_Lines(string? config, string expected)
    {
        using TemporaryComponent component = Component(config);
        component.Write("src/Probe/Counter.cs", Counter);

        (int exit, _, string error) = component.Run("generate", "--root", component.Root);

        Assert.Equal(AssuranceCommand.UsageError, exit);
        Assert.Contains(expected, error, StringComparison.Ordinal);
        Assert.Equal(Counter, component.Read("src/Probe/Counter.cs"));
    }

    [Fact(Timeout = 600000)]
    public void Generate_Keeps_Byte_Order_Marks_And_Line_Endings_And_Is_A_Fixed_Point()
    {
        using TemporaryComponent component = Component();
        string crlf = Counter.Replace("\n", "\r\n", StringComparison.Ordinal);
        string mixed =
            "namespace Probe;\n\n" +
            "// Hairs-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF\r\n" +
            "// Hairs-Human:        PENDING\n" +
            "public enum Mode\r\n{\r\n    On,\n    Off,\r\n}";
        component.Write("src/Probe/Counter.cs", crlf, byteOrderMark: true);
        component.Write("src/Probe/Mode.cs", mixed);

        (int exit, string output, string error) = component.Run("generate", "--root", component.Root);
        Assert.True(exit == 0, error);
        Assert.Contains("5 written, 0 failed.", output, StringComparison.Ordinal);

        byte[] counter = component.ReadBytes("src/Probe/Counter.cs");
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, counter.Take(3));
        string counterText = Encoding.UTF8.GetString(counter, 3, counter.Length - 3);
        Assert.StartsWith("// SPDX-FileCopyrightText: 2026 HAIRS contributors\r\n", counterText, StringComparison.Ordinal);
        Assert.Contains("// GENERATED - DO NOT EDIT MANUALLY\r\n\r\nnamespace Probe;\r\n", counterText, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", counterText.Replace("\r\n", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);

        string modeText = component.Read("src/Probe/Mode.cs");
        string mode = AssurancePlanning.Fingerprint(mixed, ".Mode");
        Assert.EndsWith(
            "// GENERATED - DO NOT EDIT MANUALLY\n\n" + mixed.Replace("Fingerprint=TBF", "Fingerprint=" + mode, StringComparison.Ordinal),
            modeText,
            StringComparison.Ordinal);

        Dictionary<string, byte[]> before = Snapshot(component);
        (int again, string againOutput, _) = component.Run("generate", "--root", component.Root);
        Assert.Equal(0, again);
        Assert.Contains("0 artefacts to write", againOutput, StringComparison.Ordinal);
        Assert.Equal(before, Snapshot(component));

        (int check, string checkOutput, _) = component.Run("check", "--root", component.Root);
        Assert.Equal(0, check);
        Assert.Equal("Probe: no violations.\n", checkOutput.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Fact(Timeout = 600000)]
    public void Generate_Refuses_To_Replace_A_Hand_Written_Record_Unless_Told_To_Adopt_It()
    {
        using TemporaryComponent component = Component();
        component.Write("src/Probe/Counter.cs", Counter);
        component.Write("HUMAN_REVIEW.md", "# Human Review\n\nApproved for preview by a person, 2026-07-01.\n");

        (int exit, _, string error) = component.Run("generate", "--root", component.Root);

        Assert.Equal(AssuranceCommand.Refused, exit);
        Assert.Contains(
            "HUMAN_REVIEW.md exists and was not written by the generator (it carries no 'GENERATED - DO NOT EDIT MANUALLY' line)",
            error,
            StringComparison.Ordinal);
        Assert.Contains("nothing was written.", error, StringComparison.Ordinal);
        Assert.Equal(Counter, component.Read("src/Probe/Counter.cs"));
        Assert.False(File.Exists(component.PathOf("CODE-ASSURANCE.md")));

        (int checkExit, string checkOutput, _) = component.Run("check", "--root", component.Root);
        Assert.Equal(AssuranceCommand.Refused, checkExit);
        Assert.Contains("HUMAN_REVIEW.md was not written by the generator", checkOutput, StringComparison.Ordinal);

        (int adopted, _, _) = component.Run("generate", "--root", component.Root, "--adopt");
        Assert.Equal(0, adopted);
        Assert.StartsWith("# Human Review: Probe\n\nGENERATED - DO NOT EDIT MANUALLY.", component.Read("HUMAN_REVIEW.md"), StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void A_Configuration_That_Names_No_Component_Is_Titled_After_Its_Root()
    {
        using TemporaryComponent component = Component(
            """{ "projects": [ "src/Probe/Probe.csproj" ], "spdx": { "copyright": [ "2026 HAIRS contributors" ], "license": "Apache-2.0" } }""");
        component.Write("src/Probe/Counter.cs", Counter);

        (int exit, _, _) = component.Run("generate", "--root", component.Root);

        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(component.Root)));
        Assert.Equal(0, exit);
        Assert.StartsWith($"# {name} Code Assurance\n", component.Read("CODE-ASSURANCE.md"), StringComparison.Ordinal);
        Assert.StartsWith($"# Human Review: {name}\n", component.Read("HUMAN_REVIEW.md"), StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void A_Dry_Run_Writes_Nothing()
    {
        using TemporaryComponent component = Component();
        component.Write("src/Probe/Counter.cs", Counter);

        (int exit, string output, _) = component.Run("generate", "--root", component.Root, "--dry-run");

        Assert.Equal(0, exit);
        Assert.Contains("  would write src/Probe/Counter.cs\n", output.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("  would write CODE-ASSURANCE.md (new)", output, StringComparison.Ordinal);
        Assert.Equal(Counter, component.Read("src/Probe/Counter.cs"));
        Assert.False(File.Exists(component.PathOf("hairs.manifest.json")));
    }

    [Fact(Timeout = 600000)]
    public void Check_Reports_Workflow_Commands_And_Exits_One()
    {
        using TemporaryComponent component = Component();
        component.Write("src/Probe/Counter.cs", Counter.Replace("// Hairs-AI:", "// Hairs-AI: ", StringComparison.Ordinal) +
            "public static class Loose { public static int Twice(int x) => x * 2; }\n");

        (int exit, string output, string error) = component.Run("check", "--root", component.Root, "--json", "-");

        Assert.Equal(AssuranceCommand.Refused, exit);
        Assert.Contains(
            "::error file=src/Probe/Counter.cs,line=1::J5 src/Probe/Counter.cs(1) is not what the generator would write.%0A  on disk:   namespace Probe;%0A",
            error,
            StringComparison.Ordinal);
        Assert.Contains(
            "::error file=src/Probe/Counter.cs,line=5::J3 src/Probe/Counter.cs(5): Probe.Counter still records the placeholder TBF",
            error,
            StringComparison.Ordinal);
        Assert.Contains(
            "::error file=src/Probe/Counter.cs,line=9::J1 src/Probe/Counter.cs(9): Probe.Loose is relevant and carries no assurance annotation",
            error,
            StringComparison.Ordinal);
        Assert.Contains("Probe: 8 violations: J1 2, J3 1, J5 4, J7 1.", error, StringComparison.Ordinal);

        using JsonDocument report = JsonDocument.Parse(output);
        Assert.Equal(8, report.RootElement.GetProperty("violationCount").GetInt32());
        Assert.Equal(2, report.RootElement.GetProperty("rules").GetProperty("J1").GetInt32());
    }

    [Fact(Timeout = 600000)]
    public void Check_Release_Also_Reports_Unresolved_Units()
    {
        using TemporaryComponent component = Component();
        component.Write("src/Probe/Counter.cs", Counter);
        Assert.Equal(0, component.Run("generate", "--root", component.Root).Exit);

        (int exit, string output, _) = component.Run("check", "--root", component.Root, "--release");

        Assert.Equal(AssuranceCommand.Refused, exit);
        Assert.Contains(
            "::error file=src/Probe/Counter.cs,line=22::J11 src/Probe/Counter.cs(22): Probe.Counter is HUMAN_PENDING and its human line reads 'PENDING'",
            output,
            StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void Generated_Artefacts_Keep_The_Line_Endings_A_Checkout_Gave_Them()
    {
        using TemporaryComponent component = Component();
        component.Write("src/Probe/Counter.cs", Counter);
        Assert.Equal(0, component.Run("generate", "--root", component.Root).Exit);

        foreach (string artefact in new[] { "CODE-ASSURANCE.md", "HUMAN_REVIEW.md", "hairs.manifest.json" })
            component.Write(artefact, component.Read(artefact).Replace("\n", "\r\n", StringComparison.Ordinal));

        Assert.Equal(0, component.Run("check", "--root", component.Root).Exit);

        component.Write("src/Probe/Counter.cs", component.Read("src/Probe/Counter.cs").Replace(
            "    public int Count { get; set; }\n", "    public int Count { get; set; }\n\n    public int Limit;\n", StringComparison.Ordinal));
        Assert.Equal(0, component.Run("generate", "--root", component.Root).Exit);

        string report = component.Read("CODE-ASSURANCE.md");
        Assert.Contains("| Code units | 3 |\r\n", report, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", report.Replace("\r\n", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Equal(0, component.Run("check", "--root", component.Root).Exit);
    }

    [Fact(Timeout = 600000)]
    public void Status_Summarizes_The_Tree_As_It_Is()
    {
        using TemporaryComponent component = Component();
        component.Write("src/Probe/Counter.cs", Counter + "public static class Loose { }\n");

        (int exit, string output, _) = component.Run("status", "--root", component.Root);

        Assert.Equal(0, exit);
        Assert.Equal(
            "Probe: 1 covered files (0 not covered), 3 code units, 2 relevant, 1 exempt\n" +
            "  Annotated:       1 of 2 (50%)\n" +
            "  Human-reviewed:  0 of 2 (0%)\n" +
            "  Human line:      1 PENDING, 0 STALE\n" +
            "  States:          NEW 1, AI_ASSESSED 1, HUMAN_PENDING 0, HUMAN_APPROVED_PENDING_FINGERPRINT 0, VERIFIED 0, STALE 0, EXEMPT 1\n" +
            "  Security:        None 0, Low 1, Medium 0, High 0, Critical 0, not assessed 1\n" +
            "  Generate:        would write 4 artefacts\n",
            output.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Fact(Timeout = 600000)]
    public void Check_Reads_A_Configuration_Named_On_The_Command_Line()
    {
        using TemporaryComponent component = Component(config: null);
        component.Write("src/Probe/Counter.cs", Counter);
        component.Write("elsewhere/hairs.json", Config);

        (int missing, _, string error) = component.Run("check", "--root", component.Root);
        Assert.Equal(AssuranceCommand.UsageError, missing);
        Assert.Contains("check needs it, or --config naming one.", error, StringComparison.Ordinal);

        (int exit, string output, _) = component.Run(
            "check", "--root", component.Root, "--config", component.PathOf("elsewhere/hairs.json"));
        Assert.Equal(AssuranceCommand.Refused, exit);
        Assert.Contains("Probe: 6 violations: J3 1, J5 4, J7 1.", output, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void The_Report_Names_A_Hand_Written_Review_Record_It_Does_Not_Read()
    {
        using TemporaryComponent component = Component(
            Config.Replace("\"spdx\"", "\"artefacts\": { \"humanReview\": \"HUMAN_REVIEW.units.md\" },\n  \"spdx\"", StringComparison.Ordinal));
        component.Write("src/Probe/Counter.cs", Counter);
        const string Signed = "# Human Review\n\nStatus: APPROVED WITH CONDITIONS for first preview use. Reviewer: a person.\n";
        component.Write("HUMAN_REVIEW.md", Signed);

        (int exit, _, string error) = component.Run("generate", "--root", component.Root);
        Assert.True(exit == 0, error);

        string report = component.Read("CODE-ASSURANCE.md");
        Assert.Contains("**No code unit in this component carries a decision on its human line yet.**", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Nothing in this component has been reviewed", report, StringComparison.Ordinal);
        Assert.Contains("`HUMAN_REVIEW.md` is a separate, hand-written review record of this component.", report, StringComparison.Ordinal);
        Assert.Equal(Signed, component.Read("HUMAN_REVIEW.md"));
        Assert.StartsWith("# Human Review: Probe", component.Read("HUMAN_REVIEW.units.md"), StringComparison.Ordinal);
        Assert.Equal(0, component.Run("check", "--root", component.Root).Exit);
    }

    [Fact(Timeout = 600000)]
    public void A_File_In_Build_Output_Below_The_Project_Root_Is_Named_As_Not_Covered()
    {
        using TemporaryComponent component = Component();
        component.Write("src/Probe/Counter.cs", Counter);
        component.Write("src/Probe/Internal/obj/Gate.cs", "namespace Probe;\ninternal static class Gate { internal static bool Open(int key) => key == 4242; }\n");
        component.Write("src/Probe/Vendor/.git", "gitdir: ../../../.git/modules/Vendor\n");
        component.Write("src/Probe/Vendor/Backdoor.cs", "namespace Probe;\npublic static class Backdoor { }\n");

        (_, string status, _) = component.Run("status", "--root", component.Root);
        Assert.StartsWith("Probe: 1 covered files (2 not covered)", status, StringComparison.Ordinal);

        Assert.Equal(0, component.Run("generate", "--root", component.Root).Exit);
        string report = component.Read("CODE-ASSURANCE.md");
        Assert.Contains("| Files not covered | 2 |", report, StringComparison.Ordinal);
        Assert.Contains("| `src/Probe/Internal/obj/Gate.cs` | inside the build output directory 'src/Probe/Internal/obj/'", report, StringComparison.Ordinal);
        Assert.Contains("| `src/Probe/Vendor/` | a nested checkout", report, StringComparison.Ordinal);
        Assert.DoesNotContain("No file under a covered project's directory", report, StringComparison.Ordinal);
        Assert.Equal(Encoding.UTF8.GetBytes("namespace Probe;\npublic static class Backdoor { }\n"), component.ReadBytes("src/Probe/Vendor/Backdoor.cs"));
    }

    [Theory(Timeout = 600000)]
    [InlineData("\"artefacts\": { \"report\": \".git/hooks/pre-commit\" }", "names a path inside a '.git' directory")]
    [InlineData("\"artefacts\": { \"humanReview\": \"Other/HUMAN_REVIEW.md\" }", "lies inside 'Other', which holds a .git entry")]
    [InlineData("\"component\": \"Probe\\necho owned\\n#\"", "$.component must be one line")]
    public void Generate_Writes_Only_Inside_The_Components_Own_Tree(string property, string expected)
    {
        using TemporaryComponent component = Component(Config.Replace("\"projects\"", property + ",\n  \"projects\"", StringComparison.Ordinal));
        component.Write("src/Probe/Counter.cs", Counter);
        component.Write("Other/.git", "gitdir: ../.git/modules/Other\n");
        Dictionary<string, byte[]> before = Snapshot(component);

        (int exit, _, string error) = component.Run("generate", "--root", component.Root);

        Assert.Equal(AssuranceCommand.UsageError, exit);
        Assert.Contains(expected, error, StringComparison.Ordinal);
        Assert.Equal(before, Snapshot(component));
    }

    [Fact(Timeout = 600000)]
    public void A_Json_Target_That_Cannot_Be_Written_Is_Refused_Before_Anything_Is_Written()
    {
        using TemporaryComponent component = Component();
        component.Write("src/Probe/Counter.cs",
            "namespace Probe;\n\npublic static class Loose\n{\n    public static int Twice(int x) => x * 2;\n}\n");
        component.Write("list.json", "{}\n");
        component.Write("assess.json",
            "{ \"schema\": 1, \"assessments\": [ { \"file\": \"src/Probe/Counter.cs\", \"unit\": \"Probe.Loose\", \"exempt\": \"a generated shim\" } ] }\n");
        Dictionary<string, byte[]> before = Snapshot(component);

        (int exit, _, string error) = component.Run(
            "insert", "--root", component.Root, "--assessments", component.PathOf("assess.json"), "--json", component.PathOf("list.json/r.json"));

        Assert.Equal(AssuranceCommand.UsageError, exit);
        Assert.Contains("--json: '", error, StringComparison.Ordinal);
        Assert.Contains("cannot be written", error, StringComparison.Ordinal);
        Assert.Equal(before, Snapshot(component));

        Assert.Equal(AssuranceCommand.UsageError, component.Run("check", "--root", component.Root, "--json", component.PathOf("list.json/c.json")).Exit);
    }

    [Fact(Timeout = 600000)]
    public void Check_Annotations_Carry_Their_Remedy_A_Prefix_And_A_Limit()
    {
        using TemporaryComponent component = Component();
        component.Write("src/Probe/Loose.cs",
            "namespace Probe;\n\npublic static class Loose\n{\n    public static int Twice(int x) => x * 2;\n\n    public static int Thrice(int x) => x * 3;\n}\n");
        Assert.Equal(0, component.Run("generate", "--root", component.Root).Exit);

        (int exit, string output, _) = component.Run(
            "check", "--root", component.Root, "--annotation-prefix", "Hairs.Probe/", "--annotation-limit", "1");

        Assert.Equal(AssuranceCommand.Refused, exit);
        string[] lines = output.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        string j1 = Assert.Single(lines, static line => line.StartsWith("::error", StringComparison.Ordinal) && line.Contains("::J1 ", StringComparison.Ordinal));
        Assert.StartsWith(
            "::error file=Hairs.Probe/src/Probe/Loose.cs,line=20::J1 src/Probe/Loose.cs(20): Probe.Loose is relevant and carries no assurance annotation" +
            "%0A  Run: hairs assurance list prints what the unit needs",
            j1,
            StringComparison.Ordinal);
        Assert.Contains("Probe: 2 further J1 violations are not shown as annotations (--annotation-limit 1).", lines);
        Assert.Contains("Probe: 3 violations: J1 3.", lines);
    }

    [Fact(Timeout = 600000)]
    public void Generate_Reports_Every_Refusal_Together()
    {
        using TemporaryComponent component = Component();
        component.Write("src/Probe/Counter.cs", "// SPDX-FileCopyrightText: 2009 A Person\n// SPDX-License-Identifier: BSD-3-Clause\n\n" + Counter);
        component.Write("HUMAN_REVIEW.md", "# Human Review\n\nSigned by a person.\n");

        (int exit, _, string error) = component.Run("generate", "--root", component.Root);

        Assert.Equal(AssuranceCommand.Refused, exit);
        Assert.Contains(
            "src/Probe/Counter.cs opens with a comment run the generator did not write (line 1: '// SPDX-FileCopyrightText: 2009 A Person'). " +
            "It will neither delete that run nor stack a second licence header above it: state exactly the run's lines in spdx or in " +
            "an spdxOverrides entry for this file, so that the generated header replaces the run, or exclude the file.",
            error,
            StringComparison.Ordinal);
        Assert.Contains("HUMAN_REVIEW.md exists and was not written by the generator", error, StringComparison.Ordinal);
        Assert.EndsWith("nothing was written.", error.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void No_Command_Is_A_Usage_Error_On_Standard_Error()
    {
        using var component = new TemporaryComponent();

        (int exit, string output, string error) = component.Run();

        Assert.Equal(AssuranceCommand.UsageError, exit);
        Assert.Empty(output);
        Assert.StartsWith("Usage:", error, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void The_Programs_Json_On_Standard_Output_Is_Utf8()
    {
        string cli = Path.Combine(AppContext.BaseDirectory, "Hairs.Cli.dll");
        Assert.True(File.Exists(cli), cli);

        using TemporaryComponent component = Component();
        component.Write("src/Probe/Maß.cs", "namespace Probe;\npublic sealed class Maß { public void Run() { System.Console.WriteLine(); } }\n");

        var start = new System.Diagnostics.ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in new[] { cli, "assurance", "list", "--root", component.Root, "--json", "-" })
            start.ArgumentList.Add(argument);

        using var process = System.Diagnostics.Process.Start(start)!;
        using var bytes = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(bytes);
        string errors = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0, errors);
        string json = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes.ToArray());
        using JsonDocument report = JsonDocument.Parse(json);
        Assert.Contains(
            report.RootElement.GetProperty("units").EnumerateArray(),
            static unit => unit.GetProperty("unit").GetString() == "Probe.Maß" && unit.GetProperty("file").GetString() == "src/Probe/Maß.cs");
    }

    [Fact(Timeout = 600000)]
    public void A_Workflow_Command_Escapes_What_The_Runner_Would_Misread()
    {
        Assert.Equal(
            "::error file=a%3Ab%2Cc.cs,line=3::J2 50%25 done%0Anext",
            AssuranceCommand.WorkflowCommand(new AssuranceViolation("J2", "a:b,c.cs", 3, "50% done\nnext")));
        Assert.Equal("::error::J7 whole", AssuranceCommand.WorkflowCommand(new AssuranceViolation("J7", null, null, "whole")));
    }

    private static Dictionary<string, byte[]> Snapshot(TemporaryComponent component) =>
        Directory.EnumerateFiles(component.Root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(component.Root, path), File.ReadAllBytes);
}
