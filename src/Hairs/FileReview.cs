// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hairs;

/// <summary>
/// Immutable review record for a single file.
/// </summary>
public sealed record FileReview
{
    public const int CurrentVersion = 1;

    public required string Path { get; init; }

    public ReviewStatus Status { get; init; } = ReviewStatus.Unreviewed;

    public string Reviewer { get; init; } = string.Empty;

    public DateTimeOffset? ReviewedAt { get; init; }

    public string? ReviewedContentHash { get; internal init; }

    public string? ReviewedRevision { get; init; }

    public IReadOnlyList<ReviewNote> Notes { get; init; } = [];

    public int Version { get; init; } = CurrentVersion;

    public int OpenNoteCount => Notes.Count(note => note.IsOpen);

    public static FileReview Empty(string path) => new() { Path = path };

    public FileReview WithDecision(
        ReviewStatus status,
        string reviewer,
        string content,
        DateTimeOffset at,
        string? revision = null)
    {
        ArgumentNullException.ThrowIfNull(reviewer);
        ArgumentNullException.ThrowIfNull(content);

        if (status != ReviewStatus.Unreviewed && string.IsNullOrWhiteSpace(reviewer))
        {
            throw new ArgumentException(
                "A review must record who made it.", nameof(reviewer));
        }

        if (status == ReviewStatus.Unreviewed)
            return this with { Status = status, Reviewer = string.Empty, ReviewedAt = null, ReviewedContentHash = null, ReviewedRevision = null };

        return this with
        {
            Status = status,
            Reviewer = reviewer,
            ReviewedAt = at,
            ReviewedContentHash = ReviewContentHash.Compute(content),
            ReviewedRevision = revision,
        };
    }

    public FileReview AddNote(ReviewNote note)
    {
        ArgumentNullException.ThrowIfNull(note);
        return this with { Notes = [.. Notes, note] };
    }

    public string NextNoteId()
    {
        int highest = 0;
        foreach (ReviewNote note in Notes)
        {
            if (note.Id.Length > 1 && note.Id[0] == 'n' &&
                int.TryParse(note.Id.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int value))
            {
                highest = Math.Max(highest, value);
            }
        }

        return string.Create(CultureInfo.InvariantCulture, $"n{highest + 1}");
    }

    public FileReview ReplaceNote(string id, Func<ReviewNote, ReviewNote> update)
    {
        ArgumentNullException.ThrowIfNull(update);

        int index = -1;
        for (int i = 0; i < Notes.Count; i++)
        {
            if (string.Equals(Notes[i].Id, id, StringComparison.Ordinal))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
            return this;

        var notes = new List<ReviewNote>(Notes);
        notes[index] = update(notes[index]);
        return this with { Notes = notes };
    }

    public FileReview RemoveNote(string id) =>
        this with { Notes = [.. Notes.Where(note => !string.Equals(note.Id, id, StringComparison.Ordinal))] };

    public bool IsEmpty => Status == ReviewStatus.Unreviewed && Notes.Count == 0;
}
