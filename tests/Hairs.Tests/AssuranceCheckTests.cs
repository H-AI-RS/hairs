// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using Hairs.Assurance;
using static Hairs.Tests.AssurancePlanning;

namespace Hairs.Tests;

/// <summary>
/// Assurance check tests for J1 through J11 rules.
/// </summary>
public sealed class AssuranceCheckTests
{
    private const string Unannotated =
        "namespace Probe;\n" +
        "\n" +
        "public static class Folds\n" +
        "{\n" +
        "    public static int Fold(int[] values) => values.Length * 2;\n" +
        "}\n";

    private static IReadOnlyList<AssuranceViolation> CheckGenerated(
        AssuranceComponentConfig config, AssuranceCheckOptions? options, params (string Path, string Text)[] files)
    {
        AssurancePlan first = Plan(config, files);
        Assert.Empty(first.Problems);
        return AssuranceChecks.Run(Replan(first, config), config, options ?? new AssuranceCheckOptions());
    }

    private static string Annotate(string text, string block) =>
        text.Replace("    public static int Fold", block + "    public static int Fold", StringComparison.Ordinal);

    private static string Folds(
        string fields = "Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF",
        string human = "PENDING",
        string? criterion = null) =>
        "namespace Probe;\n" +
        "\n" +
        "// Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF\n" +
        "// Hairs-Human:        PENDING\n" +
        "public static class Folds\n" +
        "{\n" +
        $"    // Hairs-AI:           {fields}\n" +
        (criterion is null ? string.Empty : $"    // Hairs-Falsified-If: {criterion}\n") +
        $"    // Hairs-Human:        {human}\n" +
        "    public static int Fold(int[] values) => values.Length * 2;\n" +
        "}\n";

    [Fact(Timeout = 600000)]
    public void A_Generated_Fully_Annotated_Tree_Has_No_Violations()
    {
        Assert.Empty(CheckGenerated(Config(), null, ("x.cs", Folds())));
    }

    [Fact(Timeout = 600000)]
    public void J1_A_Relevant_Unit_Without_A_Block_And_An_Exemption_In_A_Closed_Assembly()
    {
        IReadOnlyList<AssuranceViolation> found = CheckGenerated(Config(), null, ("x.cs", Unannotated));

        Assert.Equal(
            [
                ("J1", "x.cs(20): Probe.Folds is relevant and carries no assurance annotation"),
                ("J1", "x.cs(22): Probe.Folds.Fold(int[]) is relevant and carries no assurance annotation"),
            ],
            found.Select(static violation => (violation.Rule, violation.Message)));

        string hatch = Folds().Replace(
            "Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF", "EXEMPT=a generated shim", StringComparison.Ordinal);
        AssuranceViolation closed = Assert.Single(CheckGenerated(Config(closed: ["Probe"]), null, ("x.cs", hatch)));
        Assert.Equal("J1", closed.Rule);
        Assert.EndsWith(
            "Probe.Folds.Fold(int[]) states EXEMPT=a generated shim, and Probe is closed to the per-unit exemption: " +
            "a unit there is assessed or it is not shipped",
            closed.Message,
            StringComparison.Ordinal);

        Assert.Empty(CheckGenerated(Config(), null, ("x.cs", hatch)));
    }

    [Fact(Timeout = 600000)]
    public void J2_Unreadable_Blocks_Stranded_Lines_Vocabulary_And_Citations()
    {
        string stranded = Folds() + "// Hairs-Falsified-If: nothing stands above this\n";
        string vocabulary = Folds("Origin=AI; IP=low; Security=Low; Resources=11; Fingerprint=TBF; Spec=ADR-0042 s2");

        AssuranceViolation[] found =
        [
            .. CheckGenerated(Config(), new AssuranceCheckOptions(AdrRecords: new HashSet<string> { "0001" }),
                ("a.cs", stranded), ("b.cs", vocabulary))
        ];

        Assert.All(found, static violation => Assert.Equal("J2", violation.Rule));
        Assert.Equal(
            [
                "a.cs(28): carries a '// Hairs-Falsified-If:' line that stands between no '// Hairs-AI:' line and '// Hairs-Human:' line",
                "b.cs(26): Probe.Folds.Fold(int[]): IP=low is outside its vocabulary (None, Low, Medium, High, Unknown)",
                "b.cs(26): Probe.Folds.Fold(int[]): Resources=11 is not an integer 0 to 10",
                "b.cs(26): Probe.Folds.Fold(int[]) cites Spec=ADR-0042 s2, and docs/adr/ holds no record 0042",
            ],
            found.Select(static violation => violation.Message));
    }

