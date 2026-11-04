// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0
//
// HAIRS Code Assurance
// --------------------
// Relevant units:   11
// Annotated:        11/11
// Exempt:           2
// Human-reviewed:   0/11
// IP risk:          Low
// Security risk:    High
// Criteria:         6/6
// Resource impact:  6/10 max
// Unverified:       11
//
// GENERATED - DO NOT EDIT MANUALLY

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hairs.Assurance;

/// <summary>What prune removes from one block.</summary>
// Hairs-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=FA01A2
// Hairs-Human:        PENDING
public enum AssurancePruneKind
{
    /// <summary>The whole block, above a unit the exemption predicate exempts.</summary>
    Block = 0,

    /// <summary>The falsified-if criterion line of a block assessed below High.</summary>
    Criterion,
}

/// <summary>One block prune removed lines from, or left in place.</summary>
/// <param name="Unit">The unit's name, as <c>assurance list</c> prints it.</param>
/// <param name="Line">The 1-based line, in the file as read, of the first line removed or left.</param>
/// <param name="Kind">What was removed, or would have been.</param>
/// <param name="Removed">True when the lines were (or, in a dry run, would be) removed; false when they were left.</param>
/// <param name="Reason">Why: the exemption or the security value, or what the human line reads that kept them.</param>
// Hairs-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=A114E9
// Hairs-Human:        PENDING
public sealed record AssurancePruneEntry(string Unit, int Line, AssurancePruneKind Kind, bool Removed, string Reason);

/// <summary>The outcome for one file.</summary>
/// <param name="Text">The new text, or the text as read when nothing was removed.</param>
/// <param name="Entries">Every block prune removed lines from or left in place, in file order.</param>
/// <param name="Problem">
/// Why nothing was removed from the file, said of the file (it reads after
/// "the file"), or null.
/// </param>
// Hairs-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=10100C
// Hairs-Human:        PENDING
public sealed record AssurancePruneFileResult(string Text, IReadOnlyList<AssurancePruneEntry> Entries, string? Problem)
{
    // Hairs-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=536DAF
    // Hairs-Human:        PENDING
    public bool Changed => Problem is null && Entries.Any(static entry => entry.Removed);
}

/// <summary>
/// Takes annotation lines out that the rubric no longer asks for, and nothing
/// else: the block above a unit the exemption predicate now exempts (a named
/// value, once a component watches them), and the criterion line of a block
/// assessed <c>None</c>, <c>Low</c> or <c>Medium</c>, which the rubric writes
/// only for <c>High</c> and <c>Critical</c>.
///
/// Only a block whose human line reads exactly <c>PENDING</c> is touched. A
/// line that names someone, or says <c>STALE</c>, records what a person did,
/// and deleting it would erase that; such a block is left as it is and
/// reported, for a person to decide.
///
/// Whole lines go, each with its own line ending, so every other byte stays as
/// it was. Afterwards the file is scanned again, and nothing is removed from it
/// unless every unit and the file fingerprint are unchanged, every block that
/// remains reads as it did, and putting the removed lines back gives the
/// original text exactly.
/// </summary>
// Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=A23C54
// Hairs-Falsified-If: the text Apply returns differs from the text as read in a line other than an assurance comment of a block whose human line reads exactly PENDING
// Hairs-Human:        PENDING
public static class AssurancePruning
{
    /// <summary>Computes what to remove from one file. Reads nothing but its arguments.</summary>
    /// <param name="text">The file's text, decoded, without a byte-order mark.</param>
    /// <param name="path">The file's root-relative path, for the parser and for messages.</param>
    /// <param name="scanner">The scanner the component's configuration asks for.</param>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=D0807E
    // Hairs-Falsified-If: a block whose human line reads anything other than exactly PENDING, such as PENDING followed by a fingerprint part, loses a line in the text Apply returns
    // Hairs-Human:        PENDING
    public static AssurancePruneFileResult Apply(string text, string path, IAssuranceFileScanner scanner)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(scanner);

        var lines = new AssuranceLines(text);
        AssuranceScannedFile scan = scanner.ScanFile(text, path);
        // A file with no assurance comment has nothing to remove, however it
        // breaks its lines.
        if (scan.LineCount != lines.Count)
        {
            return new AssurancePruneFileResult(text, [], scan.AssuranceCommentLines.Count == 0
                ? null
                : "breaks lines on U+0085, U+2028 or U+2029, which the parser counts as line breaks and the " +
                  "annotation line model does not, so no line in it can be removed by number");
        }

