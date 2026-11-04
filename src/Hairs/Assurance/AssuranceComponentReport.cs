// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0
//
// HAIRS Code Assurance
// --------------------
// Relevant units:   5
// Annotated:        5/5
// Exempt:           0
// Human-reviewed:   0/5
// IP risk:          Low
// Security risk:    Medium
// Criteria:         3/0
// Resource impact:  4/10 max
// Unverified:       5
//
// GENERATED - DO NOT EDIT MANUALLY

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hairs.Assurance;

/// <summary>
/// <c>CODE-ASSURANCE.md</c>: what the annotations of one component state, as
/// figures, derived from the units the generator is about to leave behind.
/// </summary>
// Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=18C351
// Hairs-Falsified-If: the report's count of decided units exceeds the number of relevant units whose human line binds a decision to their current fingerprint
// Hairs-Human:        PENDING
public static class AssuranceComponentReport
{
    /// <summary>The report for <paramref name="units"/>, the post-generation units of every covered file.</summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=0EE6F4
    // Hairs-Falsified-If: the report's count of decided units exceeds the number of relevant units whose human line binds a decision to their current fingerprint
    // Hairs-Human:        PENDING
    public static string Render(AssuranceReportContext context, IReadOnlyList<AssuranceCorpusUnit> units)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(units);

        AssuranceSummary summary = AssuranceSummary.Of(units);
        AssuranceArtefactPaths paths = context.Paths;
        var report = new StringBuilder();

        report.Append($"# {context.Component} Code Assurance\n\n");
        report.Append("GENERATED - DO NOT EDIT MANUALLY. Regenerate with\n");
        report.Append($"`{context.GenerateCommand}`, which rewrites this file,\n");
        report.Append($"`{paths.HumanReview}`, `{paths.Manifest}` and every generated source header from the\n");
        report.Append("product tree.\n\n");

        report.Append(summary.Verified == 0
            ? "**No code unit in this component carries a decision on its human line yet.** This report\n" +
              "records that absence precisely. It is not a claim that the code is reviewed, assured or safe,\n" +
              "and the figures below are the measurement of how far from that claim the per-unit record is.\n\n"
            : $"**Human-reviewed: {AssuranceFormat.Count(summary.Verified)} of {AssuranceFormat.Count(summary.Relevant)} relevant units.** This report records what the\n" +
              "annotations state and no more. A decision recorded here is one person's, bound to one\n" +
              "version of one declaration, and it is not a claim that the code is assured or safe.\n\n");

        foreach (string record in context.SeparateRecords)
        {
            report.Append($"`{record}` is a separate, hand-written review record of this component. This report\n");
            report.Append("neither reads it nor summarizes it, and nothing in it is counted here.\n\n");
        }

        report.Append("## Summary\n\n");
        report.Append("| Metric | Value |\n|---|---:|\n");
        report.Append($"| Files scanned | {AssuranceFormat.Count(context.CoveredFiles.Count)} |\n");
        report.Append($"| Files not covered | {AssuranceFormat.Count(context.Excluded.Count)} |\n");
        report.Append($"| Files carrying an annotation | {AssuranceFormat.Count(AnnotatedFiles(units))} |\n");
        report.Append($"| Code units | {AssuranceFormat.Count(units.Count)} |\n");
        report.Append($"| Relevant | {AssuranceFormat.Count(summary.Relevant)} |\n");
        report.Append($"| Exempt by predicate | {AssuranceFormat.Count(summary.Exempt)} |\n");
        report.Append($"| Annotated | {AssuranceFormat.Portion(summary.Annotated, summary.Relevant)} |\n");
        report.Append($"| Human reviewed | {AssuranceFormat.Portion(summary.Verified, summary.Relevant)} |\n");
        report.Append($"| Unverified | {AssuranceFormat.Count(summary.Unverified)} |\n\n");