    [Fact(Timeout = 600000)]
    public void J2_A_Block_That_Does_Not_Parse_Is_Named_With_Its_Reason()
    {
        string broken = Annotate(Unannotated, "    // Hairs-AI:           Origin=AI; Low\n    // Hairs-Human:        PENDING\n");

        IReadOnlyList<AssuranceViolation> found = AssuranceChecks.Run(Plan(("x.cs", broken)), Config(), new AssuranceCheckOptions());

        Assert.Contains(found, static violation =>
            violation.Rule == "J2" && violation.Message == "x.cs(5): line 5 has a field with no '=': 'Low'");
        Assert.Contains(found, static violation =>
            violation.Rule == "J1" && violation.Message.EndsWith("Probe.Folds.Fold(int[]) is relevant and carries no assurance annotation", StringComparison.Ordinal));
    }

    [Fact(Timeout = 600000)]
    public void J3_Fingerprints_As_Recorded_On_Disk()
    {
        string text = Folds("Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=ABCDEF", "EB; Fingerprint=112233");
        string fold = Fingerprint(text, ".Fold(int[])");

        string[] found = [.. AssuranceChecks.Run(Plan(("x.cs", text)), Config(), new AssuranceCheckOptions())
            .Where(static violation => violation.Rule == "J3")
            .Select(static violation => violation.Message)];

        Assert.Equal(
            [
                "x.cs(5): Probe.Folds still records the placeholder TBF, so no recorded fingerprint binds anything",
                $"x.cs(9): Probe.Folds.Fold(int[]) records Fingerprint=ABCDEF and the current code computes {fold}",
                $"x.cs(9): Probe.Folds.Fold(int[]) approves Fingerprint=112233, which is neither the current {fold} nor preserved as Previous",
            ],
            found);

        string preserved = Folds($"Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint={fold}", "STALE; Previous=EB@112233");
        Assert.DoesNotContain(
            AssuranceChecks.Run(Plan(("x.cs", preserved)), Config(), new AssuranceCheckOptions()),
            static violation => violation.Rule == "J3" && violation.Message.Contains(".Fold(int[])", StringComparison.Ordinal));
    }

    [Fact(Timeout = 600000)]
    public void J4_A_Name_In_An_Artefact_That_No_Source_Line_Carries()
    {
        AssurancePlan plan = Plan(("x.cs", Folds()));
        AssuranceArtefact forged = plan.Artefacts[0] with
        {
            Desired = plan.Artefacts[0].Desired.Replace(
                "    // Hairs-Human:        PENDING", "    // Hairs-Human:        EB; Fingerprint=ABCDEF", StringComparison.Ordinal),
        };

        IReadOnlyList<AssuranceViolation> found = AssuranceChecks.Run(
            plan with { Artefacts = [forged, .. plan.Artefacts.Skip(1)] }, Config(), new AssuranceCheckOptions());

        Assert.Contains(found, static violation =>
            violation.Rule == "J4" &&
            violation.Message == "x.cs names 'EB' on a human line, and no human line in the source tree carries that name");
    }

    [Fact(Timeout = 600000)]
    public void J5_A_Stale_Artefact_Names_Its_First_Differing_Line()
    {
        string text = Folds();
        AssurancePlan plan = Plan(("x.cs", text));

        AssuranceViolation stale = AssuranceChecks.Run(plan, Config(), new AssuranceCheckOptions())
            .First(static violation => violation.Rule == "J5" && violation.File == "x.cs");

        Assert.Equal(1, stale.Line);
        Assert.Equal(
            "x.cs(1) is not what the generator would write.\n" +
            "  on disk:   namespace Probe;\n" +
            "  generated: // SPDX-FileCopyrightText: 2026 HAIRS contributors\n" +
            "  Run: hairs assurance generate",
            stale.Message);

        AssuranceViolation missing = AssuranceChecks.Run(plan, Config(), new AssuranceCheckOptions())
            .Single(static violation => violation.Rule == "J5" && violation.File == "CODE-ASSURANCE.md");
        Assert.Equal(1, missing.Line);
        Assert.Equal(
            "CODE-ASSURANCE.md(1) is not what the generator would write.\n" +
            "  on disk:   \n" +
            "  generated: # Probe Code Assurance\n" +
            "  CODE-ASSURANCE.md does not exist, and the generator would write it.\n" +
            "  Run: hairs assurance generate",
            missing.Message);
    }

