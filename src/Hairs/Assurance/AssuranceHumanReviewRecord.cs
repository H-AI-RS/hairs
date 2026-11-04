// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0
//
// HAIRS Code Assurance
// --------------------
// Relevant units:   14
// Annotated:        14/14
// Exempt:           0
// Human-reviewed:   0/14
// IP risk:          Low
// Security risk:    Medium
// Criteria:         11/0
// Resource impact:  4/10 max
// Unverified:       14
//
// GENERATED - DO NOT EDIT MANUALLY

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hairs.Assurance;

/// <summary>One alias read out of the human lines, with what it is named on.</summary>
/// <param name="Alias">The alias, live or preserved by a STALE line.</param>
/// <param name="Units">Units whose human line names it.</param>
/// <param name="Files">Distinct files those units are in.</param>
/// <param name="Current">Of those, the units whose decision is bound to the version here now.</param>
/// <param name="Outrun">Of those, the units whose code moved after the decision.</param>
// Hairs-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=E64272
// Hairs-Human:        PENDING
public sealed record AssuranceReviewerRow(string Alias, int Units, int Files, int Current, int Outrun);

/// <summary>
/// <c>HUMAN_REVIEW.md</c>: the per-unit decisions of one component, read out of
/// its human lines and nothing else.
/// </summary>
// Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=478EA2
// Hairs-Falsified-If: the record reports Status COMPLETE, or a first status count equal to the relevant count, while a relevant unit's human line does not name the fingerprint that unit computes now
// Hairs-Human:        PENDING
public static class AssuranceHumanReviewRecord
{
    /// <summary>
    /// <c>PENDING</c> while no relevant unit is current, <c>PARTIAL</c> while
    /// some are, <c>COMPLETE</c> when all are. A component with no relevant
    /// unit is <c>PENDING</c>, not complete.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=632B32
    // Hairs-Falsified-If: a list whose relevant units all name their current fingerprint except one unit in the Stale state returns COMPLETE
    // Hairs-Human:        PENDING
    public static string Status(IReadOnlyList<AssuranceCorpusUnit> units)
    {
        ArgumentNullException.ThrowIfNull(units);

        int relevant = units.Count(static unit => unit.IsRelevant);
        int current = units.Count(static unit => unit.IsRelevant && unit.State == AssuranceUnitState.Verified);
        return current == 0 ? "PENDING" : current < relevant ? "PARTIAL" : "COMPLETE";
    }

    /// <summary>
    /// Every alias any human line names, live or preserved as
    /// <c>Previous=</c>, in ordinal order.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=3CEE52
    // Hairs-Falsified-If: a unit in the Stale state is counted in its alias's Current column instead of its Outrun column
    // Hairs-Human:        PENDING
    public static IReadOnlyList<AssuranceReviewerRow> Reviewers(IReadOnlyList<AssuranceCorpusUnit> units)
    {
        ArgumentNullException.ThrowIfNull(units);

        return [.. units
            .Where(static unit => unit.Annotation is not null)
            .Select(static unit => (Unit: unit, Alias: AliasOn(unit)))
            .Where(static named => named.Alias is not null)
            .GroupBy(static named => named.Alias!, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new AssuranceReviewerRow(
                group.Key,
                group.Count(),
                group.Select(static named => named.Unit.File).Distinct(StringComparer.Ordinal).Count(),
                group.Count(static named => named.Unit.State == AssuranceUnitState.Verified),
                group.Count(static named => named.Unit.State == AssuranceUnitState.Stale)))];
    }

    /// <summary>The alias a unit's human line names: the live one, or the one a STALE line preserves.</summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=2; Fingerprint=A15C4F
    // Hairs-Falsified-If: a unit whose human line reads exactly PENDING yields an alias instead of null
    // Hairs-Human:        PENDING
    public static string? AliasOn(AssuranceCorpusUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        return unit.Annotation?.Reviewer ?? unit.Annotation?.Previous?.Reviewer;
    }

    /// <summary>The record for the post-generation units of every covered file.</summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=3E3905
    // Hairs-Falsified-If: the first count on the status line differs from the number of relevant units whose human line names the fingerprint the unit computes now
    // Hairs-Human:        PENDING
    public static string Render(AssuranceReportContext context, IReadOnlyList<AssuranceCorpusUnit> units)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(units);

        AssuranceSummary summary = AssuranceSummary.Of(units);
        AssuranceArtefactPaths paths = context.Paths;
        var record = new StringBuilder();

        record.Append($"# Human Review: {context.Component}\n\n");
        record.Append("GENERATED - DO NOT EDIT MANUALLY. Regenerate with\n");
        record.Append($"`{context.GenerateCommand}`, which rewrites this file,\n");
        record.Append($"`{paths.Report}`, `{paths.Manifest}` and every generated source header from the\n");
        record.Append("product tree.\n\n");

