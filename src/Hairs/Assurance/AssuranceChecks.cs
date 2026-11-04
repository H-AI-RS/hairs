// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0
//
// HAIRS Code Assurance
// --------------------
// Relevant units:   15
// Annotated:        15/15
// Exempt:           0
// Human-reviewed:   0/15
// IP risk:          Low
// Security risk:    High
// Criteria:         14/12
// Resource impact:  6/10 max
// Unverified:       15
//
// GENERATED - DO NOT EDIT MANUALLY

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hairs.Assurance;

/// <summary>How far the check goes.</summary>
/// <param name="Release">
/// Also report every relevant unit left in a state that blocks a release (J11).
/// </param>
/// <param name="SourcesOnly">
/// Compare only the covered files and the manifest's <c>files</c> and
/// <c>units</c> arrays with what the generator would write, not the prose of
/// the report, the human-review record or the manifest's <c>$comment</c>.
/// </param>
/// <param name="AdrRecords">
/// The four-digit numbers of the decision records a <c>Spec=ADR-nnnn</c>
/// citation may name, or null to leave citations unchecked.
/// </param>
// Hairs-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=9B41C7
// Hairs-Falsified-If: a default-constructed options value has sources-only on, so a check built without arguments skips comparing the report and the manifest prose
// Hairs-Human:        PENDING
public sealed record AssuranceCheckOptions(bool Release = false, bool SourcesOnly = false, IReadOnlySet<string>? AdrRecords = null);

/// <summary>
/// The gate: every rule the assurance system asserts over its own tree, run over a plan.
///
/// <list type="table">
/// <item><term>J1</term><description>A relevant unit carries no block; an <c>EXEMPT=</c> in a closed assembly.</description></item>
/// <item><term>J2</term><description>A block that does not parse or is attached to nothing; a value outside its vocabulary; a malformed criterion; a <c>Spec=ADR-nnnn</c> naming no record.</description></item>
/// <item><term>J3</term><description>A recorded fingerprint that is the placeholder or not the current one; an approval of a version that is neither current nor preserved.</description></item>
/// <item><term>J4</term><description>A name in a generated artefact that no source human line carries; a line the generator refused to rewrite.</description></item>
/// <item><term>J5</term><description>A generated artefact that is not what the generator would write; a second banner; a summary line below the header; a header the generator refused to replace.</description></item>
/// <item><term>J6</term><description>A preprocessor directive, when the configuration forbids them.</description></item>
/// <item><term>J7</term><description>The manifest disagrees with the tree.</description></item>
/// <item><term>J9</term><description>Generated text claims a review the annotations do not hold. A check on this tool's own output.</description></item>
/// <item><term>J10</term><description>A unit assessed High or Critical carries no criterion; where the configuration refuses one below High, a unit assessed below High carries one.</description></item>
/// <item><term>J11</term><description>With <see cref="AssuranceCheckOptions.Release"/>: a relevant unit in a state that blocks a release.</description></item>
/// </list>
/// </summary>
// Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=896DF1
// Hairs-Falsified-If: a covered file whose annotation blocks or generated header differ from what generate would write passes Run with no violation
// Hairs-Human:        PENDING
public static class AssuranceChecks
{
    /// <summary>Every violation in <paramref name="plan"/>, rule by rule.</summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=BFB66A
    // Hairs-Falsified-If: a relevant unit that carries no block in an indexable covered file produces no J1 violation
    // Hairs-Human:        PENDING
    public static IReadOnlyList<AssuranceViolation> Run(
        AssurancePlan plan, AssuranceComponentConfig config, AssuranceCheckOptions options)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(options);

        var violations = new List<AssuranceViolation>(plan.Problems);
        var closed = new HashSet<string>(config.ClosedToEscapeHatch, StringComparer.Ordinal);
        string generate = plan.Context.GenerateCommand;
        string annotate =
            $"{AssuranceReportContext.Sibling(generate, "list")} prints what the unit needs, and " +
            $"{AssuranceReportContext.Sibling(generate, "insert")} --assessments <file.json> writes an assessment " +
            "(or write an EXEMPT= block where the configuration allows one)";