    [Fact(Timeout = 600000)]
    public void J5_A_Forged_Block_Below_The_Header_Is_Reported_Reworded_Or_Not()
    {
        string forged = Folds().Replace(
            "    // Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=1",
            "    // HAIRS CODE ASSURANCE\n" +
            "    //  Human-reviewed:  2/2\n" +
            "\n" +
            "    // Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=1",
            StringComparison.Ordinal);

        string[] found = [.. CheckGenerated(Config(), null, ("x.cs", forged))
            .Where(static violation => violation.Rule == "J5")
            .Select(static violation => violation.Message)];

        Assert.Equal(
            [
                "x.cs carries 2 'HAIRS Code Assurance' banners; exactly one block is generated and every other one is a forgery",
                "x.cs(24) carries the assurance summary line '// HAIRS CODE ASSURANCE' below the generated header, and 2 such line(s) sit there; the generated block is the only one a file may carry",
            ],
            found);
    }

    [Fact(Timeout = 600000)]
    public void J5_The_Strict_Vocabulary_Also_Reads_Review_Words()
    {
        string text = Folds().Replace(
            "    // Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=1",
            "    // Each reviewer approved this in the design meeting.\n" +
            "    // Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=1",
            StringComparison.Ordinal);

        Assert.DoesNotContain(CheckGenerated(Config(), null, ("x.cs", text)), static violation => violation.Rule == "J5");
        Assert.Contains(
            CheckGenerated(Config(AssuranceForgeryVocabulary.Strict), null, ("x.cs", text)),
            static violation => violation.Rule == "J5" && violation.Message.Contains("'// Each reviewer approved", StringComparison.Ordinal));

        Assert.True(AssuranceHeader.IsSummaryLine("  // Unverified: 0", AssuranceForgeryVocabulary.Narrow));
        Assert.True(AssuranceHeader.IsSummaryLine("/// generated - do not edit manually", AssuranceForgeryVocabulary.Narrow));
        Assert.False(AssuranceHeader.IsSummaryLine("// The reviewer approved it.", AssuranceForgeryVocabulary.Narrow));
        Assert.False(AssuranceHeader.IsSummaryLine("string s = \"// Exempt: x\";", AssuranceForgeryVocabulary.Strict));
    }

    [Fact(Timeout = 600000)]
    public void J6_Directives_Are_Reported_Only_Where_The_Configuration_Forbids_Them()
    {
        string text = "#nullable enable\n" + Folds();

        Assert.DoesNotContain(CheckGenerated(Config(), null, ("x.cs", text)), static violation => violation.Rule == "J6");

        AssuranceViolation directive = Assert.Single(
            CheckGenerated(Config(forbidDirectives: true), null, ("x.cs", text)), static violation => violation.Rule == "J6");
        Assert.Equal(
            "x.cs(18): carries the preprocessor directive '#nullable enable', and a covered file carries none - " +
            "a directive is trivia and no fingerprint records it",
            directive.Message);
    }