        IReadOnlyList<AssuranceCandidate> candidates = AssuranceCandidates.Classify(lines, scan);
        var comments = new HashSet<int>(scan.AssuranceCommentLines);
        var entries = new List<AssurancePruneEntry>();
        var removals = new Dictionary<int, (int First, int Count, AssurancePruneKind Kind)>();
        var taken = new HashSet<int>();

        for (int index = 0; index < candidates.Count; index++)
        {
            AssuranceCandidate candidate = candidates[index];
            if (candidate.Annotation is not { } annotation)
                continue;

            (int First, int Count, AssurancePruneKind Kind, string Reason)? removal = RemovalFor(candidate, annotation);
            if (removal is not { } found)
                continue;

            if (!annotation.HumanIsPending)
            {
                entries.Add(new AssurancePruneEntry(candidate.Unit.Name, found.First + 1, found.Kind, false,
                    $"its human line reads '{AssuranceHumanLine.Display(annotation)}', which records what a " +
                    "person did; left in place for a person to decide"));
                continue;
            }

            // Every line goes only if it is an assurance comment that opens its
            // own line, as the parser sees it, and no other removal claims it.
            for (int line = found.First; line < found.First + found.Count; line++)
            {
                if (!comments.Contains(line) || !taken.Add(line) || lines.SeparatorOf(line).Length == 0)
                {
                    return Refused(text, entries,
                        string.Create(CultureInfo.InvariantCulture,
                            $"has no assurance comment of its own on line {line + 1}, which prune would remove for {candidate.Unit.Name}"));
                }
            }

            removals[index] = (found.First, found.Count, found.Kind);
            entries.Add(new AssurancePruneEntry(candidate.Unit.Name, found.First + 1, found.Kind, true, found.Reason));
        }

        if (removals.Count == 0)
            return new AssurancePruneFileResult(text, entries, null);

        // Bottom-up, so every line not yet removed is still where the scan found it.
        var pruned = new AssuranceLines(text);
        var removed = new List<(int Line, string Text, string Separator)>();
        foreach ((int first, int count, _) in removals.Values.OrderByDescending(static removal => removal.First))
        {
            for (int line = first; line < first + count; line++)
                removed.Add((line, lines[line], lines.SeparatorOf(line)));

            pruned.RemoveRange(first, count);
        }

