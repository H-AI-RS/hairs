// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0
//
// HAIRS Code Assurance
// --------------------
// Relevant units:   17
// Annotated:        17/17
// Exempt:           13
// Human-reviewed:   0/17
// IP risk:          Low
// Security risk:    High
// Criteria:         15/8
// Resource impact:  6/10 max
// Unverified:       17
//
// GENERATED - DO NOT EDIT MANUALLY

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Hairs.Assurance;

/// <summary>
/// One machine assessment to write above one unit, as the command-line input
/// states it.
///
/// There is no human field, and that is the safety property: whatever the input
/// says, the human line written is <c>PENDING</c>. An assessment is a claim by
/// whoever produced it, and it is recorded as such, with <c>Fingerprint=TBF</c>
/// for the generator to fill in.
/// </summary>
// Hairs-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=83C9B1
// Hairs-Falsified-If: a value carried by an input entry reaches the human line of the block Render writes, so that line reads other than PENDING
// Hairs-Human:        PENDING
public sealed record AssuranceAssessment
{
    /// <summary>Position in the input, for reporting.</summary>
    public int Index { get; init; }

    /// <summary>The file, relative to the component root, with forward slashes.</summary>
    public required string File { get; init; }

    /// <summary>The unit's qualified name, as <c>assurance list</c> prints it.</summary>
    public required string Unit { get; init; }

    /// <summary>The fingerprint of the version that was assessed.</summary>
    public string? Fingerprint { get; init; }

    public string? Origin { get; init; }

    public string? Spec { get; init; }

    public string? Ip { get; init; }

    public string? Security { get; init; }

    public int? Resources { get; init; }

    public int? Cs { get; init; }

    public int? Cl { get; init; }

    public string? FalsifiedIf { get; init; }

    /// <summary>The reason, for the <c>EXEMPT=</c> form. Null for an assessment.</summary>
    public string? Exempt { get; init; }

    // Hairs-AI:           Origin=AI; IP=None; Security=Medium; Resources=0; Fingerprint=D9004F
    // Hairs-Falsified-If: an entry whose Exempt is the empty string is validated as an assessment instead of being refused for carrying no reason
    // Hairs-Human:        PENDING
    public bool IsExemption => Exempt is not null;
}

/// <summary>What happened to one input entry.</summary>
/// <param name="Entry">The entry.</param>
/// <param name="Applied">True when the block was (or, in a dry run, would be) written.</param>
/// <param name="Message">What was done, or why not.</param>
/// <param name="Line">The 1-based line the block's first line has in the new text, when applied.</param>
// Hairs-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=4B0151
// Hairs-Human:        PENDING
public sealed record AssuranceInsertEntryResult(
    AssuranceAssessment Entry,
    bool Applied,
    string Message,
    int? Line = null);

/// <summary>The outcome for one file.</summary>
/// <param name="Text">The new text, or the original when nothing was applied.</param>
/// <param name="Entries">One result per entry, in input order.</param>
// Hairs-AI:           Origin=AI; IP=None; Security=Low; Resources=2; Fingerprint=3BF13A
// Hairs-Falsified-If: a result with at least one applied entry reports Changed false, so the inserted text is never written
// Hairs-Human:        PENDING
public sealed record AssuranceInsertFileResult(string Text, IReadOnlyList<AssuranceInsertEntryResult> Entries)
{
    // Hairs-AI:           Origin=AI; IP=None; Security=Low; Resources=2; Fingerprint=D70263
    // Hairs-Falsified-If: a result whose entries were all refused reports Changed true
    // Hairs-Human:        PENDING
    public bool Changed => Entries.Any(static entry => entry.Applied);
}

