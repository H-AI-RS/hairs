// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0
//
// HAIRS Code Assurance
// --------------------
// Relevant units:   20
// Annotated:        20/20
// Exempt:           0
// Human-reviewed:   0/20
// IP risk:          Low
// Security risk:    High
// Criteria:         20/15
// Resource impact:  5/10 max
// Unverified:       20
//
// GENERATED - DO NOT EDIT MANUALLY

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Hairs.Assurance;

/// <summary>
/// Finding, removing and writing the generated block at the top of a covered
/// file, and recognizing a forged one anywhere else in it.
/// </summary>
// Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=5; Fingerprint=6ED41D
// Hairs-Falsified-If: Strip deletes a copyright line that the configuration does not state for the file without returning a refusal
// Hairs-Human:        PENDING
public static class AssuranceHeader
{
    /// <summary>
    /// The nine row labels in the narrow vocabulary's form: lower case, a hyphen
    /// read as a space, without the colon.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=None; Security=High; Resources=1; Fingerprint=4415CB
    // Hairs-Falsified-If: a normalized label keeps its trailing colon or its upper case, so a comment opening 'relevant units : 3' below the header is not reported
    // Hairs-Human:        PENDING
    private static readonly string[] NormalizedRowLabels =
        [.. AssuranceBanner.RowLabels.Select(static label => label.TrimEnd(':').Replace('-', ' ').ToLowerInvariant())];

    /// <summary>
    /// The summary vocabulary, matched case-insensitively anywhere in a comment line.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=1; Fingerprint=DAF119
    // Hairs-Falsified-If: a term holds an upper-case letter, so IsSummaryComment, which compares terms ordinally against lower-cased text, never matches it
    // Hairs-Human:        PENDING
    public static readonly IReadOnlyList<string> StrictVocabulary =
    [
        "hairs code assurance",
        "generated - do not edit manually",
        "relevant units:", "annotated:", "exempt:", "human-reviewed",
        "ip risk", "security risk", "criteria:", "resource impact", "unverified:",
        "human reviewed", "reviewer", "reviewstate", "humanreviewed", "approved",
        "eligible for release",
    ];

    /// <summary>
    /// What a comment must open with, after its slashes and spaces, to be a
    /// summary line under the narrow vocabulary: the banner, the marker, or one
    /// of the header's row labels.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=None; Security=High; Resources=1; Fingerprint=AA4C2E
    // Hairs-Falsified-If: an opening keeps the leading slashes and space of the banner or the marker, so a line whose comment body opens with the banner text is not accepted by IsSummaryLine
    // Hairs-Human:        PENDING
    private static readonly string[] NarrowOpenings =
    [
        AssuranceBanner.Banner[3..],
        AssuranceBanner.GeneratedMarker[3..],
        .. AssuranceBanner.RowLabels,
    ];

