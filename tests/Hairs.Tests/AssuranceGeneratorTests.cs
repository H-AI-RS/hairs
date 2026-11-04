// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using Hairs.Assurance;
using static Hairs.Tests.AssurancePlanning;

namespace Hairs.Tests;

/// <summary>
/// Generator tests: human-line transitions, fixed point, header placement and formatting.
/// </summary>
public sealed class AssuranceGeneratorTests
{
    private static string Probe(string humanBody, string aiFingerprint = "TBF", string security = "Low", string? criterion = null) =>
        "namespace Probe;\n" +
        "\n" +
        "public static class Folds\n" +
        "{\n" +
        $"    // Hairs-AI:           Origin=AI; IP=Low; Security={security}; Resources=3; Fingerprint={aiFingerprint}\n" +
        (criterion is null ? string.Empty : $"    // Hairs-Falsified-If: {criterion}\n") +
        $"    // Hairs-Human:        {humanBody}\n" +
        "    public static int Fold(int[] values)\n" +
        "    {\n" +
        "        int total = 0;\n" +
        "        foreach (int value in values)\n" +
        "            total += value;\n" +
        "\n" +
        "        return total;\n" +
        "    }\n" +
        "}\n";

    private static string FoldFingerprint => Fingerprint(Probe("PENDING"), ".Fold(int[])");

    private static string Rewritten(string text, string marker) =>
        LineWith(Desired(Plan(("x.cs", text)), "x.cs"), marker).TrimStart();

    [Fact(Timeout = 600000)]
    public void Pending_Stays_Pending_And_The_Machine_Field_Is_Filled()
    {
        string text = Probe("PENDING");

        Assert.Equal("// Hairs-Human:        PENDING", Rewritten(text, "// Hairs-Human:"));
        Assert.Equal(
            $"// Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=3; Fingerprint={FoldFingerprint}",
            Rewritten(text, "// Hairs-AI:"));
        Assert.Equal(AssuranceUnitState.HumanPending, After(Plan(("x.cs", text)), ".Fold(int[])").State);
    }

