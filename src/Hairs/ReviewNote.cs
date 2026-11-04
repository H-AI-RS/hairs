// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;

namespace Hairs;

public enum ReviewNoteKind
{
    Question = 0,
    Concern,
    Todo,
    Observation,
}

public enum ReviewAnchorStatus
{
    Anchored = 0,
    Moved,
    Ambiguous,
    Orphaned,
    FileLevel,
}

public sealed record ReviewAnchor(
    int StartLine,
    int EndLine,
    string AnchorText = "",
    string? Symbol = null)
{
    public static ReviewAnchor File => new(-1, -1);

    public bool IsFileLevel => StartLine < 0;

    public int LineCount => IsFileLevel ? 0 : Math.Max(1, (EndLine - StartLine) + 1);
}

public sealed record ReviewResolution(
    DateTimeOffset ResolvedAt,
    string ResolvedBy,
    string Text);

public sealed record ReviewNote
{
    public required string Id { get; init; }

    public required ReviewNoteKind Kind { get; init; }

    public required string Text { get; init; }

    public required string Author { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public ReviewAnchor Anchor { get; init; } = ReviewAnchor.File;

    public ReviewResolution? Resolution { get; init; }

    public bool IsOpen => Resolution is null && Kind != ReviewNoteKind.Observation;
}

public readonly record struct AnchoredNote(
    ReviewNote Note,
    ReviewAnchorStatus Status,
    int StartLine,
    int EndLine)
{
    public bool NeedsAttention =>
        Status is ReviewAnchorStatus.Orphaned or ReviewAnchorStatus.Ambiguous;
}