        report.Append("## Review states\n\n");
        report.Append("| State | Count |\n|---|---:|\n");
        foreach (AssuranceUnitState state in AssuranceVocabulary.States)
            report.Append($"| {AssuranceStateMachine.Name(state)} | {AssuranceFormat.Count(units.Count(unit => unit.State == state))} |\n");

        report.Append('\n');
        report.Append(Distribution("IP risk", AssuranceVocabulary.IpRiskValues, "IP", units, summary));
        report.Append(Distribution("Security risk", AssuranceVocabulary.SecurityRiskValues, "Security", units, summary));

        report.Append("## Resource impact\n\n");
        report.Append("| Metric | Value |\n|---|---:|\n");
        report.Append($"| Maximum | {(summary.MaxResources is { } max ? AssuranceFormat.Count(max) + " / 10" : "n/a")} |\n");
        report.Append($"| Average over annotated units | {(summary.MeanResources is { } mean ? AssuranceFormat.Mean(mean) + " / 10" : "n/a")} |\n");
        report.Append($"| Units scored | {AssuranceFormat.Count(summary.Annotated)} |\n\n");

        report.Append("## High-security review areas\n\n");

        string[] high = [.. units
            .Where(static unit => unit.Annotation?.Field("Security") is "High" or "Critical")
            .Select(static unit =>
                $"- `{unit.Name}` in `{unit.File}` - Security={unit.Annotation!.Field("Security")}, " +
                $"human line {AssuranceHumanLine.Display(unit.Annotation)}")];

        report.Append(high.Length == 0
            ? "No annotated unit is assessed High or Critical. This says nothing about the units nothing\nhas assessed.\n\n"
            : string.Join("\n", high) + "\n\n");

        report.Append("## Falsification criteria\n\n");
        report.Append("| Metric | Value |\n|---|---:|\n");
        report.Append($"| Units carrying a criterion | {AssuranceFormat.Count(summary.Criteria)} |\n");
        report.Append($"| Units required to carry one | {AssuranceFormat.Count(summary.CriteriaRequired)} |\n");
        report.Append($"| Required and missing | {AssuranceFormat.Count(AssuranceChecks.MissingCriteria(units).Count)} |\n\n");
        report.Append($"A `{AssuranceVocabulary.FalsifiedIfMarker}` line states, at the declaration, the observation that would make\n");
        report.Append("the unit wrong. `Security=High` says a unit is risky, which is a set and not a test; the\n");
        report.Append("criterion is the test. It is required where `Security` is `High` or `Critical`");

        report.Append(context.CriteriaBelowHighRefused
            ? " and written\n" +
              $"nowhere else: `{context.CheckCommand}` names every unit that owes one and carries none, and\n" +
              "every unit below `High` that carries one.\n\n"
            : ", permitted\n" +
              $"elsewhere, and `{context.CheckCommand}` names every unit that owes one and carries none.\n\n");
        report.Append("The line is a comment, so it is outside every fingerprint by construction: rewording a\n");
        report.Append("criterion moves no recorded value here, in a file header or in\n");
        report.Append($"`{paths.Manifest}`, and invalidates nothing. That is the intended reading - a\n");
        report.Append("criterion is an instruction to whoever reads the unit, not part of what a review is bound to.\n\n");

        report.Append("## Exemption\n\n");
        report.Append("Exemption is decided by one predicate in `CSharpAssuranceScanner`, not per unit, so\n");
        report.Append("that the rule is reviewable in one place rather than in several hundred.\n\n");
        report.Append("| Case | Units |\n|---|---:|\n");
        foreach (string exemption in AssuranceVocabulary.ExemptionCases)
        {
            if (exemption == AssuranceVocabulary.NamedValue && !context.NamedValuesWatched)
                continue;

            report.Append(
                $"| {exemption} | {AssuranceFormat.Count(units.Count(unit => string.Equals(unit.Exemption, exemption, StringComparison.Ordinal)))} |\n");
        }