        record.Append(
            $"> **Status: {Status(units)}.** Human-reviewed: {AssuranceFormat.Count(summary.Verified)} of " +
            $"{AssuranceFormat.Count(summary.Relevant)} relevant units. `{context.CheckCommand} --release`\n");
        record.Append("> fails while any relevant unit is without a decision bound to its current fingerprint.\n\n");

        record.Append(HowToUseThisFile());
        record.Append(HowAReviewIsRecorded());

        record.Append("## 3. Summary\n\n");
        record.Append("| Metric | Value |\n|---|---:|\n");
        record.Append($"| Files scanned | {AssuranceFormat.Count(context.CoveredFiles.Count)} |\n");
        record.Append($"| Code units | {AssuranceFormat.Count(units.Count)} |\n");
        record.Append($"| Relevant | {AssuranceFormat.Count(summary.Relevant)} |\n");
        record.Append($"| Exempt | {AssuranceFormat.Count(summary.Exempt)} |\n");
        record.Append($"| Assessed | {AssuranceFormat.Portion(summary.Annotated, summary.Relevant)} |\n");
        record.Append($"| Human reviewed | {AssuranceFormat.Portion(summary.Verified, summary.Relevant)} |\n");
        record.Append($"| Unverified | {AssuranceFormat.Count(summary.Unverified)} |\n");
        record.Append($"| Aliases naming a decision | {AssuranceFormat.Count(Reviewers(units).Count)} |\n\n");

        record.Append("## 4. Review States\n\n");
        record.Append("One row per state of the machine that reads the two lines. The states are computed from the\n");
        record.Append("annotations and the current fingerprints; nothing stores them.\n\n");
        record.Append("| State | Units |\n|---|---:|\n");
        foreach (AssuranceUnitState state in AssuranceVocabulary.States)
            record.Append($"| {AssuranceStateMachine.Name(state)} | {AssuranceFormat.Count(units.Count(unit => unit.State == state))} |\n");

        record.Append('\n');
        record.Append(ReviewerSection(units));
        record.Append(CoverageSection(context, units));
        record.Append(DecisionSection(units));
        record.Append(OutrunSection(units));
        record.Append(RequiredFirstSection(units));
        record.Append(WhatThisRecordDoesNotSay(units));

        return record.ToString();
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=None; Resources=0; Fingerprint=CA068A
    // Hairs-Human:        PENDING
    private static string HowToUseThisFile() =>
        "## 1. How To Use This File\n" +
        "\n" +
        "Read it; do not edit it. A decision about a code unit is the `" + AssuranceVocabulary.HumanMarker + "` line on that\n" +
        "unit's declaration, and every table below is read out of those lines. There is nothing here\n" +
        "to fill in and nothing here to leave blank.\n" +
        "\n";

