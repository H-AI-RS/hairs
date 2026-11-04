// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;

namespace Hairs.Assurance;

public sealed class AssuranceRefusalException : Exception
{
    public AssuranceRefusalException(string message)
        : base(message)
    {
    }
}

public static class AssuranceHumanLine
{
    private const string PreviousMarker = "Previous=";
    private static readonly string FingerprintMarker = AssuranceVocabulary.FingerprintField + "=";

    public static string Refreshed(AssuranceAnnotation annotation, string currentFingerprint, string where)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        ArgumentNullException.ThrowIfNull(currentFingerprint);
        ArgumentNullException.ThrowIfNull(where);

        if (annotation.HumanIsPending || annotation.Reviewer is null)
            return annotation.HumanIsStale ? annotation.HumanBody : AssuranceVocabulary.Pending;

        if (!IsDefined(annotation.HumanBody))
            return annotation.HumanBody;

        string reviewer = annotation.Reviewer;
        string? approved = annotation.HumanFingerprint;

        string assessment = string.Concat(
            annotation.HumanBody
                .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Skip(1)
                .Where(static part => !part.StartsWith(FingerprintMarker, StringComparison.Ordinal))
                .Select(static part => "; " + part));

        if (approved is null || string.Equals(approved, AssuranceVocabulary.ToBeFilled, StringComparison.Ordinal))
        {
            string? recorded = annotation.RecordedFingerprint;
            if (string.Equals(recorded, currentFingerprint, StringComparison.Ordinal))
                return $"{reviewer}{assessment}; {FingerprintMarker}{currentFingerprint}";

            if (AssuranceVocabulary.IsWellFormedFingerprint(recorded))
                return $"{AssuranceVocabulary.Stale}; {PreviousMarker}{reviewer}@{recorded}";

            throw new AssuranceRefusalException(
                $"The assurance generator will not bind the approval on {where} to a version: its human line " +
                $"reads '{annotation.HumanBody}' and its machine line records " +
                $"{(recorded is null ? "no Fingerprint" : $"Fingerprint={recorded}")}, so nothing says which " +
                $"version {reviewer} approved. Run the generator before recording a decision, or state the " +
                $"version approved as '{reviewer}; {FingerprintMarker}<six hex>'.");
        }

        if (string.Equals(approved, currentFingerprint, StringComparison.Ordinal))
            return $"{reviewer}{assessment}; {FingerprintMarker}{approved}";

        return $"{AssuranceVocabulary.Stale}; {PreviousMarker}{reviewer}@{approved}";
    }

    public static bool IsDefined(string body)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (string.Equals(body, AssuranceVocabulary.Pending, StringComparison.Ordinal))
            return true;

        string[] parts = body.Split(';', StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || parts[0].Length == 0)
            return false;

        if (string.Equals(parts[0], AssuranceVocabulary.Stale, StringComparison.Ordinal))
        {
            return parts.Length == 2 &&
                parts[1].StartsWith(PreviousMarker, StringComparison.Ordinal) &&
                parts[1].IndexOf('@', StringComparison.Ordinal) > PreviousMarker.Length;
        }

        if (!AssuranceVocabulary.IsAlias(parts[0]))
            return false;

        for (int index = 1; index < parts.Length; index++)
        {
            string part = parts[index];
            if (!AssuranceVocabulary.HumanFieldMarkers.Any(marker => part.StartsWith(marker, StringComparison.Ordinal)))
                return false;
        }

        return true;
    }

    public static IReadOnlySet<string> ReviewerNames(string body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var names = new HashSet<string>(StringComparer.Ordinal);
        if (string.Equals(body, AssuranceVocabulary.Pending, StringComparison.Ordinal))
            return names;

        string[] parts = body.Split(';', StringSplitOptions.TrimEntries);
        for (int index = 0; index < parts.Length; index++)
        {
            string part = parts[index];
            if (part.Length == 0 || part.StartsWith(FingerprintMarker, StringComparison.Ordinal))
                continue;

            if (index == 0 && string.Equals(part, AssuranceVocabulary.Stale, StringComparison.Ordinal))
                continue;

            string value = part.StartsWith(PreviousMarker, StringComparison.Ordinal)
                ? part[PreviousMarker.Length..]
                : part;

            int at = value.LastIndexOf('@');
            names.Add(at < 0 ? value : value[..at]);
        }

        return names;
    }

    public static void RefuseInventedApproval(string where, string before, string after)
    {
        ArgumentNullException.ThrowIfNull(where);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        if (!IsDefined(before))
        {
            throw new AssuranceRefusalException(
                $"The assurance generator will not rewrite the human line on {where}, which " +
                $"reads '{before}'. A human line is one of '{AssuranceVocabulary.Pending}', a " +
                "reviewer, a reviewer with a Fingerprint, or " +
                $"'{AssuranceVocabulary.Stale}; {PreviousMarker}<reviewer>@<fingerprint>'. " +
                "Only a human may create an approval." +
                $" A reviewer is an alias: it opens with a letter, holds letters, digits, '.', '_', '-', ''' and " +
                $"single spaces, is at most {AssuranceVocabulary.MaxAliasLength} characters long, and is not a " +
                "placeholder such as TODO, NONE or NOT REVIEWED.");
        }

        IReadOnlySet<string> permitted = ReviewerNames(before);
        foreach (string name in ReviewerNames(after))
        {
            if (!permitted.Contains(name))
            {
                throw new AssuranceRefusalException(
                    $"The assurance generator tried to write reviewer '{name}' onto {where}, " +
                    $"whose human line reads '{before}'. Only a human may create an approval.");
            }
        }
    }

    public static string Display(AssuranceAnnotation? annotation) => annotation switch
    {
        null => "no annotation",
        { HumanBody.Length: 0 } => "an empty line",
        _ => annotation.HumanBody,
    };

    public static IEnumerable<string> BodiesIn(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var lines = new AssuranceLines(text);
        for (int index = 0; index < lines.Count; index++)
        {
            string line = lines[index].Trim();
            if (line.StartsWith(AssuranceVocabulary.HumanMarker, StringComparison.Ordinal))
                yield return line[AssuranceVocabulary.HumanMarker.Length..].Trim();
        }
    }
}