    [Fact(Timeout = 600000)]
    public void J7_The_Manifest_Is_Held_To_The_Tree()
    {
        AssuranceComponentConfig config = Config();
        AssurancePlan first = Plan(config, ("x.cs", Folds()));
        AssurancePlan current = Replan(first, config);
        AssuranceArtefact manifest = current.Artefacts.Single(static artefact => artefact.Kind == AssuranceArtefactKind.Manifest);

        string fold = Fingerprint(Folds(), ".Fold(int[])");
        string tampered = manifest.Current
            .Replace($"\"fingerprint\": \"{fold}\"", "\"fingerprint\": \"000000\"", StringComparison.Ordinal);

        IReadOnlyList<AssuranceViolation> found = AssuranceManifest.Violations(
            manifest.RelativePath,
            current.Files.Select(static file => new AssuranceManifestFile(file.Source.RelativePath, file.FileFingerprint)),
            current.UnitsAfter,
            tampered);

        Assert.Equal(
            [$"x.cs: Probe.Folds.Fold(int[]) is recorded in hairs.manifest.json as 000000 and the current code computes {fold}"],
            found.Select(static violation => violation.Message));

        Assert.Equal(
            ["hairs.manifest.json is absent or empty, so no unit is covered at all"],
            AssuranceManifest.Violations(manifest.RelativePath, [], [], string.Empty).Select(static violation => violation.Message));
    }

    [Fact(Timeout = 600000)]
    public void J10_A_High_Unit_Owes_A_Criterion()
    {
        AssuranceViolation missing = Assert.Single(
            CheckGenerated(Config(), null, ("x.cs", Folds("Origin=AI; IP=Low; Security=Critical; Resources=1; Fingerprint=TBF"))));

        Assert.Equal("J10", missing.Rule);
        Assert.Equal(
            "x.cs(26): Probe.Folds.Fold(int[]) is assessed Security=Critical and carries no '// Hairs-Falsified-If:' " +
            "line, so nothing at the declaration says what would make it wrong",
            missing.Message);

        Assert.Empty(CheckGenerated(Config(), null, ("x.cs", Folds(
            "Origin=AI; IP=Low; Security=Critical; Resources=1; Fingerprint=TBF", criterion: "a negative length is accepted"))));
    }

    [Fact(Timeout = 600000)]
    public void J11_Unresolved_Units_Block_Only_A_Release()
    {
        Assert.Empty(CheckGenerated(Config(), null, ("x.cs", Folds())));

        string[] found = [.. CheckGenerated(Config(), new AssuranceCheckOptions(Release: true), ("x.cs", Folds(Generated(), human: "EB")))
            .Select(static violation => $"{violation.Rule} {violation.Message}")];

        Assert.Equal(["J11 x.cs(22): Probe.Folds is HUMAN_PENDING and its human line reads 'PENDING'"], found);
    }

    [Fact(Timeout = 600000)]
    public void J9_The_Generated_Text_Claims_No_Review_The_Annotations_Do_Not_Hold()
    {
        string current = Folds(Generated(), human: "EB");
        string outrun = Folds(human: "EB; Fingerprint=112233").Replace("class Folds", "class Other", StringComparison.Ordinal);
        string approved = Folds(human: "EB; Fingerprint=TBF", fields: Generated("Origin=AI; IP=Low; Security=High; Resources=1"), criterion: "a zero is returned for a non-empty array")
            .Replace("class Folds", "class Third", StringComparison.Ordinal);

        IReadOnlyList<AssuranceViolation> found = CheckGenerated(
            Config(), new AssuranceCheckOptions(), ("a.cs", current), ("b.cs", outrun), ("c.cs", approved), ("d.cs", Unannotated.Replace("Folds", "Fourth", StringComparison.Ordinal)));

        Assert.DoesNotContain(found, static violation => violation.Rule == "J9");

        IReadOnlyList<AssuranceViolation> claims = AssuranceReviewClaims.Violations(
            "x.md",
            [
                "| VERIFIED | 0 |",
                "It is not an approval.",
                "Every unit here is verified and eligible for release.",
                "No unit is outside this record: every unit below is approved.",
            ],
            []);

        Assert.Equal(
            [
                "x.md(3) says 'Every unit here is verified and eligible for release.', and the annotations hold no such state: the term 'verified' is stated with neither the count the annotations give (0) nor a negation before it",
                "x.md(4) says 'No unit is outside this record: every unit below is approved.', and the annotations hold no such state: the term 'approved' is stated with neither the count the annotations give (0) nor a negation before it",
            ],
            claims.Select(static violation => violation.Message));
    }

    private static string Generated(string fields = "Origin=AI; IP=Low; Security=Low; Resources=1") =>
        $"{fields}; Fingerprint={Fingerprint(Folds(), ".Fold(int[])")}";
}
