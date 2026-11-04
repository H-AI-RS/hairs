// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0
//
// HAIRS Code Assurance
// --------------------
// Relevant units:   9
// Annotated:        9/9
// Exempt:           0
// Human-reviewed:   0/9
// IP risk:          Low
// Security risk:    High
// Criteria:         9/9
// Resource impact:  5/10 max
// Unverified:       9
//
// GENERATED - DO NOT EDIT MANUALLY

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hairs.Assurance;

/// <summary>
/// The rule that no generated text may say a unit is reviewed, approved or
/// releasable while the annotations say no such thing (J9), ported with its
/// algorithm and its quirks.
/// </summary>
// Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=5; Fingerprint=E4AB3C
// Hairs-Falsified-If: a generated line stating a claim term with a count other than the annotations' count, and no negation before it in its clause, yields no J9 violation
// Hairs-Human:        PENDING
public static class AssuranceReviewClaims
{
    private static readonly string[] Negations =
        ["no", "not", "nothing", "never", "none", "neither", "nobody", "absence", "unverified"];

    private static readonly char[] ClauseSeparators = [':', ';', '.'];

    /// <summary>
    /// Every line of <paramref name="lines"/> stating a review the annotations
    /// in <paramref name="scope"/> do not hold.
    /// </summary>
    /// <param name="where">The artefact, for messages.</param>
    /// <param name="lines">Its generated text: the header of a source file, or the whole of anything else.</param>
    /// <param name="scope">The units the text speaks for: the file's own for a source header, all of them otherwise.</param>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=5; Fingerprint=59C6E3
    // Hairs-Falsified-If: a line stating a claim term with a number after it that differs from the annotations' count, and no negation before it in its clause, yields no violation
    // Hairs-Human:        PENDING
    public static IReadOnlyList<AssuranceViolation> Violations(
        string where, IReadOnlyList<string> lines, IReadOnlyList<AssuranceCorpusUnit> scope)
    {
        ArgumentNullException.ThrowIfNull(where);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(scope);

        var violations = new List<AssuranceViolation>();
        for (int line = 0; line < lines.Count; line++)
        {
            string content = lines[line];
            string lowered = content.ToLowerInvariant();

            foreach (string term in AssuranceRules.ReviewClaimTerms)
            {
                int at = WholeWord(lowered, term);
                if (at < 0 || IsSupported(lowered, at, term, scope))
                    continue;

                string count = Supported(term, scope)?.ToString(CultureInfo.InvariantCulture) ?? "none is defined for it";
                violations.Add(new AssuranceViolation(
                    "J9",
                    where,
                    line + 1,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{where}({line + 1}) says '{content.Trim()}', and the annotations hold no such state: the term " +
                        $"'{term}' is stated with neither the count the annotations give ({count}) nor a negation before it")));
                break;
            }
        }

        return violations;
    }

    /// <summary>
    /// Where <paramref name="term"/> first stands as a whole word that is not
    /// the tail of a dotted name, or -1.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=85E526
    // Hairs-Falsified-If: a claim term standing alone between spaces is not found, so its line is never judged
    // Hairs-Human:        PENDING
    private static int WholeWord(string lowered, string term)
    {
        for (int index = lowered.IndexOf(term, StringComparison.Ordinal);
             index >= 0;
             index = index + 1 <= lowered.Length ? lowered.IndexOf(term, index + 1, StringComparison.Ordinal) : -1)
        {
            bool before = index == 0 || !char.IsAsciiLetter(lowered[index - 1]);
            bool after = index + term.Length >= lowered.Length || !char.IsAsciiLetter(lowered[index + term.Length]);

            if (before && after && (index == 0 || lowered[index - 1] != '.'))
                return index;
        }

        return -1;
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=4; Fingerprint=C3BA8D
    // Hairs-Falsified-If: a claim term with no negation before it in its clause and a number after it that differs from the annotations' count is treated as supported
    // Hairs-Human:        PENDING
    private static bool IsSupported(string lowered, int at, string term, IReadOnlyList<AssuranceCorpusUnit> scope)
    {
        if (Negations.Any(negation => IsWordBefore(lowered, negation, at)))
            return true;

        if (Supported(term, scope) is not { } expected)
            return false;

        return FirstNumberAfter(lowered, at + term.Length) == expected;
    }

    /// <summary>A negation as a whole word in the clause that holds <paramref name="at"/>, before it.</summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=BB5B2C
    // Hairs-Falsified-If: a negation standing after the claim term, or only inside a longer word such as cannot, is taken as denying it
    // Hairs-Human:        PENDING
    private static bool IsWordBefore(string lowered, string word, int at)
    {
        int clause = 0;
        for (int index = 0; index < at && index < lowered.Length; index++)
        {
            if (ClauseSeparators.Contains(lowered[index]))
                clause = index + 1;
        }

        for (int index = lowered.IndexOf(word, clause, StringComparison.Ordinal);
             index >= 0 && index < at;
             index = lowered.IndexOf(word, index + 1, StringComparison.Ordinal))
        {
            bool before = index == 0 || !char.IsAsciiLetter(lowered[index - 1]);
            bool after = index + word.Length >= lowered.Length || !char.IsAsciiLetter(lowered[index + word.Length]);
            if (before && after)
                return true;
        }

        return false;
    }

    /// <summary>The count the annotations give for a term, or null where the term names no countable state.</summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=6CF914
    // Hairs-Falsified-If: the count returned for the terms naming the current signed state includes a Stale, Unknown or exempt unit
    // Hairs-Human:        PENDING
    private static int? Supported(string term, IReadOnlyList<AssuranceCorpusUnit> scope) => term switch
    {
        "verified" or "human reviewed" or "human-reviewed" or "humanreviewed" =>
            scope.Count(static unit => unit.IsRelevant && unit.State == AssuranceUnitState.Verified),
        "approved" =>
            scope.Count(static unit => unit.State == AssuranceUnitState.HumanApprovedPendingFingerprint),
        _ => null,
    };

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=2D1281
    // Hairs-Falsified-If: a number standing before the claim term is returned as the count stated after it
    // Hairs-Human:        PENDING
    private static int? FirstNumberAfter(string line, int index)
    {
        while (index < line.Length && !char.IsAsciiDigit(line[index]))
            index++;

        if (index >= line.Length)
            return null;

        int end = index;
        while (end < line.Length && char.IsAsciiDigit(line[end]))
            end++;

        return int.TryParse(line.AsSpan(index, end - index), NumberStyles.None, CultureInfo.InvariantCulture, out int value)
            ? value
            : null;
    }
}
