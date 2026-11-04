// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;

namespace Hairs;

/// <summary>
/// Recorded human review status for a file.
/// </summary>
public enum ReviewStatus
{
    Unreviewed = 0,
    InReview,
    Reviewed,
    Question,
    NeedsChange,
}

/// <summary>
/// Freshness of a file's review relative to its current content.
/// </summary>
public enum ReviewFreshness
{
    NotReviewed = 0,
    Current,
    Stale,
    Unknown,
}

/// <summary>
/// Current evaluation of a file's review record against disk content.
/// </summary>
public readonly record struct ReviewState(
    ReviewStatus Status,
    ReviewFreshness Freshness,
    int OpenNotes = 0)
{
    public static ReviewState None => new(ReviewStatus.Unreviewed, ReviewFreshness.NotReviewed);

    public bool IsVerified =>
        Status == ReviewStatus.Reviewed && Freshness == ReviewFreshness.Current;

    public bool IsStaleApproval =>
        Status == ReviewStatus.Reviewed && Freshness == ReviewFreshness.Stale;

    public bool IsAttested => Status != ReviewStatus.Unreviewed;

    public string ToDisplayString() => (Status, Freshness) switch
    {
        (ReviewStatus.Unreviewed, _) => "needs review",
        (_, ReviewFreshness.Unknown) => "review state unknown",
        (ReviewStatus.Reviewed, ReviewFreshness.Stale) => "reviewed, then modified",
        (ReviewStatus.Reviewed, _) => "reviewed",
        (ReviewStatus.InReview, ReviewFreshness.Stale) => "in review, then modified",
        (ReviewStatus.InReview, _) => "in review",
        (ReviewStatus.Question, ReviewFreshness.Stale) => "open question, then modified",
        (ReviewStatus.Question, _) => "open question",
        (ReviewStatus.NeedsChange, ReviewFreshness.Stale) => "needs change, then modified",
        (ReviewStatus.NeedsChange, _) => "needs change",
        _ => "review state unknown",
    };
}