/// <summary>
/// Writes machine assessments above unannotated units, and nothing else.
///
/// The block goes directly above the declaration's first line: below its
/// documentation comment and above its attributes, at its indentation.
/// It reads:
/// <code>
/// // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=2; Fingerprint=TBF
/// // Hairs-Falsified-If: &lt;one line of prose&gt;
/// // Hairs-Human:        PENDING
/// </code>
///
/// Every other byte of the file is kept: each inserted line takes the ending of
/// the line above it, and untouched lines keep their own. After inserting, the
/// file is scanned again, and the insert is thrown away unless every unit and
/// the file fingerprint are unchanged, every new block is attached to its unit,
/// and deleting the new lines gives back the original text exactly.
/// </summary>
// Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=160B8B
// Hairs-Falsified-If: text returned by Apply holds a newly inserted human line that reads anything other than PENDING
// Hairs-Human:        PENDING
public static class AssuranceInsertion
{
    /// <summary>
    /// Every problem with an entry that can be found without the file. Empty
    /// means the entry is well formed.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=B1A5EC
    // Hairs-Falsified-If: an entry assessed High or Critical whose falsification criterion is only whitespace is returned with no problem
    // Hairs-Human:        PENDING
    public static IReadOnlyList<string> Validate(AssuranceAssessment entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var problems = new List<string>();

        if (entry.IsExemption)
        {
            if (entry.Origin is not null || entry.Spec is not null || entry.Ip is not null ||
                entry.Security is not null || entry.Resources is not null || entry.Cs is not null ||
                entry.Cl is not null || entry.FalsifiedIf is not null)
            {
                problems.Add("states EXEMPT beside assessment fields; an exemption is not an assessment");
            }

            string reason = entry.Exempt!.Trim();
            if (reason.Length == 0)
                problems.Add("EXEMPT carries no reason");
            else if (reason.Contains(';', StringComparison.Ordinal))
                problems.Add("the EXEMPT reason contains ';', which would end the field");

            if (!IsOneLine(reason))
                problems.Add("the EXEMPT reason must be one line");

            problems.AddRange(AssuranceRules.ExemptionReasonProblems(reason));

            if (entry.Fingerprint is not null && !AssuranceVocabulary.IsWellFormedFingerprint(entry.Fingerprint))
                problems.Add($"fingerprint '{entry.Fingerprint}' is not six uppercase hex characters");

            return problems;
        }

        if (entry.Fingerprint is null)
            problems.Add("has no fingerprint; give the one 'assurance list' printed for the version assessed");
        else if (!AssuranceVocabulary.IsWellFormedFingerprint(entry.Fingerprint))
            problems.Add($"fingerprint '{entry.Fingerprint}' is not six uppercase hex characters");

        Closed("origin", entry.Origin, AssuranceVocabulary.OriginValues, problems);
        Closed("ip", entry.Ip, AssuranceVocabulary.IpRiskValues, problems);
        Closed("security", entry.Security, AssuranceVocabulary.SecurityRiskValues, problems);

        if (entry.Resources is not { } resources)
            problems.Add("has no resources");
        else if (resources is < 0 or > 10)
            problems.Add($"resources {resources.ToString(CultureInfo.InvariantCulture)} is not an integer 0 to 10");

        if (entry.Cs is { } cs && cs is < 0 or > 10)
            problems.Add($"CS {cs.ToString(CultureInfo.InvariantCulture)} is not an integer 0 to 10");

        if (entry.Cl is { } cl && cl is < 0 or > 10)
            problems.Add($"CL {cl.ToString(CultureInfo.InvariantCulture)} is not an integer 0 to 10");

        if (entry.Spec is { } spec)
        {
            string trimmed = spec.Trim();
            if (trimmed.Length == 0)
                problems.Add("Spec is empty; leave it out instead");
            else if (trimmed.Contains(';', StringComparison.Ordinal))
                problems.Add("Spec contains ';', which would end the field");

            if (!IsOneLine(trimmed))
                problems.Add("Spec must be one line");

            problems.AddRange(AssuranceRules.SpecProblems(trimmed));
        }

        if (entry.FalsifiedIf is { } criterion)
        {
            string trimmed = criterion.Trim();
            if (!IsOneLine(trimmed))
                problems.Add($"{AssuranceVocabulary.FalsifiedIfMarker} must be one line");

            problems.AddRange(AssuranceRules.CriterionProblems(trimmed));
        }

        if (entry.Security is { } security &&
            AssuranceRules.SecurityRequiringACriterion.Contains(security, StringComparer.Ordinal) &&
            string.IsNullOrWhiteSpace(entry.FalsifiedIf))
        {
            problems.Add(
                $"is assessed Security={security} and carries no '{AssuranceVocabulary.FalsifiedIfMarker}' " +
                "line, so nothing at the declaration says what would make it wrong");
        }

        // The rubric writes a criterion only for High and Critical.
        if (entry.Security is { } below &&
            AssuranceRules.SecurityWritingNoCriterion.Contains(below, StringComparer.Ordinal) &&
            entry.FalsifiedIf is not null)
        {
            problems.Add(
                $"is assessed Security={below} and carries a '{AssuranceVocabulary.FalsifiedIfMarker}' line, " +
                "which is written only for High and Critical; leave falsifiedIf out");
        }

        return problems;
    }