        if (context.NamedValuesWatched)
        {
            report.Append($"\n`{AssuranceVocabulary.NamedValue}` is this component's choice (`\"namedValues\": \"watched\"`): a `const`\n");
            report.Append("field, an enum declaration and a `static readonly` `Guid`, `IntPtr`, `UIntPtr`, `nint` or `nuint`\n");
            report.Append("stated by literals alone is a value with a name and carries no decision to assess, so it\n");
            report.Append($"carries no annotation. It is still a unit with an entry in `{paths.Manifest}`, so a\n");
            report.Append("change to its value moves a fingerprint the check compares.\n");
        }

        AssuranceCorpusUnit[] declared = [.. units.Where(static unit => unit.Annotation?.ExemptReason is not null)];

        report.Append("\n## Per-unit exemptions\n\n");
        report.Append($"| Metric | Value |\n|---|---:|\n| Per-unit exemptions | {AssuranceFormat.Count(declared.Length)} |\n\n");
        report.Append("A per-unit `EXEMPT=<reason>` line exempts one unit by a reason a human wrote, for what the\n");
        report.Append("predicate cannot see. Nothing mechanical checks that the reason is true, that it describes\n");
        report.Append("the unit it sits on, or that it says anything at all, so every use is counted and named\n");
        report.Append("here.");
        report.Append(context.ClosedToEscapeHatch.Count switch
        {
            0 => "\n\n",
            1 => $" {AssuranceFormat.CodeList(context.ClosedToEscapeHatch)} is closed to it entirely: a unit there is\n" +
                 $"assessed or it is not shipped, and `{context.CheckCommand}` reports any use of it there.\n\n",
            _ => $" {AssuranceFormat.CodeList(context.ClosedToEscapeHatch)} are closed to it entirely: a unit\n" +
                 $"there is assessed or it is not shipped, and `{context.CheckCommand}` reports any use of it there.\n\n",
        });

        report.Append(declared.Length == 0
            ? "No unit in this component states a per-unit exemption.\n\n"
            : string.Join(
                "\n",
                declared.Select(static unit => $"- `{unit.Name}` in `{unit.File}` - {unit.Annotation!.ExemptReason}")) + "\n\n");

        report.Append("## Files not covered\n\n");
        if (context.Excluded.Count == 0)
        {
            report.Append("No file under a covered project's directory, and no file a covered project compiles in\n");
            report.Append("through a `<Compile Include>` it states, is left out of the record.\n\n");
        }
        else
        {
            report.Append("These files are under a covered project, or compiled into one, and are left out of the\n");
            report.Append($"record. They carry no generated header, no unit of theirs is in `{paths.Manifest}`, and\n");
            report.Append("no figure in this report counts them. A path ending in `/` is a directory the tool does not\n");
            report.Append("enter, and none of its files is covered.\n\n");
            report.Append("| File | Reason |\n|---|---|\n");
            foreach (AssuranceExcludedSource excluded in context.Excluded)
                report.Append($"| `{excluded.RelativePath}` | {excluded.Reason} |\n");

            report.Append('\n');
        }

        report.Append("## Change detection\n\n");
        report.Append($"`{paths.Manifest}` lists **every** code unit in {AssembliesPhrase(context.Assemblies.Count)} -\n");
        report.Append($"{AssuranceFormat.Count(units.Count)} of them, exempt and relevant alike - with the fingerprint of its declaration.\n");
        report.Append($"{AssuranceManifest.ChangeDetectionStatement} A unit listed there is watched, not reviewed:\n");
        report.Append("the entry records what the declaration's tokens hashed to when the generator last ran, and\n");
        report.Append("nothing else. What the manifest adds is that a unit the exemption predicate treats as\n");
        report.Append("trivial is no longer invisible: a semantic change to one moves a value in a generated file\n");
        report.Append($"the check compares byte for byte. `{context.CheckCommand}` holds the manifest to the tree.\n\n");
        report.Append($"Beside the units it lists **every covered file** - {AssuranceFormat.Count(context.CoveredFiles.Count)} of them - with a\n");
        report.Append("fingerprint over the complete token stream of its compilation unit. A unit entry exists only\n");
        report.Append("for a declaration kind the scanner enumerates, and an enumeration is a whitelist: an\n");
        report.Append("`[assembly: ...]` attribute is a member of nothing and can be in no unit at all.\n");
        report.Append($"{AssuranceManifest.CompletenessStatement} Comments are outside the stream, because a token's\n");
        report.Append("text is its own characters, so the generated header above and the annotation lines below move\n");
        report.Append("no file fingerprint - which is what lets one generation be a fixed point.\n\n");

