// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

namespace Hairs;

/// <summary>
/// Evaluates a review record against current disk content to compute review state.
/// </summary>
public static class ReviewStateEvaluator
{
    public static ReviewState Evaluate(FileReview? review, string? content)
    {
        if (review is null || review.Status == ReviewStatus.Unreviewed)
        {
            int pending = review?.OpenNoteCount ?? 0;
            return new ReviewState(ReviewStatus.Unreviewed, ReviewFreshness.NotReviewed, pending);
        }

        return new ReviewState(review.Status, FreshnessOf(review, content), review.OpenNoteCount);
    }

    private static ReviewFreshness FreshnessOf(FileReview review, string? content)
    {
        if (content is null)
            return ReviewFreshness.Unknown;

        if (!ReviewContentHash.IsKnownAlgorithm(review.ReviewedContentHash))
            return ReviewFreshness.Unknown;

        return ReviewContentHash.Matches(review.ReviewedContentHash, content)
            ? ReviewFreshness.Current
            : ReviewFreshness.Stale;
    }
}