        foreach (AssurancePlannedFile file in plan.Files)
        {
            if (!file.Indexable)
                continue;

            string path = file.Source.RelativePath;
            IReadOnlyList<AssuranceCorpusUnit> units = file.Before;

            foreach (AssuranceCorpusUnit unit in units)
            {
                if (unit.IsRelevant && unit.Annotation is null)
                {
                    violations.Add(At("J1", unit, $"{unit.Where} is relevant and carries no assurance annotation") with
                    {
                        Remedy = annotate,
                    });
                }

                if (unit.Annotation?.ExemptReason is { } reason && closed.Contains(unit.Assembly))
                {
                    violations.Add(At("J1", unit,
                        $"{unit.Where} states EXEMPT={reason}, and {unit.Assembly} is closed to the per-unit " +
                        "exemption: a unit there is assessed or it is not shipped"));
                }
            }

            violations.AddRange(Orphans(file));

            foreach (AssuranceCorpusUnit unit in units)
            {
                if (unit.Annotation is not { } annotation)
                    continue;

                foreach (string problem in AssuranceRules.VocabularyProblems(annotation))
                    violations.Add(At("J2", unit, $"{unit.Where}: {problem}"));
            }

            if (options.AdrRecords is { } records)
                violations.AddRange(SpecViolations(units, records, config.AdrDirectory));

            violations.AddRange(FingerprintViolations(units).Select(violation => violation with { Remedy = generate }));

            if (AssuranceHeader.DuplicateBanners(path, file.Source.Text) is { } duplicate)
                violations.Add(duplicate);

            if (AssuranceHeader.ForgedSummary(path, file.Source.Text, file.Scan.CommentLines, config.ForgeryVocabulary) is { } forged)
                violations.Add(forged);

            if (config.ForbidDirectives)
            {
                foreach (AssuranceDirective directive in file.Scan.Directives)
                {
                    violations.Add(new AssuranceViolation(
                        "J6",
                        path,
                        directive.Line + 1,
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"{path}({directive.Line + 1}): carries the preprocessor directive '{directive.Text}', and a " +
                            $"covered file carries none - a directive is trivia and no fingerprint records it")));
                }
            }

            foreach ((AssuranceCorpusUnit unit, string message) in MissingCriteriaOf(units))
                violations.Add(At("J10", unit, message));

