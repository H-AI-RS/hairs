// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Globalization;
using Hairs;

namespace Hairs.Tests;

/// <summary>
/// What the review record promises, asserted rather than described.
/// </summary>
public sealed class ReviewRecordTests
{
    private const string Source = "class A\n{\n    void M() { }\n}\n";

    [Fact(Timeout = 600000)]
    public void A_File_With_No_Record_Is_Unreviewed()
    {
        ReviewState state = ReviewStateEvaluator.Evaluate(null, Source);

        Assert.Equal(ReviewStatus.Unreviewed, state.Status);
        Assert.Equal(ReviewFreshness.NotReviewed, state.Freshness);
        Assert.False(state.IsVerified);
        Assert.Equal("needs review", state.ToDisplayString());
    }

    [Fact(Timeout = 600000)]
    public void A_Review_Of_The_Current_Content_Is_Verified()
    {
        FileReview review = FileReview.Empty("a.cs")
            .WithDecision(ReviewStatus.Reviewed, "Enrico", Source, Stamp);

        ReviewState state = ReviewStateEvaluator.Evaluate(review, Source);

        Assert.True(state.IsVerified);
        Assert.Equal(ReviewFreshness.Current, state.Freshness);
        Assert.Equal("reviewed", state.ToDisplayString());
    }

    [Fact(Timeout = 600000)]
    public void An_Edit_After_A_Review_Makes_It_Stale()
    {
        FileReview review = FileReview.Empty("a.cs")
            .WithDecision(ReviewStatus.Reviewed, "Enrico", Source, Stamp);

        ReviewState state = ReviewStateEvaluator.Evaluate(review, Source.Replace("void M()", "int M()"));

        Assert.Equal(ReviewFreshness.Stale, state.Freshness);
        Assert.False(state.IsVerified);
        Assert.True(state.IsStaleApproval);
        Assert.Equal("reviewed, then modified", state.ToDisplayString());

        Assert.Equal("Enrico", review.Reviewer);
        Assert.NotNull(review.ReviewedAt);
    }

    [Fact(Timeout = 600000)]
    public void Reverting_An_Edit_Makes_The_Review_Current_Again()
    {
        FileReview review = FileReview.Empty("a.cs")
            .WithDecision(ReviewStatus.Reviewed, "Enrico", Source, Stamp);

        Assert.Equal(
            ReviewFreshness.Stale,
            ReviewStateEvaluator.Evaluate(review, Source + "\n// changed\n").Freshness);

        Assert.Equal(ReviewFreshness.Current, ReviewStateEvaluator.Evaluate(review, Source).Freshness);
    }

    [Theory(Timeout = 600000)]
    [InlineData("a\r\nb\r\n")]
    [InlineData("a\rb\r")]
    [InlineData("﻿a\nb\n")]
    public void Line_Endings_And_A_Byte_Order_Mark_Do_Not_Invalidate_A_Review(string variant)
    {
        FileReview review = FileReview.Empty("a.cs")
            .WithDecision(ReviewStatus.Reviewed, "Enrico", "a\nb\n", Stamp);

        Assert.Equal(ReviewFreshness.Current, ReviewStateEvaluator.Evaluate(review, variant).Freshness);
    }

    [Fact(Timeout = 600000)]
    public void Reindenting_Does_Invalidate_A_Review()
    {
        FileReview review = FileReview.Empty("a.cs")
            .WithDecision(ReviewStatus.Reviewed, "Enrico", "class A\n{\n    void M();\n}\n", Stamp);

        Assert.Equal(
            ReviewFreshness.Stale,
            ReviewStateEvaluator.Evaluate(review, "class A\n{\n\tvoid M();\n}\n").Freshness);
    }

    [Fact(Timeout = 600000)]
    public void An_Unrecognized_Hash_Algorithm_Is_Unknown_Not_Stale()
    {
        var review = new FileReview
        {
            Path = "a.cs",
            Status = ReviewStatus.Reviewed,
            Reviewer = "Enrico",
            ReviewedAt = Stamp,
            ReviewedContentHash = "blake3:deadbeef",
        };

        ReviewState state = ReviewStateEvaluator.Evaluate(review, Source);

        Assert.Equal(ReviewFreshness.Unknown, state.Freshness);
        Assert.False(state.IsVerified);
    }

    [Fact(Timeout = 600000)]
    public void A_Missing_File_Is_Unknown()
    {
        FileReview review = FileReview.Empty("a.cs")
            .WithDecision(ReviewStatus.Reviewed, "Enrico", Source, Stamp);

        Assert.Equal(ReviewFreshness.Unknown, ReviewStateEvaluator.Evaluate(review, null).Freshness);
    }

    [Fact(Timeout = 600000)]
    public void Open_Notes_Are_Reported_On_An_Unreviewed_File()
    {
        FileReview review = FileReview.Empty("a.cs").AddNote(new ReviewNote
        {
            Id = "n1",
            Kind = ReviewNoteKind.Question,
            Text = "Is ToPrimitive required here?",
            Author = "Enrico",
            CreatedAt = Stamp,
        });

        ReviewState state = ReviewStateEvaluator.Evaluate(review, Source);

        Assert.Equal(ReviewStatus.Unreviewed, state.Status);
        Assert.Equal(1, state.OpenNotes);
    }

    [Fact(Timeout = 600000)]
    public void An_Observation_Is_Never_Open()
    {
        var note = new ReviewNote
        {
            Id = "n1",
            Kind = ReviewNoteKind.Observation,
            Text = "Mirrors the spec's wording.",
            Author = "Enrico",
            CreatedAt = Stamp,
        };

        Assert.False(note.IsOpen);
        Assert.True((note with { Kind = ReviewNoteKind.Question }).IsOpen);
    }

    [Fact(Timeout = 600000)]
    public void Note_Ids_Do_Not_Repeat()
    {
        FileReview review = FileReview.Empty("a.cs");

        for (int i = 1; i <= 3; i++)
        {
            string id = review.NextNoteId();
            Assert.Equal(string.Create(CultureInfo.InvariantCulture, $"n{i}"), id);
            review = review.AddNote(new ReviewNote
            {
                Id = id,
                Kind = ReviewNoteKind.Question,
                Text = "?",
                Author = "Enrico",
                CreatedAt = Stamp,
            });
        }

        review = review.RemoveNote("n2");
        Assert.Equal("n4", review.NextNoteId());
    }

    [Fact(Timeout = 600000)]
    public void Clearing_A_Review_Drops_The_Reviewer_And_The_Hash()
    {
        FileReview cleared = FileReview.Empty("a.cs")
            .WithDecision(ReviewStatus.Reviewed, "Enrico", Source, Stamp)
            .WithDecision(ReviewStatus.Unreviewed, "Enrico", Source, Stamp);

        Assert.Equal(ReviewStatus.Unreviewed, cleared.Status);
        Assert.Equal(string.Empty, cleared.Reviewer);
        Assert.Null(cleared.ReviewedContentHash);
        Assert.Null(cleared.ReviewedAt);
        Assert.True(cleared.IsEmpty);
    }

    private static DateTimeOffset Stamp => new(2026, 9, 4, 9, 14, 0, TimeSpan.Zero);
}