    /// <summary>
    /// The lines of the block for a valid entry, at <paramref name="indent"/>.
    /// The field order is: Origin, Spec, IP, Security, Resources, CS, CL, Fingerprint.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=2; Fingerprint=56DCA7
    // Hairs-Falsified-If: a rendered block's last line is anything other than the human marker followed by PENDING, whatever the entry holds
    // Hairs-Human:        PENDING
    public static IReadOnlyList<string> Render(AssuranceAssessment entry, string indent)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(indent);

        string human = AssuranceAnnotation.RenderLine(indent, AssuranceVocabulary.HumanMarker, AssuranceVocabulary.Pending);

        if (entry.IsExemption)
        {
            return
            [
                AssuranceAnnotation.RenderLine(
                    indent, AssuranceVocabulary.AiMarker, $"{AssuranceVocabulary.ExemptField}={entry.Exempt!.Trim()}"),
                human,
            ];
        }

        var fields = new StringBuilder();
        fields.Append("Origin=").Append(entry.Origin);
        if (entry.Spec is { } spec)
            fields.Append("; Spec=").Append(spec.Trim());

        fields.Append("; IP=").Append(entry.Ip)
            .Append("; Security=").Append(entry.Security)
            .Append("; Resources=").Append(entry.Resources!.Value.ToString(CultureInfo.InvariantCulture));

        if (entry.Cs is { } cs)
            fields.Append("; CS=").Append(cs.ToString(CultureInfo.InvariantCulture));
        if (entry.Cl is { } cl)
            fields.Append("; CL=").Append(cl.ToString(CultureInfo.InvariantCulture));

        fields.Append("; Fingerprint=").Append(AssuranceVocabulary.ToBeFilled);

        var lines = new List<string> { AssuranceAnnotation.RenderLine(indent, AssuranceVocabulary.AiMarker, fields.ToString()) };
        if (entry.FalsifiedIf is { } criterion)
            lines.Add(AssuranceAnnotation.RenderLine(indent, AssuranceVocabulary.FalsifiedIfMarker, criterion.Trim()));

