// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using Hairs;

namespace Hairs.Tests;

/// <summary>
/// Notes are attached to code, not to line numbers.
/// </summary>
public sealed class NoteAnchoringTests
{
    private static readonly string Source = string.Join(
        '\n',
        "class A",
        "{",
        "    void First() { }",
        "",
        "    void Second() { }",
        "}");

    [Fact(Timeout = 600000)]
    public void A_Note_On_Unchanged_Code_Stays_Where_It_Was()
    {
        AnchoredNote placed = Place(Source, Source, line: 2);

        Assert.Equal(ReviewAnchorStatus.Anchored, placed.Status);
        Assert.Equal(2, placed.StartLine);
    }

    [Fact(Timeout = 600000)]
    public void A_Note_Follows_Its_Code_When_Lines_Are_Inserted_Above()
    {
        string edited = "// a new header comment\n// and another\n" + Source;
        AnchoredNote placed = Place(Source, edited, line: 2);

        Assert.Equal(ReviewAnchorStatus.Moved, placed.Status);
        Assert.Equal(4, placed.StartLine);
    }

    [Fact(Timeout = 600000)]
    public void A_Note_Follows_Its_Code_When_Lines_Are_Deleted_Above()
    {
        string edited = Source.Replace("class A\n", string.Empty);
        AnchoredNote placed = Place(Source, edited, line: 2);

        Assert.Equal(ReviewAnchorStatus.Moved, placed.Status);
        Assert.Equal(1, placed.StartLine);
    }

    [Fact(Timeout = 600000)]
    public void A_Note_Whose_Code_Is_Deleted_Is_Orphaned_Not_Dropped()
    {
        string edited = Source.Replace("    void First() { }\n", string.Empty);
        AnchoredNote placed = Place(Source, edited, line: 2);

        Assert.Equal(ReviewAnchorStatus.Orphaned, placed.Status);
        Assert.True(placed.NeedsAttention);
        Assert.Equal("Why?", placed.Note.Text);
    }

    [Fact(Timeout = 600000)]
    public void A_Note_Whose_Code_Now_Appears_Twice_Is_Ambiguous()
    {
        string edited =
            """
            // a new header
            class A
            {
                void First() { }

                void Second() { }

                void First() { }
            }
            """;

        AnchoredNote placed = Place(Source, edited, line: 2);

        Assert.Equal(ReviewAnchorStatus.Ambiguous, placed.Status);
        Assert.True(placed.NeedsAttention);
    }

    [Fact(Timeout = 600000)]
    public void A_File_Level_Note_Has_No_Line()
    {
        var note = new ReviewNote
        {
            Id = "n1",
            Kind = ReviewNoteKind.Observation,
            Text = "This whole file mirrors the specification.",
            Author = "Enrico",
            CreatedAt = default,
            Anchor = ReviewAnchor.File,
        };

        AnchoredNote placed = NoteAnchoring.Place(note, Source.Split('\n'));

        Assert.Equal(ReviewAnchorStatus.FileLevel, placed.Status);
        Assert.Equal(-1, placed.StartLine);
    }

    [Fact(Timeout = 600000)]
    public void A_Multi_Line_Anchor_Keeps_Its_Range()
    {
        ReviewAnchor anchor = NoteAnchoring.CreateAnchor(Source, 2, 4);
        Assert.Equal(3, anchor.LineCount);

        var note = NewNote(anchor);
        AnchoredNote placed = NoteAnchoring.Place(note, ("// header\n" + Source).Split('\n'));

        Assert.Equal(ReviewAnchorStatus.Moved, placed.Status);
        Assert.Equal(3, placed.StartLine);
        Assert.Equal(5, placed.EndLine);
    }

    [Fact(Timeout = 600000)]
    public void An_Out_Of_Range_Anchor_Is_Clamped()
    {
        ReviewAnchor anchor = NoteAnchoring.CreateAnchor(Source, 500, 900);

        Assert.Equal(5, anchor.StartLine);
        Assert.Equal(5, anchor.EndLine);
        Assert.Equal("}", anchor.AnchorText);
    }

    [Fact(Timeout = 600000)]
    public void Anchoring_Agrees_With_Hashing_About_Line_Endings()
    {
        ReviewAnchor anchor = NoteAnchoring.CreateAnchor(Source, 2, 2);
        AnchoredNote placed = NoteAnchoring.Place(
            NewNote(anchor), ReviewContentHash.Normalize(Source.Replace("\n", "\r\n")).Split('\n'));

        Assert.Equal(ReviewAnchorStatus.Anchored, placed.Status);
        Assert.Equal(2, placed.StartLine);
    }

    [Fact(Timeout = 600000)]
    public void A_Symbol_Is_Recorded_But_Does_Not_Decide_Placement()
    {
        ReviewAnchor anchor = NoteAnchoring.CreateAnchor(Source, 2, 2, "A.First");
        Assert.Equal("A.First", anchor.Symbol);

        AnchoredNote placed = NoteAnchoring.Place(NewNote(anchor), ("// header\n" + Source).Split('\n'));
        Assert.Equal(ReviewAnchorStatus.Moved, placed.Status);
        Assert.Equal(3, placed.StartLine);
    }

    [Fact(Timeout = 600000)]
    public void A_Note_On_A_Blank_Line_Keeps_Its_Line_And_Reports_It_Cannot_Be_Placed()
    {
        ReviewAnchor anchor = NoteAnchoring.CreateAnchor(Source, 3, 3);

        Assert.False(anchor.IsFileLevel);
        Assert.Equal(3, anchor.StartLine);
        Assert.Equal(string.Empty, anchor.AnchorText);

        AnchoredNote placed = NoteAnchoring.Place(NewNote(anchor), Source.Split('\n'));
        Assert.Equal(ReviewAnchorStatus.Ambiguous, placed.Status);
        Assert.Equal(3, placed.StartLine);
        Assert.True(placed.NeedsAttention);
    }

    [Fact(Timeout = 600000)]
    public void A_Moved_Note_Does_Not_Need_Attention()
    {
        AnchoredNote placed = Place(Source, "// header\n" + Source, line: 2);

        Assert.Equal(ReviewAnchorStatus.Moved, placed.Status);
        Assert.False(placed.NeedsAttention);
    }

    private static AnchoredNote Place(string original, string edited, int line)
    {
        var review = FileReview.Empty("a.cs")
            .AddNote(NewNote(NoteAnchoring.CreateAnchor(original, line, line)));

        return NoteAnchoring.Place(review, edited)[0];
    }

    private static ReviewNote NewNote(ReviewAnchor anchor) => new()
    {
        Id = "n1",
        Kind = ReviewNoteKind.Question,
        Text = "Why?",
        Author = "Enrico",
        CreatedAt = default,
        Anchor = anchor,
    };
}