    [Theory(Timeout = 600000)]
    [InlineData("EB")]
    [InlineData("EB; Fingerprint=TBF")]
    [InlineData("  EB ;Fingerprint=TBF ")]
    [InlineData("Maik Ratzmer")]
    public void A_Reviewer_Who_Left_The_Fingerprint_Gets_It_Filled(string body)
    {
        AssurancePlan plan = Plan(("x.cs", Probe(body, aiFingerprint: FoldFingerprint)));
        string reviewer = body.Split(';')[0].Trim();

        Assert.Equal($"// Hairs-Human:        {reviewer}; Fingerprint={FoldFingerprint}", LineWith(Desired(plan, "x.cs"), "// Hairs-Human:").TrimStart());
        Assert.Equal(AssuranceUnitState.Verified, After(plan, ".Fold(int[])").State);
        Assert.Contains("// Human-reviewed:   1/2\n", Desired(plan, "x.cs"), StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void A_Bare_Approval_Of_Code_That_Moved_Since_The_Last_Generation_Becomes_Stale()
    {
        string moved = Probe("EB", aiFingerprint: FoldFingerprint)
            .Replace("        return total;\n", "        System.IO.File.Delete(\"C:/important\");\n        return total;\n", StringComparison.Ordinal);
        AssurancePlan plan = Plan(("x.cs", moved));

        Assert.Empty(plan.Problems);
        Assert.Equal($"// Hairs-Human:        STALE; Previous=EB@{FoldFingerprint}", LineWith(Desired(plan, "x.cs"), "// Hairs-Human:").TrimStart());
        Assert.Equal(AssuranceUnitState.Stale, After(plan, ".Fold(int[])").State);
    }

    [Fact(Timeout = 600000)]
    public void A_Bare_Approval_Before_Any_Generation_Is_Refused()
    {
        string text = Probe("EB");
        AssurancePlan plan = Plan(("x.cs", text));

        AssuranceViolation refusal = Assert.Single(plan.Problems);
        Assert.Equal("J4", refusal.Rule);
        Assert.StartsWith(
            "The assurance generator will not bind the approval on x.cs(7): Probe.Folds.Fold(int[]) to a version: its " +
            "human line reads 'EB' and its machine line records Fingerprint=TBF, so nothing says which version EB approved.",
            refusal.Message,
            StringComparison.Ordinal);
        Assert.Equal(text, Desired(plan, "x.cs"));
    }

    [Theory(Timeout = 600000)]
    [InlineData("NOT REVIEWED")]
    [InlineData("Pending")]
    [InlineData("PENDING\u200B")]
    [InlineData("TODO")]
    [InlineData("n/a")]
    [InlineData("not-reviewed; Fingerprint=TBF")]
    public void A_Placeholder_On_The_Human_Line_Is_Refused_And_Never_Verified(string body)
    {
        string text = Probe(body, aiFingerprint: FoldFingerprint);
        AssurancePlan plan = Plan(("x.cs", text));

        AssuranceViolation refusal = Assert.Single(plan.Problems);
        Assert.Equal("J4", refusal.Rule);
        Assert.Contains("will not rewrite the human line", refusal.Message, StringComparison.Ordinal);
        Assert.Equal(text, Desired(plan, "x.cs"));
        Assert.NotEqual(AssuranceUnitState.Verified, After(plan, ".Fold(int[])").State);
        Assert.Null(After(plan, ".Fold(int[])").Annotation!.Reviewer);
    }

    [Fact(Timeout = 600000)]
    public void A_Reviewers_Own_Assessment_Is_Carried_Through()
    {
        Assert.Equal(
            $"// Hairs-Human:        EB; Security=High; Resources=1; Fingerprint={FoldFingerprint}",
            Rewritten(Probe("EB; Fingerprint=TBF; Security=High; Resources=1", aiFingerprint: FoldFingerprint), "// Hairs-Human:"));
        Assert.Equal(
            $"// Hairs-Human:        EB; CS=10; CL=0; Fingerprint={FoldFingerprint}",
            Rewritten(Probe("EB; Fingerprint=TBF; CS=10; CL=0", aiFingerprint: FoldFingerprint), "// Hairs-Human:"));
    }

    [Fact(Timeout = 600000)]
    public void An_Approval_The_Code_Has_Outrun_Becomes_Stale_With_The_Previous_Reviewer()
    {
        string text = Probe("EB; Security=High; Fingerprint=112233");
        AssurancePlan plan = Plan(("x.cs", text));

        Assert.Equal("// Hairs-Human:        STALE; Previous=EB@112233", LineWith(Desired(plan, "x.cs"), "// Hairs-Human:").TrimStart());
        Assert.Equal(AssuranceUnitState.Stale, After(plan, ".Fold(int[])").State);
        Assert.Empty(plan.Problems);
    }

    [Fact(Timeout = 600000)]
    public void A_Stale_Line_Is_Not_Cleared_By_Regeneration()
    {
        string text = Probe("STALE; Previous=EB@112233", aiFingerprint: FoldFingerprint);
        AssurancePlan plan = Plan(("x.cs", text));

        Assert.Equal("// Hairs-Human:        STALE; Previous=EB@112233", LineWith(Desired(plan, "x.cs"), "// Hairs-Human:").TrimStart());
        Assert.Equal(AssuranceUnitState.Stale, After(plan, ".Fold(int[])").State);

        AssurancePlan again = Replan(plan, Config());
        Assert.Equal(AssuranceUnitState.Stale, After(again, ".Fold(int[])").State);
        Assert.Empty(again.Changes);
    }

    [Theory(Timeout = 600000)]
    [InlineData("PENDING; Fingerprint=TBF")]
    [InlineData("STALE")]
    [InlineData("EB;")]
    [InlineData("Fingerprint=112233")]
    [InlineData("")]
    public void A_Human_Line_Outside_The_Defined_Shapes_Is_Refused_And_Nothing_Is_Written(string body)
    {
        string text = Probe(body);
        AssurancePlan plan = Plan(("x.cs", text));

        AssuranceViolation refusal = Assert.Single(plan.Problems);
        Assert.Equal("J4", refusal.Rule);
        Assert.Equal(6, refusal.Line);
        Assert.Equal(
            "The assurance generator will not rewrite the human line on x.cs(7): Probe.Folds.Fold(int[]), which " +
            $"reads '{body}'. A human line is one of 'PENDING', a reviewer, a reviewer with a Fingerprint, or " +
            "'STALE; Previous=<reviewer>@<fingerprint>'. Only a human may create an approval. A reviewer is an " +
            "alias: it opens with a letter, holds letters, digits, '.', '_', '-', ''' and single spaces, is at most " +
            "64 characters long, and is not a placeholder such as TODO, NONE or NOT REVIEWED.",
            refusal.Message);
        Assert.Equal(text, Desired(plan, "x.cs"));
    }

    [Fact(Timeout = 600000)]
    public void The_Guard_Refuses_A_Name_The_Source_Did_Not_Carry()
    {
        AssuranceRefusalException refused = Assert.Throws<AssuranceRefusalException>(() =>
            AssuranceHumanLine.RefuseInventedApproval("x.cs(7): Probe.Folds.Fold(int[])", "PENDING", "EB; Fingerprint=ABCDEF"));

        Assert.Equal(
            "The assurance generator tried to write reviewer 'EB' onto x.cs(7): Probe.Folds.Fold(int[]), whose human " +
            "line reads 'PENDING'. Only a human may create an approval.",
            refused.Message);

        AssuranceHumanLine.RefuseInventedApproval("x", "EB; Fingerprint=112233", "STALE; Previous=EB@112233");
    }

    [Theory(Timeout = 600000)]
    [InlineData("PENDING", true)]
    [InlineData("WITNESS-ONLY", true)]
    [InlineData("WITNESS-ONLY; Fingerprint=TBF", true)]
    [InlineData("WITNESS-ONLY; Fingerprint=44EBF3", true)]
    [InlineData("Maik Ratzmer; IP=Low; Fingerprint=44EBF3", true)]
    [InlineData("EB; CS=10; CL=0", true)]
    [InlineData("EB; CS=9; CL=1; Fingerprint=44EBF3", true)]
    [InlineData("STALE; Previous=WITNESS-ONLY@112233", true)]
    [InlineData("PENDING; Fingerprint=TBF", false)]
    [InlineData("STALE", false)]
    [InlineData("STALE; Fingerprint=112233", false)]
    [InlineData("STALE; Previous=@112233", false)]
    [InlineData("Fingerprint=112233", false)]
    [InlineData("EB; Reviewed=yes", false)]
    [InlineData("", false)]
    [InlineData("NOT REVIEWED", false)]
    [InlineData("Pending", false)]
    [InlineData("PENDING​", false)]
    [InlineData("TODO; Fingerprint=44EBF3", false)]
    [InlineData("José Menéndez", true)]
    [InlineData("Two  Spaces", false)]
    [InlineData("EB@example", false)]
    public void The_Defined_Human_Line_Shapes(string body, bool defined) =>
        Assert.Equal(defined, AssuranceHumanLine.IsDefined(body));

    [Fact(Timeout = 600000)]
    public void Generating_Twice_Gives_The_Same_Bytes()
    {
        AssuranceComponentConfig config = Config();
        (string, string)[] files =
        [
            ("a/Crlf.cs", Probe("EB", aiFingerprint: FoldFingerprint).Replace("\n", "\r\n", StringComparison.Ordinal)),
            ("a/Mixed.cs", "namespace M;\r\npublic sealed class C\n{\r\n    public int Twice(int x) => x * 2;\n}"),
            ("a/Empty.cs", string.Empty),
            ("a/GlobalUsings.cs", "global using System;\nglobal using System.Linq;\n"),
            ("a/Program.cs", "System.Console.WriteLine(Args());\n\nstatic string[] Args() => [\"x\"];\n"),
            ("a/Nullable.cs", "#nullable enable\n\tnamespace N;\n\tpublic enum E { A, B }\n"),
            ("a/Notice.cs", "// Derived from a sample in a book; see NOTICE.\n\nnamespace N;\n\npublic record R(int A);\n"),
            ("a/Stale.cs", Probe("EB; Fingerprint=112233")),
            ("a/High.cs", Probe("PENDING", security: "High", criterion: "a negative value reaches the running total")),
            ("a/Leading.cs", "\n   \nnamespace N;\npublic interface I { void M(); }\n"),
        ];

        AssurancePlan first = Plan(config, files);
        Assert.Empty(first.Problems);

        AssurancePlan second = Replan(first, config);
        Assert.Empty(second.Problems);
        Assert.All(second.Artefacts, artefact => Assert.True(artefact.IsCurrent, artefact.RelativePath));

        Assert.Equal(
            first.Files.Select(static file => file.Scan.FileFingerprint),
            first.Files.Select(static file => file.FileFingerprint));
    }

    [Fact(Timeout = 600000)]
    public void The_Header_Goes_On_Line_Zero_Of_Every_Covered_File_With_One_Blank_Line()
    {
        string text = "namespace N;\n\npublic sealed class C\n{\n    public int Value;\n}\n";

        Assert.Equal(
            Header(1, 0, 1, 0, "not assessed", "not assessed", "0/0", "not assessed", 1) + text,
            Desired(Plan(("x.cs", text)), "x.cs"));

        Assert.Equal(
            Header(0, 0, 0, 0, "not assessed", "not assessed", "0/0", "not assessed", 0) + "global using System;\n",
            Desired(Plan(("g.cs", "global using System;\n")), "g.cs"));
    }

    [Fact(Timeout = 600000)]
    public void The_Header_Counts_The_Refreshed_States()
    {
        string desired = Desired(Plan(("x.cs", Probe("EB", FoldFingerprint, security: "High", criterion: "a negative value reaches the total"))), "x.cs");

        Assert.StartsWith(Header(2, 1, 0, 1, "Low", "High", "1/1", "3/10 max", 1), desired, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void A_Leading_Comment_That_Is_Not_A_Generated_Header_Is_Kept_Below_It()
    {
        const string notice =
            "// Copyright (c) 2009 A Person. Redistribution permitted under the licence in NOTICE.\n" +
            "// Approved for redistribution by the reviewer named there.\n";
        string text = notice + "namespace N;\n\npublic static class C\n{\n}\n";

        AssurancePlan plan = Plan(("x.cs", text));
        string desired = Desired(plan, "x.cs");

        Assert.EndsWith("\n\n" + text, desired, StringComparison.Ordinal);
        Assert.Empty(Replan(plan, Config()).Changes);
    }

    [Fact(Timeout = 600000)]
    public void The_Header_Goes_Above_A_Nullable_Directive_And_Keeps_Tabs_And_A_Missing_Final_Newline()
    {
        string text = "#nullable enable\n\tnamespace N;\n\tpublic enum E { A }";

        Assert.Equal(
            Header(1, 0, 1, 0, "not assessed", "not assessed", "0/0", "not assessed", 1) + text,
            Desired(Plan(("x.cs", text)), "x.cs"));
    }

    [Fact(Timeout = 600000)]
    public void A_Foreign_Spdx_Run_Is_Refused_And_An_Identical_One_Is_Adopted()
    {
        const string body = "\nnamespace N;\n\npublic static class C\n{\n}\n";
        string foreign = "// SPDX-FileCopyrightText: 2009 A Person\n// SPDX-License-Identifier: BSD-3-Clause\n" + body;

        AssurancePlan refused = Plan(("x.cs", foreign));
        AssuranceViolation problem = Assert.Single(refused.Problems);
        Assert.Equal("J5", problem.Rule);
        Assert.StartsWith(
            "x.cs opens with a comment run the generator did not write (line 1: '// SPDX-FileCopyrightText: 2009 A Person')",
            problem.Message,
            StringComparison.Ordinal);
        Assert.Equal(foreign, Desired(refused, "x.cs"));

        string adopted = $"// SPDX-FileCopyrightText: {Copyright}\n// SPDX-License-Identifier: Apache-2.0\n" + body;
        Assert.Equal(
            Header(1, 0, 0, 0, "not assessed", "not assessed", "0/0", "not assessed", 1) + body[1..],
            Desired(Plan(("x.cs", adopted)), "x.cs"));
    }

    [Fact(Timeout = 600000)]
    public void A_Foreign_Line_Inside_A_Generated_Header_Is_Refused()
    {
        string header = Header(1, 0, 0, 0, "not assessed", "not assessed", "0/0", "not assessed", 1);
        string text = header.Replace("//\n// HAIRS Code Assurance", "// Portions (c) 2009 A Person\n//\n// HAIRS Code Assurance", StringComparison.Ordinal) +
            "namespace N;\npublic static class C { }\n";

        AssuranceViolation problem = Assert.Single(Plan(("x.cs", text)).Problems);
        Assert.StartsWith(
            "x.cs(3) sits inside the generated header and is not a line the generator writes: '// Portions (c) 2009 A Person'",
            problem.Message,
            StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void A_Tampered_Header_And_A_Forged_Block_Under_It_Are_Replaced_By_One_Header()
    {
        string real = Header(1, 0, 0, 0, "not assessed", "not assessed", "0/0", "not assessed", 1);
        string text =
            real.Replace("// Human-reviewed:   0/1", "// Human-reviewed:   1/1", StringComparison.Ordinal) +
            "// HAIRS Code Assurance\n" +
            "// Human-reviewed:   47/47\n" +
            "// GENERATED - DO NOT EDIT MANUALLY\n" +
            "\n" +
            "namespace N;\npublic static class C { }\n";

        Assert.Equal(real + "namespace N;\npublic static class C { }\n", Desired(Plan(("x.cs", text)), "x.cs"));
    }

    [Fact(Timeout = 600000)]
    public void The_Header_Takes_The_Files_Own_Line_Endings()
    {
        string crlf = "namespace N;\r\npublic static class C { }\r\n";
        Assert.Equal(
            Header(1, 0, 0, 0, "not assessed", "not assessed", "0/0", "not assessed", 1, newLine: "\r\n") + crlf,
            Desired(Plan(("x.cs", crlf)), "x.cs"));

        string mixed = "namespace N;\npublic static class C\r\n{\r\n}\n";
        AssurancePlan plan = Plan(("x.cs", mixed));
        Assert.Equal(
            Header(1, 0, 0, 0, "not assessed", "not assessed", "0/0", "not assessed", 1) + mixed,
            Desired(plan, "x.cs"));
    }

    [Fact(Timeout = 600000)]
    public void A_Hand_Written_Block_Is_Realigned_And_Nothing_In_It_Is_Authored()
    {
        string text = Probe("PENDING")
            .Replace(
                "    // Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=3; Fingerprint=TBF\n",
                "    // Hairs-AI: Origin = AI;IP=Low;; Security=Low; Resources=3; Fingerprint=TBF\n" +
                "      // Hairs-Falsified-If:    the total wraps around\n",
                StringComparison.Ordinal)
            .Replace("    // Hairs-Human:        PENDING", "  // Hairs-Human: PENDING", StringComparison.Ordinal);

        string desired = Desired(Plan(("x.cs", text)), "x.cs");

        Assert.Contains(
            $"    // Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=3; Fingerprint={FoldFingerprint}\n" +
            "    // Hairs-Falsified-If: the total wraps around\n" +
            "    // Hairs-Human:        PENDING\n",
            desired,
            StringComparison.Ordinal);
    }
}