        string result = pruned.Render();
        string? failure = Verify(text, result, path, scanner, scan, candidates, removals, removed);
        return failure is null
            ? new AssurancePruneFileResult(result, entries, null)
            : Refused(text, entries, $"did not scan the same after removing ({failure})");
    }

    /// <summary>
    /// What changed in the code between two scans of one file, or null when
    /// nothing did: the file fingerprint, the number of units, or a unit's
    /// name, fingerprint or exemption.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=B9CB54
    // Hairs-Falsified-If: two scans whose units match in name and fingerprint but differ in exemption are answered with null
    // Hairs-Human:        PENDING
    public static string? CodeDifference(AssuranceScannedFile before, AssuranceScannedFile after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        if (!string.Equals(before.FileFingerprint, after.FileFingerprint, StringComparison.Ordinal))
            return $"the file fingerprint moved from {before.FileFingerprint} to {after.FileFingerprint}";

        if (before.Units.Count != after.Units.Count)
            return "the number of code units changed";

        for (int index = 0; index < before.Units.Count; index++)
        {
            AssuranceScannedUnit was = before.Units[index].Unit;
            AssuranceScannedUnit now = after.Units[index].Unit;
            if (!string.Equals(was.Name, now.Name, StringComparison.Ordinal) ||
                !string.Equals(was.Fingerprint, now.Fingerprint, StringComparison.Ordinal) ||
                !string.Equals(was.Exemption, now.Exemption, StringComparison.Ordinal))
            {
                return $"unit {was.Name} changed";
            }
        }

        return null;
    }

    /// <summary>
    /// The lines prune would take from one parsed block, or null: the whole
    /// block above a unit the predicate exempts, whatever the block says, or
    /// the criterion line of an assessment below High.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=1; Fingerprint=4FB20A
    // Hairs-Falsified-If: a block assessed High or Critical, or a block whose own exemption reason is what exempts its unit, is given lines to remove
    // Hairs-Human:        PENDING
    private static (int First, int Count, AssurancePruneKind Kind, string Reason)? RemovalFor(
        AssuranceCandidate candidate, AssuranceAnnotation annotation)
    {
        // The predicate's exemption, not an EXEMPT= written in the block: that
        // block is what exempts its unit, and taking it away would not.
        if (candidate.Unit.IsExempt)
        {
            return (annotation.AiLine, annotation.HumanLine - annotation.AiLine + 1, AssurancePruneKind.Block,
                $"exempt: {candidate.Unit.Exemption}");
        }

        // The rule the check applies where a component refuses these, so that
        // what prune leaves is what that check accepts.
        if (AssuranceRules.CarriesCriterionBelowHigh(annotation))
        {
            return (annotation.FalsifiedIfLine!.Value, 1, AssurancePruneKind.Criterion,
                $"Security={annotation.Field("Security")}");
        }

        return null;
    }

    /// <summary>
    /// Rescans the pruned text and checks that removing changed nothing but
    /// the lines it was meant to take out. Returns what went wrong, or null.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=94EC85
    // Hairs-Falsified-If: a pruned text that differs from the original outside the removed lines, or whose remaining block lost its criterion or changed a field, is answered with null
    // Hairs-Human:        PENDING
    private static string? Verify(
        string original,
        string pruned,
        string path,
        IAssuranceFileScanner scanner,
        AssuranceScannedFile before,
        IReadOnlyList<AssuranceCandidate> candidatesBefore,
        IReadOnlyDictionary<int, (int First, int Count, AssurancePruneKind Kind)> removals,
        IReadOnlyList<(int Line, string Text, string Separator)> removed)
    {
        var lines = new AssuranceLines(pruned);
        AssuranceScannedFile after = scanner.ScanFile(pruned, path);

        if (after.LineCount != lines.Count)
            return "the parser and the line model count different lines";

        if (CodeDifference(before, after) is { } difference)
            return difference;

        IReadOnlyList<AssuranceCandidate> candidatesAfter = AssuranceCandidates.Classify(lines, after);
        for (int index = 0; index < candidatesAfter.Count; index++)
        {
            AssuranceCandidate was = candidatesBefore[index];
            AssuranceCandidate now = candidatesAfter[index];

            if (removals.TryGetValue(index, out (int First, int Count, AssurancePruneKind Kind) removal))
            {
                if (removal.Kind == AssurancePruneKind.Block)
                {
                    if (now.Annotation is not null || now.Source.LeadingAssuranceLines.Count > 0)
                        return $"{was.Unit.Name} still carries an assurance line after its block was removed";

                    continue;
                }

                if (now.Annotation is not { } kept || kept.HasCriterionLine || !SameAssessment(was.Annotation!, kept))
                    return $"the block of {was.Unit.Name} does not read back as the same block without its criterion";

                continue;
            }

            // A unit nothing was removed from keeps exactly what it had.
            if (was.IsAnnotated != now.IsAnnotated ||
                (was.Annotation is { } wasBlock && now.Annotation is { } nowBlock &&
                 (!SameAssessment(wasBlock, nowBlock) ||
                  !string.Equals(wasBlock.FalsifiedIf, nowBlock.FalsifiedIf, StringComparison.Ordinal))))
            {
                return $"the annotation on {was.Unit.Name} changed";
            }
        }

        if (after.AssuranceCommentLines.Count != before.AssuranceCommentLines.Count - removed.Count)
            return "an assurance comment appeared or disappeared outside the removed lines";

        // The strongest check: put the removed lines back, each with its own
        // ending, and the original must come back byte for byte.
        var restored = new AssuranceLines(pruned);
        foreach ((int line, string text, string separator) in removed.OrderBy(static entry => entry.Line))
            restored.Insert(line, [text], separator);

        return string.Equals(restored.Render(), original, StringComparison.Ordinal)
            ? null
            : "putting the removed lines back does not give the original text";
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=1; Fingerprint=EE93FA
    // Hairs-Falsified-If: two blocks whose human lines differ, or whose machine fields differ in order, are answered the same assessment
    // Hairs-Human:        PENDING
    private static bool SameAssessment(AssuranceAnnotation before, AssuranceAnnotation after) =>
        before.Fields.SequenceEqual(after.Fields) &&
        string.Equals(before.HumanBody, after.HumanBody, StringComparison.Ordinal);

    /// <summary>
    /// The file as read, with every removal it planned turned into one it did
    /// not make. <paramref name="problem"/> is said of the file: it reads after
    /// "the file".
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=None; Security=Medium; Resources=2; Fingerprint=A8D9D5
    // Hairs-Human:        PENDING
    private static AssurancePruneFileResult Refused(string text, IEnumerable<AssurancePruneEntry> entries, string problem) =>
        new(text,
            [.. entries.Select(entry => entry.Removed
                ? entry with { Removed = false, Reason = $"not removed: the file {problem}" }
                : entry)],
            $"{problem}; nothing was removed from it");
}