    // Hairs-AI:           Origin=AI; IP=Low; Security=None; Resources=0; Fingerprint=1409FD
    // Hairs-Human:        PENDING
    private static string HowAReviewIsRecorded() =>
        "## 2. How A Review Is Recorded\n" +
        "\n" +
        "In one place: the `" + AssuranceVocabulary.HumanMarker + "` line of the assurance annotation that sits on the\n" +
        "declaration being read. Nothing in this file is edited by hand, no second document carries a\n" +
        "per-item checklist, and no list of permitted aliases exists to be added to.\n" +
        "\n" +
        "```csharp\n" +
        AssuranceVocabulary.AiMarker + "           Origin=AI; IP=Low; Security=High; Resources=2; Fingerprint=4A3BFD\n" +
        AssuranceVocabulary.FalsifiedIfMarker + " a negative value reaches the running total\n" +
        AssuranceVocabulary.HumanMarker + "        PENDING\n" +
        "```\n" +
        "\n" +
        "The last line has four shapes. A human writes three of them; the generator writes the fourth\n" +
        "and may never invent an alias, which the check asserts in both directions.\n" +
        "\n" +
        "| Line | Meaning |\n" +
        "|---|---|\n" +
        "| `PENDING` | Nobody has recorded a decision for this unit. The generator leaves it exactly as it stands. |\n" +
        "| `<alias>` | A human states their own alias and leaves the machine field to the generator, which fills it with the declaration's fingerprint at the next run. |\n" +
        "| `<alias>; Fingerprint=<six hex>` | A decision bound to one exact version of one declaration. |\n" +
        "| `STALE; Previous=<alias>@<fingerprint>` | Written by the generator when the code moved after a decision. Only a human clears it, by stating their alias again. |\n" +
        "\n" +
        "A human may state their own `IP=`, `Security=`, `Resources=`, `CS=` and `CL=` assessment beside their alias,\n" +
        "which is how a reader disagrees with the machine assessment on the line above: an assessment is\n" +
        "a comment and moves no fingerprint, so there is nowhere else to say it.\n" +
        "\n" +
        "**No branch, commit or tag is recorded in this file.** Each decision names the fingerprint of\n" +
        "the declaration it was made against, and the state machine compares that value with the\n" +
        "declaration as it now stands. A commit says a tree moved; a fingerprint says whether this unit\n" +
        "did, which is the narrower and the more useful of the two.\n" +
        "\n";

    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=5ADC21
    // Hairs-Falsified-If: a unit whose annotation states an EXEMPT reason is counted among the assessed units in the closing sentence
    // Hairs-Human:        PENDING
    private static string WhatThisRecordDoesNotSay(IReadOnlyList<AssuranceCorpusUnit> units)
    {
        AssuranceAnnotation[] assessed = [.. units
            .Where(static unit => unit.IsRelevant && unit.Annotation is { ExemptReason: null })
            .Select(static unit => unit.Annotation!)];
        int machine = assessed.Count(static annotation =>
            string.Equals(annotation.Field("Origin"), "AI", StringComparison.Ordinal));

        return
            "## 10. What This Record Does Not Say\n" +
            "\n" +
            "It is not an approval of the component, and a full table above would not be one either. It\n" +
            "records which declarations somebody stated a decision about, and against which version of\n" +
            "each. It does not record what they read, how long they spent, or whether they were right.\n" +
            "\n" +
            "A fingerprint is six hex characters of SHA-256 over a declaration's token texts. It answers\n" +
            "whether a unit changed since a decision was recorded against it. It is not a collision-free\n" +
            "identifier across units and it is not a cryptographic commitment, so it detects a change and\n" +
            "does not resist a forger with commit access.\n" +
            "\n" +
            "An assessment is a comment, so changing one moves no fingerprint anywhere, and nothing\n" +
            "mechanical checks that it is right; the check holds its values to their vocabularies and no\n" +
            "further.\n" +
            "\n" +
            $"{AssuranceFormat.Count(machine)} of the {AssuranceFormat.Count(assessed.Length)} assessed units declare `Origin=AI`. " +
            "Reading a declaration is the only thing\n" +
            "that makes it read.\n";
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=984E85
    // Hairs-Falsified-If: the section says no alias appears in the tree while at least one unit's human line names an alias
    // Hairs-Human:        PENDING
    private static string ReviewerSection(IReadOnlyList<AssuranceCorpusUnit> units)
    {
        IReadOnlyList<AssuranceReviewerRow> rows = Reviewers(units);
        var section = new StringBuilder("## 5. Aliases In The Tree\n\n");

        if (rows.Count == 0)
        {
            section.Append("No alias appears on a human line anywhere in the product tree. Nobody has recorded a\n");
            section.Append("decision about any unit of this component.\n\n");
            return section.ToString();
        }

        section.Append("Read out of the human lines, never registered. `Current` counts the units whose decision\n");
        section.Append("names the fingerprint the declaration carries now; `Outrun` counts the units whose\n");
        section.Append("declaration has changed since.\n\n");
        section.Append("| Alias | Units | Files | Current | Outrun |\n|---|---:|---:|---:|---:|\n");
        foreach (AssuranceReviewerRow row in rows)
        {
            section.Append(
                $"| {row.Alias} | {AssuranceFormat.Count(row.Units)} | {AssuranceFormat.Count(row.Files)} | " +
                $"{AssuranceFormat.Count(row.Current)} | {AssuranceFormat.Count(row.Outrun)} |\n");
        }

        section.Append('\n');
        return section.ToString();
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=8B51B9
    // Hairs-Falsified-If: a covered file whose path contains a '|' character produces a table row whose counts fall under the wrong column headers
    // Hairs-Human:        PENDING
    private static string CoverageSection(AssuranceReportContext context, IReadOnlyList<AssuranceCorpusUnit> units)
    {
        var section = new StringBuilder("## 6. Coverage By File\n\n");
        section.Append("One row per covered file, carrying that file's generated header. `Unverified` counts the\n");
        section.Append("relevant units in a state that blocks a release.");
        section.Append(context.Excluded.Count == 0
            ? "\n\n"
            : $" The files the configuration leaves out\nare listed in `{context.Paths.Report}`.\n\n");
        section.Append("| File | Units | Relevant | Exempt | Unverified | IP risk | Security risk | Criteria |\n");
        section.Append("|---|---:|---:|---:|---:|---|---|---:|\n");

        ILookup<string, AssuranceCorpusUnit> byFile = units.ToLookup(static unit => unit.File, StringComparer.Ordinal);
        foreach (string file in context.CoveredFiles)
        {
            AssuranceCorpusUnit[] owned = [.. byFile[file]];
            AssuranceSummary summary = AssuranceSummary.Of(owned);

            section.Append(
                $"| `{file}` | {AssuranceFormat.Count(owned.Length)} | {AssuranceFormat.Count(summary.Relevant)} | " +
                $"{AssuranceFormat.Count(summary.Exempt)} | {AssuranceFormat.Count(summary.Unverified)} | " +
                $"{summary.MaxIpRisk ?? AssuranceBanner.NotAssessed} | {summary.MaxSecurityRisk ?? AssuranceBanner.NotAssessed} | " +
                $"{AssuranceFormat.Count(summary.Criteria)}/{AssuranceFormat.Count(summary.CriteriaRequired)} |\n");
        }

        section.Append('\n');
        return section.ToString();
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=78B89F
    // Hairs-Falsified-If: a unit in the Stale or HumanPending state is listed under Decisions Recorded
    // Hairs-Human:        PENDING
    private static string DecisionSection(IReadOnlyList<AssuranceCorpusUnit> units)
    {
        AssuranceCorpusUnit[] decided = [.. units.Where(static unit =>
            unit.State is AssuranceUnitState.Verified or AssuranceUnitState.HumanApprovedPendingFingerprint)];
        var section = new StringBuilder("## 7. Decisions Recorded\n\n");

        if (decided.Length == 0)
        {
            section.Append("No unit in this component carries a decision on its human line. Every one of them reads\n");
            section.Append($"`{AssuranceVocabulary.Pending}`.\n\n");
            return section.ToString();
        }

        section.Append("One entry per unit whose human line names an alias, with the line exactly as the source\n");
        section.Append("states it.\n\n");
        foreach (AssuranceCorpusUnit unit in decided)
            section.Append($"- `{unit.Name}` in `{unit.File}` - {AssuranceHumanLine.Display(unit.Annotation)}\n");

        section.Append('\n');
        return section.ToString();
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=65467B
    // Hairs-Falsified-If: a unit in the Stale state is left out of the list of decisions the code has outrun
    // Hairs-Human:        PENDING
    private static string OutrunSection(IReadOnlyList<AssuranceCorpusUnit> units)
    {
        AssuranceCorpusUnit[] outrun = [.. units.Where(static unit => unit.State == AssuranceUnitState.Stale)];
        var section = new StringBuilder("## 8. Decisions The Code Has Outrun\n\n");

        if (outrun.Length == 0)
        {
            section.Append("No unit carries a decision that the code has since moved past.\n\n");
            return section.ToString();
        }

        section.Append("The declaration changed after the decision was recorded. The alias and the version it was\n");
        section.Append("recorded against are preserved rather than deleted, because that is more useful than a\n");
        section.Append("blank line. Only a human clears one.\n\n");
        foreach (AssuranceCorpusUnit unit in outrun)
            section.Append($"- `{unit.Name}` in `{unit.File}` - {AssuranceHumanLine.Display(unit.Annotation)}, now `{unit.Fingerprint}`\n");

        section.Append('\n');
        return section.ToString();
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=4D894E
    // Hairs-Falsified-If: a unit whose machine line assesses it Critical is left out of the list of units where a decision is required first
    // Hairs-Human:        PENDING
    private static string RequiredFirstSection(IReadOnlyList<AssuranceCorpusUnit> units)
    {
        AssuranceCorpusUnit[] required = [.. units.Where(static unit => unit.Annotation?.Field("Security") is "High" or "Critical")];
        var section = new StringBuilder("## 9. Where A Decision Is Required First\n\n");

        if (required.Length == 0)
        {
            section.Append("No unit is assessed `High` or `Critical`. This says nothing about the units nothing has\n");
            section.Append("assessed.\n\n");
            return section.ToString();
        }

        section.Append("The units at the top of the security vocabulary, with the observation that would show each\n");
        section.Append("one wrong and the human line it carries. The set is read from the assessments rather than\n");
        section.Append("written out, so a unit that becomes `High` joins it at the next generation.\n\n");
        foreach (AssuranceCorpusUnit unit in required)
        {
            AssuranceAnnotation annotation = unit.Annotation!;
            section.Append(
                $"- `{unit.Name}` in `{unit.File}` - Security={annotation.Field("Security")}, " +
                $"Spec={annotation.Field("Spec") ?? "none cited"}, `{unit.Fingerprint}`, " +
                $"{AssuranceHumanLine.Display(annotation)}\n");
            section.Append($"  - Falsified if: {annotation.FalsifiedIf ?? "no criterion is stated"}\n");
        }

        section.Append('\n');
        return section.ToString();
    }
}