        report.Append("## Verification\n\n");
        report.Append($"The generator and the check are one computation: `{context.CheckCommand}` works out what the\n");
        report.Append("generator would write and compares it with the tree byte for byte, so a record edited by\n");
        report.Append("hand, or left behind by code that moved, is reported rather than trusted.\n\n");
        report.Append("| Mode | Command | Effect |\n|---|---|---|\n");
        report.Append(
            $"| Generate | `{context.GenerateCommand}` | Fills every `Fingerprint=TBF`, refreshes a decision the code has outrun into " +
            $"`STALE; Previous=...`, rewrites the generated headers, `{paths.HumanReview}`, `{paths.Manifest}` and this file. |\n");
        report.Append(
            $"| Check | `{context.CheckCommand}` | Reports every generated artefact that is not byte-identical to what the generator " +
            "would produce, every relevant unit with no annotation, every annotation this system cannot read, every " +
            "fingerprint out of date and every unit at the top of the security vocabulary without a criterion" +
            (context.CriteriaBelowHighRefused ? ", or below it with one" : string.Empty) + ". |\n");
        report.Append(
            $"| Release | `{context.CheckCommand} --release` | The check, and additionally every relevant unit left in a " +
            "state that blocks a release. |\n\n");
        report.Append("The fingerprint is six hex characters - 24 bits - of SHA-256 over the declaration's token\n");
        report.Append("texts, joined by single spaces. Trivia is excluded because a token's text is its own\n");
        report.Append("characters and never the comments or whitespace around it, so `dotnet format` moves no\n");
        report.Append("fingerprint and an annotation is never part of what it describes. The value answers whether a\n");
        report.Append("unit changed since it was reviewed. It is not a collision-free identifier across units and it\n");
        report.Append("is not a cryptographic commitment.\n");

        return report.ToString();
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=3; Fingerprint=EAE22A
    // Hairs-Human:        PENDING
    private static int AnnotatedFiles(IEnumerable<AssuranceCorpusUnit> units) => units
        .Where(static unit => unit.Annotation is not null)
        .Select(static unit => unit.File)
        .Distinct(StringComparer.Ordinal)
        .Count();

    /// <summary>
    /// One row per vocabulary value counting every unit whose block states it,
    /// exempt ones included, and a last row for the relevant units no block
    /// assesses.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=3; Fingerprint=EF5D73
    // Hairs-Falsified-If: the not-annotated row differs from the relevant units minus the annotated ones
    // Hairs-Human:        PENDING
    private static string Distribution(
        string heading,
        IReadOnlyList<string> vocabulary,
        string field,
        IReadOnlyList<AssuranceCorpusUnit> units,
        AssuranceSummary summary)
    {
        var section = new StringBuilder($"## {heading}\n\n| Value | Units |\n|---|---:|\n");
        foreach (string value in vocabulary)
        {
            int count = units.Count(unit => string.Equals(unit.Annotation?.Field(field), value, StringComparison.Ordinal));
            section.Append($"| {value} | {AssuranceFormat.Count(count)} |\n");
        }

        section.Append($"| *not annotated* | {AssuranceFormat.Count(summary.Relevant - summary.Annotated)} |\n\n");
        return section.ToString();
    }

    // Hairs-AI:           Origin=AI; IP=None; Security=None; Resources=1; Fingerprint=43DBD5
    // Hairs-Human:        PENDING
    private static string AssembliesPhrase(int count) =>
        count == 1 ? "the covered assembly" : $"the {AssuranceFormat.Count(count)} covered assemblies";
}