    /// <summary>
    /// True for a comment line that reads like part of a generated summary,
    /// under <paramref name="vocabulary"/>. A line that is not a <c>//</c>
    /// comment never is.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=35AD38
    // Hairs-Falsified-If: under the narrow vocabulary a '//' line whose body opens with 'unverified:' in lower case is not accepted as a summary line
    // Hairs-Human:        PENDING
    public static bool IsSummaryLine(string line, AssuranceForgeryVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(line);

        string content = line.Trim();
        if (!content.StartsWith("//", StringComparison.Ordinal))
            return false;

        if (vocabulary == AssuranceForgeryVocabulary.Strict)
            return StrictVocabulary.Any(term => content.Contains(term, StringComparison.OrdinalIgnoreCase));

        string body = content.TrimStart('/').TrimStart();
        return NarrowOpenings.Any(opening => body.StartsWith(opening, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// How many assurance banners a text carries, at any indentation and in any
    /// case. A generated file carries exactly one.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=AF95F5
    // Hairs-Falsified-If: an indented or lower-case copy of the banner line is not counted
    // Hairs-Human:        PENDING
    public static int BannerCount(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var lines = new AssuranceLines(text);
        int count = 0;
        for (int index = 0; index < lines.Count; index++)
        {
            string line = lines[index].Trim();
            if (string.Equals(line, AssuranceBanner.Banner, StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// A violation when <paramref name="text"/> carries more than one banner.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=9E099B
    // Hairs-Falsified-If: a text carrying two banner lines returns null
    // Hairs-Human:        PENDING
    public static AssuranceViolation? DuplicateBanners(string path, string text)
    {
        ArgumentNullException.ThrowIfNull(path);

        int count = BannerCount(text);
        return count <= 1
            ? null
            : new AssuranceViolation(
                "J5",
                path,
                null,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{path} carries {count} '{AssuranceBanner.Banner[3..]}' banners; exactly one block is generated and every other one is a forgery"));
    }

    /// <summary>
    /// True for one line of comment text that reads like part of a generated summary.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=E77B11
    // Hairs-Falsified-If: a comment 'Relevant-Units : 3', with a hyphen and a space before the colon, is not reported under the narrow vocabulary
    // Hairs-Human:        PENDING
    public static bool IsSummaryComment(string comment, AssuranceForgeryVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(comment);

        string content = CommentContent(comment);
        if (content.Length == 0)
            return false;

        if (vocabulary == AssuranceForgeryVocabulary.Strict &&
            StrictVocabulary.Any(term => content.Contains(term, StringComparison.Ordinal)))
        {
            return true;
        }

        string words = WordsOf(content);
        if (words.Contains("hairs code assurance", StringComparison.Ordinal) ||
            words.Contains("generated do not edit manually", StringComparison.Ordinal))
        {
            return true;
        }

        string labelled = content.Replace('-', ' ');
        foreach (string label in NormalizedRowLabels)
        {
            if (!labelled.StartsWith(label, StringComparison.Ordinal))
                continue;

            if (labelled[label.Length..].TrimStart().StartsWith(':'))
                return true;
        }

        return false;
    }

    /// <summary>
    /// A violation naming the first summary comment line below the generated
    /// marker, and how many there are; otherwise null.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=4; Fingerprint=F41069
    // Hairs-Falsified-If: a row-label comment inside a documentation comment below the generated marker returns null
    // Hairs-Human:        PENDING
    public static AssuranceViolation? ForgedSummary(
        string path, string text, IReadOnlyList<AssuranceCommentLine> comments, AssuranceForgeryVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(comments);

        var lines = new AssuranceLines(text);
        int header = -1;
        for (int index = 0; index < lines.Count; index++)
        {
            if (string.Equals(lines[index].Trim(), AssuranceBanner.GeneratedMarker, StringComparison.Ordinal))
            {
                header = index;
                break;
            }
        }

        AssuranceCommentLine? first = null;
        int count = 0;
        foreach (AssuranceCommentLine comment in comments)
        {
            if (comment.Line <= header || !IsSummaryComment(comment.Text, vocabulary))
                continue;

            first ??= comment;
            count++;
        }

        return first is not { } found
            ? null
            : new AssuranceViolation(
                "J5",
                path,
                found.Line + 1,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{path}({found.Line + 1}) carries the assurance summary line '{found.Text.Trim()}' below the generated header, and {count} such line(s) sit there; the generated block is the only one a file may carry"));
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=54B271
    // Hairs-Falsified-If: a block-comment line ' * Exempt: 3 */' comes back still holding its asterisks or closing delimiter, so the label check misses it
    // Hairs-Human:        PENDING
    private static string CommentContent(string comment)
    {
        string text = comment.Trim();
        if (text.EndsWith("*/", StringComparison.Ordinal))
            text = text[..^2];

        text = text.TrimStart('/', '*', '!').Trim();

        var content = new StringBuilder(text.Length);
        bool space = false;
        foreach (char character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                space = content.Length > 0;
                continue;
            }

            if (space)
                content.Append(' ');

            space = false;
            content.Append(char.ToLowerInvariant(character));
        }

        return content.ToString();
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=9B9031
    // Hairs-Falsified-If: a run of two or more characters that are neither letters nor digits between two words comes back as more than one space
    // Hairs-Human:        PENDING
    private static string WordsOf(string content)
    {
        var words = new StringBuilder(content.Length);
        bool gap = false;
        foreach (char character in content)
        {
            if (char.IsLetterOrDigit(character))
            {
                if (gap && words.Length > 0)
                    words.Append(' ');

                gap = false;
                words.Append(character);
            }
            else
            {
                gap = true;
            }
        }

        return words.ToString();
    }

    /// <summary>
    /// The generated header of a text, from its first line through the marker,
    /// or nothing when it has none.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=A8E833
    // Hairs-Falsified-If: a text that opens with a complete generated header is returned without its marker line
    // Hairs-Human:        PENDING
    public static IReadOnlyList<string> GeneratedHeaderLines(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var lines = new AssuranceLines(text);
        if (!lines[0].StartsWith(AssuranceBanner.SpdxCopyrightPrefix, StringComparison.Ordinal))
            return [];

        for (int index = 0; index < lines.Count && lines[index].StartsWith("//", StringComparison.Ordinal); index++)
        {
            if (string.Equals(lines[index], AssuranceBanner.GeneratedMarker, StringComparison.Ordinal))
                return [.. Enumerable.Range(0, index + 1).Select(line => lines[line])];
        }

        return [];
    }

    /// <summary>
    /// Removes the generated header and any forged summary runs directly under
    /// it, in place. Returns null, or why nothing was removed.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=5; Fingerprint=64DCF0
    // Hairs-Falsified-If: a second header under the first, whose copyright line names a holder the configuration does not state, is removed without a refusal
    // Hairs-Human:        PENDING
    public static string? Strip(
        AssuranceLines lines,
        IReadOnlyList<string> spdxLines,
        AssuranceForgeryVocabulary vocabulary,
        string path,
        out IReadOnlyList<string> separators)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(spdxLines);
        ArgumentNullException.ThrowIfNull(path);

        separators = [];
        if (!lines[0].StartsWith(AssuranceBanner.SpdxPrefix, StringComparison.Ordinal))
            return null;

        int run = LeadingCommentRun(lines);
        int marker = MarkerIn(lines, run);
        int through;

        if (marker >= 0)
        {
            for (int index = 0; index <= marker; index++)
            {
                string line = lines[index];
                if (IsHeaderShaped(line) || IsSummaryLine(line, vocabulary))
                    continue;

                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"{path}({index + 1}) sits inside the generated header and is not a line the generator writes: " +
                    $"'{line}'. The generator will not delete it; move it below the header, or delete it.");
            }

            for (int index = 0; index <= marker; index++)
            {
                string line = lines[index];
                if (line.StartsWith(AssuranceBanner.SpdxCopyrightPrefix, StringComparison.Ordinal) &&
                    !spdxLines.Contains(line, StringComparer.Ordinal))
                {
                    return string.Create(
                        CultureInfo.InvariantCulture,
                        $"{path}({index + 1}) carries '{line}' in its generated header, and " +
                        $"{AssuranceComponentConfig.FileName} does not state that line for this file. The generator " +
                        $"will not drop a copyright line: state it in spdx or spdxOverrides, or delete it by hand.");
                }
            }

            through = marker + 1;
        }
        else
        {
            for (int index = 0; index < run; index++)
            {
                if (spdxLines.Contains(lines[index], StringComparer.Ordinal))
                    continue;

                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"{path} opens with a comment run the generator did not write (line {index + 1}: " +
                    $"'{lines[index]}'). It will neither delete that run nor stack a second licence header " +
                    $"above it: state exactly the run's lines in spdx or in an spdxOverrides entry for this file, " +
                    $"so that the generated header replaces the run, or exclude the file.");
            }

            through = run;
        }

        if (through < lines.Count && lines[through].Trim().Length == 0)
            through++;

        var removed = new List<string>(through);
        for (int index = 0; index < through; index++)
            removed.Add(lines.SeparatorOf(index));

        RemoveLeading(lines, through);
        separators = removed;

        while (LeadingRunIsGeneratedCopy(lines))
            RemoveLeadingCommentRun(lines);

        return null;
    }

    /// <summary>
    /// Inserts <paramref name="header"/> at the top of the file.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=74ACD7
    // Hairs-Falsified-If: a CRLF file whose old header was stripped gets LF endings on the new header's lines
    // Hairs-Human:        PENDING
    public static void Insert(AssuranceLines lines, IReadOnlyList<string> header, IReadOnlyList<string> separators)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(separators);

        string fallback = lines.SeparatorOf(0) is { Length: > 0 } own ? own : lines.NewLine;
        for (int index = 0; index < header.Count; index++)
        {
            string separator = index < separators.Count && separators[index].Length > 0 ? separators[index] : fallback;
            lines.Insert(index, [header[index]], separator);
        }
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=1; Fingerprint=FBBD80
    // Hairs-Falsified-If: a documentation line, or the machine line of an assurance block, counts as header-shaped
    // Hairs-Human:        PENDING
    private static bool IsHeaderShaped(string line) =>
        line.StartsWith(AssuranceBanner.SpdxCopyrightPrefix, StringComparison.Ordinal) ||
        line.StartsWith(AssuranceBanner.SpdxLicensePrefix, StringComparison.Ordinal) ||
        line is "//" or AssuranceBanner.Banner or AssuranceBanner.BannerRule or AssuranceBanner.GeneratedMarker ||
        AssuranceBanner.RowLabels.Any(label => line.StartsWith("// " + label, StringComparison.Ordinal));

    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=A3504D
    // Hairs-Falsified-If: a leading run broken by a blank line is counted past that blank line
    // Hairs-Human:        PENDING
    private static int LeadingCommentRun(AssuranceLines lines)
    {
        int run = 0;
        while (run < lines.Count && lines[run].StartsWith("//", StringComparison.Ordinal))
            run++;

        return run;
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=385A7F
    // Hairs-Falsified-If: a marker on a line after the leading comment run is returned
    // Hairs-Human:        PENDING
    private static int MarkerIn(AssuranceLines lines, int run)
    {
        for (int index = 0; index < run; index++)
        {
            if (string.Equals(lines[index], AssuranceBanner.GeneratedMarker, StringComparison.Ordinal))
                return index;
        }

        return -1;
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=BCE73F
    // Hairs-Falsified-If: a leading run that holds a documentation line or an assurance block line before its own marker is reported as a copy
    // Hairs-Human:        PENDING
    private static bool LeadingRunIsGeneratedCopy(AssuranceLines lines)
    {
        int marker = MarkerIn(lines, LeadingCommentRun(lines));
        if (marker < 0)
            return false;

        for (int index = 0; index <= marker; index++)
        {
            if (!IsHeaderShaped(lines[index]))
                return false;
        }

        return true;
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=F419FE
    // Hairs-Falsified-If: more than one blank line after the copy's marker is removed
    // Hairs-Human:        PENDING
    private static void RemoveLeadingCommentRun(AssuranceLines lines)
    {
        int run = LeadingCommentRun(lines);
        int marker = MarkerIn(lines, run);
        if (marker < 0)
            return;

        int through = marker + 1;

        if (through < lines.Count && lines[through].Trim().Length == 0)
            through++;

        RemoveLeading(lines, through);
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=FDFB4C
    // Hairs-Falsified-If: removing as many lines as the text holds leaves no line at all instead of one empty line
    // Hairs-Human:        PENDING
    private static void RemoveLeading(AssuranceLines lines, int count)
    {
        if (count < lines.Count)
        {
            lines.RemoveRange(0, count);
            return;
        }

        lines.RemoveRange(0, lines.Count - 1);
        lines.Replace(0, string.Empty);
    }
}
