// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace Hairs;

/// <summary>
/// Places recorded notes onto the file as it currently is.
/// Notes track code text rather than volatile line numbers.
/// </summary>
public static class NoteAnchoring
{
    public static IReadOnlyList<AnchoredNote> Place(FileReview review, string content)
    {
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(content);

        if (review.Notes.Count == 0)
            return [];

        string[] lines = SplitLines(ReviewContentHash.Normalize(content));
        var placed = new List<AnchoredNote>(review.Notes.Count);
        foreach (ReviewNote note in review.Notes)
            placed.Add(Place(note, lines));

        return placed;
    }

    public static AnchoredNote Place(ReviewNote note, string[] lines)
    {
        ArgumentNullException.ThrowIfNull(note);
        ArgumentNullException.ThrowIfNull(lines);

        ReviewAnchor anchor = note.Anchor;
        if (anchor.IsFileLevel)
            return new AnchoredNote(note, ReviewAnchorStatus.FileLevel, -1, -1);

        string[] wanted = SplitLines(ReviewContentHash.Normalize(anchor.AnchorText));
        if (wanted.Length == 0)
            return new AnchoredNote(note, ReviewAnchorStatus.Ambiguous, anchor.StartLine, anchor.EndLine);

        if (MatchesAt(lines, wanted, anchor.StartLine))
            return new AnchoredNote(note, ReviewAnchorStatus.Anchored, anchor.StartLine, anchor.StartLine + wanted.Length - 1);

        int found = -1;
        for (int start = 0; start + wanted.Length <= lines.Length; start++)
        {
            if (!MatchesAt(lines, wanted, start))
                continue;

            if (found >= 0)
                return new AnchoredNote(note, ReviewAnchorStatus.Ambiguous, anchor.StartLine, anchor.EndLine);

            found = start;
        }

        return found >= 0
            ? new AnchoredNote(note, ReviewAnchorStatus.Moved, found, found + wanted.Length - 1)
            : new AnchoredNote(note, ReviewAnchorStatus.Orphaned, anchor.StartLine, anchor.EndLine);
    }

    public static ReviewAnchor CreateAnchor(string content, int startLine, int endLine, string? symbol = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        string[] lines = SplitLines(ReviewContentHash.Normalize(content));
        if (lines.Length == 0)
            return ReviewAnchor.File;

        int start = Math.Clamp(startLine, 0, lines.Length - 1);
        int end = Math.Clamp(endLine, start, lines.Length - 1);

        return new ReviewAnchor(start, end, string.Join('\n', lines[start..(end + 1)]), symbol);
    }

    private static bool MatchesAt(string[] lines, string[] wanted, int start)
    {
        if (start < 0 || start + wanted.Length > lines.Length)
            return false;

        for (int i = 0; i < wanted.Length; i++)
        {
            if (!string.Equals(lines[start + i], wanted[i], StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static string[] SplitLines(string normalized)
    {
        if (normalized.Length == 0)
            return [];

        string[] lines = normalized.Split('\n');
        return lines.Length > 0 && lines[^1].Length == 0 ? lines[..^1] : lines;
    }
}