        lines.Add(human);
        return lines;
    }

    /// <summary>
    /// Applies the entries for one file to <paramref name="text"/>.
    ///
    /// Each entry is applied or refused on its own. The only exception is the
    /// check after inserting: if the file does not scan the same, every entry
    /// for it is refused and the original text is returned.
    /// </summary>
    /// <param name="text">The file's text, decoded, without a byte-order mark.</param>
    /// <param name="path">The file's root-relative path, for the parser and for messages.</param>
    /// <param name="scanner">The scanner, used before and after inserting.</param>
    /// <param name="entries">The entries naming this file.</param>
    /// <param name="closedToEscapeHatch">True when <c>EXEMPT=</c> may not be written in this file's assembly.</param>
    /// <param name="assembly">The file's assembly, for messages.</param>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=E5D891
    // Hairs-Falsified-If: two entries naming the same unit of one file are applied, or one of them is, instead of both being refused
    // Hairs-Human:        PENDING
    public static AssuranceInsertFileResult Apply(
        string text,
        string path,
        IAssuranceFileScanner scanner,
        IReadOnlyList<AssuranceAssessment> entries,
        bool closedToEscapeHatch = false,
        string? assembly = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(scanner);
        ArgumentNullException.ThrowIfNull(entries);

        var lines = new AssuranceLines(text);
        AssuranceScannedFile scan = scanner.ScanFile(text, path);
        IReadOnlyList<AssuranceCandidate> candidates = AssuranceCandidates.Classify(lines, scan);

        var results = new AssuranceInsertEntryResult?[entries.Count];
        var targets = new Dictionary<int, List<int>>();

        for (int index = 0; index < entries.Count; index++)
        {
            AssuranceAssessment entry = entries[index];

            IReadOnlyList<string> problems = Validate(entry);
            if (problems.Count > 0)
            {
                results[index] = Refused(entry, string.Join("; ", problems));
                continue;
            }

            if (entry.IsExemption && closedToEscapeHatch)
            {
                results[index] = Refused(entry,
                    $"{assembly ?? "this assembly"} is closed to the per-unit exemption: a unit there is " +
                    "assessed or it is not shipped");
                continue;
            }

            (int? target, string? why) = Resolve(candidates, entry);
            if (target is not { } found)
            {
                results[index] = Refused(entry, why!);
                continue;
            }

            AssuranceCandidate candidate = candidates[found];
            if (!candidate.Insertable)
            {
                results[index] = Refused(entry, candidate.Reason switch
                {
                    AssuranceCandidates.Exempt => $"the unit is exempt ({candidate.Exemption}) and takes no annotation",
                    AssuranceCandidates.Annotated =>
                        $"the unit already carries an annotation at line {candidate.Annotation!.AiLine + 1}",
                    _ => $"{candidate.Reason}: {candidate.Detail}; a human has to repair this first",
                });
                continue;
            }

            if (!targets.TryGetValue(found, out List<int>? claimants))
                targets[found] = claimants = [];

            claimants.Add(index);
        }

        // Two entries for one unit: neither is chosen.
        var inserts = new List<(int Unit, int Entry)>();
        foreach ((int unit, List<int> claimants) in targets)
        {
            if (claimants.Count == 1)
            {
                inserts.Add((unit, claimants[0]));
                continue;
            }

            string numbers = string.Join(
                ", ", claimants.Select(claimant => "#" + entries[claimant].Index.ToString(CultureInfo.InvariantCulture)));
            foreach (int claimant in claimants)
            {
                results[claimant] = Refused(entries[claimant],
                    $"the input assesses this unit {claimants.Count.ToString(CultureInfo.InvariantCulture)} " +
                    $"times (entries {numbers}); state it once");
            }
        }

        if (inserts.Count == 0)
            return new AssuranceInsertFileResult(text, Complete(results, entries));

        // Bottom-up, so every declaration line not yet written to is still where
        // the scan found it.
        inserts.Sort((left, right) =>
            candidates[right.Unit].Unit.DeclarationLine.CompareTo(candidates[left.Unit].Unit.DeclarationLine));

        var blocks = new Dictionary<int, IReadOnlyList<string>>();
        foreach ((int unit, int entry) in inserts)
        {
            AssuranceFileUnit source = candidates[unit].Source;
            IReadOnlyList<string> block = Render(entries[entry], source.Indent);
            lines.Insert(source.Unit.DeclarationLine, block, SeparatorAt(lines, source.Unit.DeclarationLine));
            blocks[unit] = block;
        }

        string inserted = lines.Render();
        string? failure = Verify(text, inserted, path, scanner, scan, candidates, blocks);
        if (failure is not null)
        {
            foreach ((_, int entry) in inserts)
            {
                results[entry] = Refused(entries[entry],
                    $"the file did not scan the same after inserting ({failure}); nothing was written to it");
            }

            return new AssuranceInsertFileResult(text, Complete(results, entries));
        }

        foreach ((int unit, int entry) in inserts)
        {
            int line = NewLineOf(candidates[unit].Unit.DeclarationLine, candidates, blocks);
            results[entry] = new AssuranceInsertEntryResult(
                entries[entry],
                true,
                entries[entry].IsExemption ? "EXEMPT block inserted" : "assessment inserted",
                line + 1);
        }

        return new AssuranceInsertFileResult(inserted, Complete(results, entries));
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=34BB23
    // Hairs-Falsified-If: an entry whose fingerprint matches none of the units of that name is resolved to one of them instead of being refused as stale
    // Hairs-Human:        PENDING
    private static (int? Index, string? Why) Resolve(IReadOnlyList<AssuranceCandidate> candidates, AssuranceAssessment entry)
    {
        var named = new List<int>();
        for (int index = 0; index < candidates.Count; index++)
        {
            if (string.Equals(candidates[index].Unit.Name, entry.Unit, StringComparison.Ordinal))
                named.Add(index);
        }

        if (named.Count == 0)
            return (null, "no code unit in this file has that name; 'assurance list' prints the names");

        if (entry.Fingerprint is null)
        {
            return named.Count == 1
                ? (named[0], null)
                : (null, $"{named.Count.ToString(CultureInfo.InvariantCulture)} units in this file have that name; " +
                    "add the fingerprint to say which");
        }

        List<int> matching = [.. named.Where(index =>
            string.Equals(candidates[index].Unit.Fingerprint, entry.Fingerprint, StringComparison.Ordinal))];

        if (matching.Count == 1)
            return (matching[0], null);

        if (matching.Count == 0)
        {
            string current = string.Join(", ", named.Select(index => candidates[index].Unit.Fingerprint));
            return (null,
                $"the assessed fingerprint {entry.Fingerprint} is stale: the unit now computes {current}. " +
                "The code changed after it was assessed; list it again and reassess");
        }

        return (null,
            $"{matching.Count.ToString(CultureInfo.InvariantCulture)} units in this file have that name and " +
            "that fingerprint, so neither identifies one");
    }

    /// <summary>
    /// The ending for lines inserted above <paramref name="line"/>: the ending of
    /// the line above it, which is the one the neighbourhood uses. The first line
    /// of a file has none above it and takes its own.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=0; Fingerprint=FC2669
    // Hairs-Falsified-If: a block inserted above a declaration inside a CRLF region of a file whose endings are mixed is given LF endings
    // Hairs-Human:        PENDING
    private static string SeparatorAt(AssuranceLines lines, int line)
    {
        if (line > 0 && lines.SeparatorOf(line - 1) is { Length: > 0 } above)
            return above;

        if (lines.SeparatorOf(line) is { Length: > 0 } own)
            return own;

        return lines.NewLine;
    }

    /// <summary>Where a declaration line lands once the blocks above it are inserted.</summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=DBF600
    // Hairs-Falsified-If: a declaration with two three-line blocks inserted above it is placed at its old line plus anything other than six
    // Hairs-Human:        PENDING
    private static int NewLineOf(
        int declarationLine, IReadOnlyList<AssuranceCandidate> candidates, Dictionary<int, IReadOnlyList<string>> blocks)
    {
        int shift = 0;
        foreach ((int unit, IReadOnlyList<string> block) in blocks)
        {
            if (candidates[unit].Unit.DeclarationLine < declarationLine)
                shift += block.Count;
        }

        return declarationLine + shift;
    }

    /// <summary>
    /// Rescans the new text and checks that inserting changed nothing but the
    /// comments it was meant to add. Returns what went wrong, or null.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=BA8C95
    // Hairs-Falsified-If: an inserted text that differs from the original in a character outside the inserted lines is answered with null
    // Hairs-Human:        PENDING
    private static string? Verify(
        string original,
        string inserted,
        string path,
        IAssuranceFileScanner scanner,
        AssuranceScannedFile before,
        IReadOnlyList<AssuranceCandidate> candidatesBefore,
        Dictionary<int, IReadOnlyList<string>> blocks)
    {
        var lines = new AssuranceLines(inserted);
        AssuranceScannedFile after = scanner.ScanFile(inserted, path);

        if (after.LineCount != lines.Count)
            return "the parser and the line model count different lines";

        if (!string.Equals(after.FileFingerprint, before.FileFingerprint, StringComparison.Ordinal))
            return $"the file fingerprint moved from {before.FileFingerprint} to {after.FileFingerprint}";

        if (after.Units.Count != before.Units.Count)
            return "the number of code units changed";

        for (int index = 0; index < after.Units.Count; index++)
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

        IReadOnlyList<AssuranceCandidate> candidatesAfter = AssuranceCandidates.Classify(lines, after);
        var insertedLines = new HashSet<int>();

        for (int index = 0; index < candidatesAfter.Count; index++)
        {
            AssuranceCandidate was = candidatesBefore[index];
            AssuranceCandidate now = candidatesAfter[index];

            if (blocks.TryGetValue(index, out IReadOnlyList<string>? block))
            {
                int first = NewLineOf(was.Unit.DeclarationLine, candidatesBefore, blocks);
                if (now.Annotation is not { } annotation || annotation.AiLine != first)
                    return $"the block inserted for {was.Unit.Name} is not attached to it";

                if (AssuranceRules.VocabularyProblems(annotation).FirstOrDefault() is { } problem)
                    return $"the block inserted for {was.Unit.Name} reads back with a problem: {problem}";

                for (int offset = 0; offset < block.Count; offset++)
                {
                    if (!string.Equals(lines[first + offset], block[offset], StringComparison.Ordinal))
                        return $"the block inserted for {was.Unit.Name} is not where it was written";

                    insertedLines.Add(first + offset);
                }

                continue;
            }

            // A unit nothing was inserted for keeps exactly what it had.
            if (was.IsAnnotated != now.IsAnnotated ||
                was.IsExempt != now.IsExempt ||
                (was.Annotation is { } before1 && now.Annotation is { } after1 &&
                 (!string.Equals(before1.HumanBody, after1.HumanBody, StringComparison.Ordinal) ||
                  before1.Fields.Count != after1.Fields.Count)))
            {
                return $"the annotation on {was.Unit.Name} changed";
            }
        }

        if (after.AssuranceCommentLines.Count != before.AssuranceCommentLines.Count + insertedLines.Count)
            return "an assurance comment appeared or disappeared outside the inserted blocks";

        // The strongest check: take the inserted lines out again and the original
        // must come back byte for byte.
        var check = new AssuranceLines(inserted);
        foreach (int line in insertedLines.OrderByDescending(static line => line))
            check.RemoveRange(line, 1);

        return string.Equals(check.Render(), original, StringComparison.Ordinal)
            ? null
            : "removing the inserted lines does not give back the original text";
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=3; Fingerprint=008F44
    // Hairs-Falsified-If: an entry left without a result is reported as applied
    // Hairs-Human:        PENDING
    private static IReadOnlyList<AssuranceInsertEntryResult> Complete(
        AssuranceInsertEntryResult?[] results, IReadOnlyList<AssuranceAssessment> entries)
    {
        var complete = new AssuranceInsertEntryResult[results.Length];
        for (int index = 0; index < results.Length; index++)
            complete[index] = results[index] ?? Refused(entries[index], "not applied");

        return complete;
    }

    // Hairs-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=F03306
    // Hairs-Human:        PENDING
    private static AssuranceInsertEntryResult Refused(AssuranceAssessment entry, string message) =>
        new(entry, false, message);

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=1; Fingerprint=84F930
    // Hairs-Falsified-If: a vocabulary value that differs from an allowed one only in letter case, such as 'high', adds no problem
    // Hairs-Human:        PENDING
    private static void Closed(string name, string? value, string[] allowed, List<string> problems)
    {
        if (value is null)
        {
            problems.Add($"has no {name}");
            return;
        }

        if (!allowed.Contains(value, StringComparer.Ordinal))
            problems.Add($"{name} '{value}' is outside its vocabulary ({string.Join(", ", allowed)})");
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=79917E
    // Hairs-Falsified-If: a value holding a U+2028 line separator or a lone carriage return is reported as one line
    // Hairs-Human:        PENDING
    private static bool IsOneLine(string value)
    {
        foreach (char character in value)
        {
            if (character is '\r' or '\n' || character == (char)0x0085 || character == (char)0x2028 ||
                character == (char)0x2029)
            {
                return false;
            }
        }

        return true;
    }
}