            if (config.CriteriaBelowHigh == AssuranceCriteriaBelowHigh.Refused)
            {
                foreach (AssuranceCorpusUnit unit in CriteriaBelowHighOf(units))
                {
                    violations.Add(At("J10", unit,
                        $"{unit.Where} is assessed Security={unit.Annotation!.Field("Security")} and carries a " +
                        $"'{AssuranceVocabulary.FalsifiedIfMarker}' line, which this component writes only for High and Critical") with
                    {
                        Remedy = $"{AssuranceReportContext.Sibling(generate, "prune")} removes it where the human line reads PENDING",
                    });
                }
            }
        }

        violations.AddRange(InventedApprovals(plan));
        violations.AddRange(StaleArtefacts(plan, options.SourcesOnly));

        AssuranceArtefact manifest = plan.Artefacts.Single(static artefact => artefact.Kind == AssuranceArtefactKind.Manifest);
        violations.AddRange(AssuranceManifest.Violations(
            manifest.RelativePath,
            plan.Files.Select(static file => new AssuranceManifestFile(file.Source.RelativePath, file.FileFingerprint)),
            plan.UnitsAfter,
            manifest.Current,
            generate));

        violations.AddRange(ReviewClaims(plan, options.SourcesOnly));

        if (options.Release)
            violations.AddRange(Unresolved(plan));

        return violations;
    }

    /// <summary>
    /// Every unit assessed High or Critical that carries no criterion line.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=None; Security=High; Resources=3; Fingerprint=293283
    // Hairs-Falsified-If: a unit assessed Critical with no criterion line is absent from the returned messages
    // Hairs-Human:        PENDING
    public static IReadOnlyList<string> MissingCriteria(IEnumerable<AssuranceCorpusUnit> units) =>
        [.. MissingCriteriaOf(units).Select(static missing => missing.Message)];

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=24C868
    // Hairs-Falsified-If: a block that is not an exemption and assesses security as High with no criterion line is not yielded
    // Hairs-Human:        PENDING
    private static IEnumerable<(AssuranceCorpusUnit Unit, string Message)> MissingCriteriaOf(IEnumerable<AssuranceCorpusUnit> units)
    {
        ArgumentNullException.ThrowIfNull(units);

        foreach (AssuranceCorpusUnit unit in units)
        {
            if (unit.Annotation is not { } annotation ||
                !AssuranceRules.RequiresFalsificationCriterion(annotation) ||
                annotation.HasCriterionLine)
            {
                continue;
            }

            yield return (unit,
                $"{unit.Where} is assessed Security={annotation.Field("Security")} and carries no " +
                $"'{AssuranceVocabulary.FalsifiedIfMarker}' line, so nothing at the declaration says what would make it wrong");
        }
    }

    /// <summary>Every unit whose block is assessed below High and carries a criterion line.</summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=4BF82B
    // Hairs-Falsified-If: a block assessed Medium that carries a criterion line is not yielded, so a component that refuses one passes the check
    // Hairs-Human:        PENDING
    private static IEnumerable<AssuranceCorpusUnit> CriteriaBelowHighOf(IEnumerable<AssuranceCorpusUnit> units) =>
        units.Where(static unit => unit.Annotation is { } annotation && AssuranceRules.CarriesCriterionBelowHigh(annotation));

    /// <summary>
    /// Every assurance comment that is not part of an attached block.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=306FF8
    // Hairs-Falsified-If: a criterion comment that stands under no attached AI line produces no J2 violation
    // Hairs-Human:        PENDING
    private static IEnumerable<AssuranceViolation> Orphans(AssurancePlannedFile file)
    {
        string path = file.Source.RelativePath;
        var lines = new AssuranceLines(file.Source.Text);
        var attached = new HashSet<int>();

        foreach (AssuranceCorpusUnit unit in file.Before)
        {
            if (unit.Annotation is not { } annotation)
                continue;

            attached.Add(annotation.AiLine);
            attached.Add(annotation.HumanLine);
            if (annotation.FalsifiedIfLine is { } criterion)
                attached.Add(criterion);
        }

        foreach (int line in file.Scan.AssuranceCommentLines)
        {
            if (attached.Contains(line))
                continue;

            string message;
            if (lines[line].TrimStart().StartsWith(AssuranceVocabulary.FalsifiedIfMarker, StringComparison.Ordinal))
            {
                message = $"carries a '{AssuranceVocabulary.FalsifiedIfMarker}' line that stands between no " +
                    $"'{AssuranceVocabulary.AiMarker}' line and '{AssuranceVocabulary.HumanMarker}' line";
            }
            else
            {
                AssuranceAnnotation.TryParseStrict(lines, line, out _, out string? problem);
                message = problem ?? "an assurance comment that is attached to no declaration";
            }

            yield return new AssuranceViolation(
                "J2", path, line + 1, string.Create(CultureInfo.InvariantCulture, $"{path}({line + 1}): {message}"));
        }
    }

    /// <summary>
    /// Every <c>Spec=ADR-nnnn</c> naming no record.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=4AAEE2
    // Hairs-Falsified-If: a Spec citation of ADR-0042 when the records hold no 0042 produces no J2 violation
    // Hairs-Human:        PENDING
    private static IEnumerable<AssuranceViolation> SpecViolations(
        IEnumerable<AssuranceCorpusUnit> units, IReadOnlySet<string> records, string directory)
    {
        foreach (AssuranceCorpusUnit unit in units)
        {
            if (unit.Annotation?.Field("Spec") is not { } spec || !spec.StartsWith("ADR-", StringComparison.Ordinal))
                continue;

            string number = new([.. spec["ADR-".Length..].TakeWhile(char.IsAsciiDigit)]);
            if (number.Length == 4 && records.Contains(number))
                continue;

            yield return At("J2", unit,
                $"{unit.Where} cites Spec={spec}, and {directory}/ holds no record " +
                $"{(number.Length == 0 ? "with a four-digit number" : number)}");
        }
    }

    /// <summary>
    /// Every recorded fingerprint that binds nothing or binds another version.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=ABAE2C
    // Hairs-Falsified-If: a block whose recorded fingerprint differs from the one the unit's current tokens compute produces no J3 violation
    // Hairs-Human:        PENDING
    private static IEnumerable<AssuranceViolation> FingerprintViolations(IEnumerable<AssuranceCorpusUnit> units)
    {
        foreach (AssuranceCorpusUnit unit in units)
        {
            if (unit.Annotation is not { } annotation)
                continue;

            string? recorded = annotation.RecordedFingerprint;
            if (recorded is not null)
            {
                if (string.Equals(recorded, AssuranceVocabulary.ToBeFilled, StringComparison.Ordinal))
                {
                    yield return At("J3", unit,
                        $"{unit.Where} still records the placeholder {AssuranceVocabulary.ToBeFilled}, " +
                        "so no recorded fingerprint binds anything");
                }
                else if (!string.Equals(recorded, unit.Fingerprint, StringComparison.Ordinal))
                {
                    yield return At("J3", unit,
                        $"{unit.Where} records Fingerprint={recorded} and the current code computes {unit.Fingerprint}");
                }
            }

            string? approved = annotation.HumanFingerprint;
            if (approved is null ||
                string.Equals(approved, AssuranceVocabulary.ToBeFilled, StringComparison.Ordinal) ||
                string.Equals(approved, unit.Fingerprint, StringComparison.Ordinal) ||
                (annotation.Previous is { } previous && string.Equals(previous.Fingerprint, approved, StringComparison.Ordinal)))
            {
                continue;
            }

            yield return At("J3", unit,
                $"{unit.Where} approves Fingerprint={approved}, which is neither the current {unit.Fingerprint} " +
                "nor preserved as Previous");
        }
    }

    /// <summary>
    /// Every name a generated artefact carries on a human line that no human
    /// line in the source carries.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=4; Fingerprint=AB77B2
    // Hairs-Falsified-If: an alias that a generated artefact prints on a human line, and no human line in the source files carries, produces no J4 violation
    // Hairs-Human:        PENDING
    private static IEnumerable<AssuranceViolation> InventedApprovals(AssurancePlan plan)
    {
        var carried = new HashSet<string>(StringComparer.Ordinal);
        foreach (AssurancePlannedFile file in plan.Files)
        {
            foreach (string body in AssuranceHumanLine.BodiesIn(file.Source.Text))
                carried.UnionWith(AssuranceHumanLine.ReviewerNames(body));
        }

        foreach (AssuranceArtefact artefact in plan.Artefacts)
        {
            var reported = new HashSet<string>(StringComparer.Ordinal);
            foreach (string body in AssuranceHumanLine.BodiesIn(artefact.Desired))
            {
                foreach (string name in AssuranceHumanLine.ReviewerNames(body))
                {
                    if (carried.Contains(name) || !reported.Add(name))
                        continue;

                    yield return new AssuranceViolation(
                        "J4",
                        artefact.RelativePath,
                        null,
                        $"{artefact.RelativePath} names '{name}' on a human line, and no human line in the source tree carries that name");
                }
            }
        }

        foreach (AssuranceCorpusUnit unit in plan.UnitsAfter)
        {
            if (unit.State != AssuranceUnitState.Verified)
                continue;

            if (unit.Annotation?.Reviewer is null ||
                !string.Equals(unit.Annotation.HumanFingerprint, unit.Fingerprint, StringComparison.Ordinal))
            {
                yield return At("J4", unit, $"{unit.Where} would be VERIFIED without a reviewer bound to its fingerprint");
            }
        }
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=4; Fingerprint=329817
    // Hairs-Falsified-If: with sources-only off, a report or manifest whose text differs from what the generator would write produces no J5 violation
    // Hairs-Human:        PENDING
    private static IEnumerable<AssuranceViolation> StaleArtefacts(AssurancePlan plan, bool sourcesOnly)
    {
        string generate = plan.Context.GenerateCommand;

        foreach (AssuranceArtefact artefact in plan.Artefacts)
        {
            if (artefact.IsCurrent)
                continue;

            if (artefact.Kind != AssuranceArtefactKind.Source && artefact.Exists && !sourcesOnly &&
                !AssuranceGenerator.IsGenerated(artefact.Current))
            {
                yield return new AssuranceViolation(
                    "J5",
                    artefact.RelativePath,
                    null,
                    $"{artefact.RelativePath} was not written by the generator: it carries no " +
                    $"'{AssuranceGenerator.GeneratedNotice}' line. Point \"artefacts.{AssuranceGenerator.ConfigKey(artefact.Kind)}\" " +
                    $"in {AssuranceComponentConfig.FileName} at another file, or run generate with --adopt to replace it.");
                continue;
            }

            if (artefact.Kind == AssuranceArtefactKind.Source || !sourcesOnly)
            {
                yield return new AssuranceViolation(
                    "J5",
                    artefact.RelativePath,
                    artefact.Exists ? AssuranceGenerator.FirstDifference(artefact.Current, artefact.Desired) : 1,
                    AssuranceGenerator.Describe(artefact, generate));
                continue;
            }

            if (artefact.Kind != AssuranceArtefactKind.Manifest)
                continue;

            string? current = AssuranceManifest.ArraysOf(artefact.Current);
            string desired = AssuranceManifest.ArraysOf(artefact.Desired)!;

            if (current is null)
            {
                yield return new AssuranceViolation(
                    "J5",
                    artefact.RelativePath,
                    artefact.Exists ? null : 1,
                    artefact.Exists
                        ? $"{artefact.RelativePath} has no 'files' array where the generator writes one.\n  Run: {generate}"
                        : AssuranceGenerator.Describe(artefact, generate));
                continue;
            }

            if (string.Equals(current, desired, StringComparison.Ordinal))
                continue;

            int offset = new AssuranceLines(artefact.Current[..^current.Length]).Count - 1;
            int? first = AssuranceGenerator.FirstDifference(current, desired);
            yield return new AssuranceViolation(
                "J5",
                artefact.RelativePath,
                first + offset,
                AssuranceGenerator.Describe(artefact.RelativePath, current, desired, offset, generate));
        }
    }

    /// <summary>
    /// The review-claim rule over what the generator would write.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=DAAAF2
    // Hairs-Falsified-If: with sources-only on, a review claim written into the manifest comment on disk produces no J9 violation
    // Hairs-Human:        PENDING
    private static IEnumerable<AssuranceViolation> ReviewClaims(AssurancePlan plan, bool sourcesOnly)
    {
        ILookup<string, AssuranceCorpusUnit> byFile = plan.UnitsAfter.ToLookup(static unit => unit.File, StringComparer.Ordinal);

        foreach (AssuranceArtefact artefact in plan.Artefacts)
        {
            if (sourcesOnly && artefact.Kind is AssuranceArtefactKind.Report or AssuranceArtefactKind.HumanReview)
                continue;

            string text = sourcesOnly && artefact.Kind == AssuranceArtefactKind.Manifest ? artefact.Current : artefact.Desired;
            IReadOnlyList<AssuranceViolation> found = artefact.Kind == AssuranceArtefactKind.Source
                ? AssuranceReviewClaims.Violations(
                    artefact.RelativePath,
                    AssuranceHeader.GeneratedHeaderLines(artefact.Desired),
                    [.. byFile[artefact.RelativePath]])
                : AssuranceReviewClaims.Violations(
                    artefact.RelativePath, Lines(text), plan.UnitsAfter);

            foreach (AssuranceViolation violation in found)
                yield return violation;
        }
    }

    /// <summary>
    /// Every relevant unit the plan leaves in a state that blocks a release.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=74A884
    // Hairs-Falsified-If: with the release option on, a relevant unit left in a state that blocks a release produces no J11 violation
    // Hairs-Human:        PENDING
    private static IEnumerable<AssuranceViolation> Unresolved(AssurancePlan plan)
    {
        foreach (AssurancePlannedFile file in plan.Files)
        {
            for (int index = 0; index < file.After.Count; index++)
            {
                AssuranceCorpusUnit after = file.After[index];
                if (!after.IsRelevant || !AssuranceStateMachine.BlocksRelease(after.State))
                    continue;

                AssuranceCorpusUnit at = index < file.Before.Count ? file.Before[index] : after;
                yield return At("J11", at,
                    $"{at.Where} is {AssuranceStateMachine.Name(after.State)} and its human line reads " +
                    $"'{AssuranceHumanLine.Display(after.Annotation)}'");
            }
        }
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=9368F2
    // Hairs-Falsified-If: a line of the artefact text is dropped or merged with its neighbour, so a claim on it is never judged
    // Hairs-Human:        PENDING
    private static List<string> Lines(string text)
    {
        var lines = new AssuranceLines(text);
        var list = new List<string>(lines.Count);
        for (int index = 0; index < lines.Count; index++)
            list.Add(lines[index]);

        return list;
    }

    // Hairs-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=EAFAF7
    // Hairs-Human:        PENDING
    private static AssuranceViolation At(string rule, AssuranceCorpusUnit unit, string message) =>
        new(rule, unit.File, unit.Line, message);
}
