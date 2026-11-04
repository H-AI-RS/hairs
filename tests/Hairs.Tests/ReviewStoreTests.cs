// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Threading.Tasks;
using Hairs;
using Hairs.Storage;

namespace Hairs.Tests;

/// <summary>
/// The record on disk: where it goes, what it looks like, and round trips.
/// </summary>
public sealed class ReviewStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "hairs-review-tests", Guid.NewGuid().ToString("n"));

    public ReviewStoreTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact(Timeout = 600000)]
    public void A_Record_Mirrors_The_Source_Tree()
    {
        Assert.Equal(
            ".hairs-review/src/Hairs.Core/Runtime/State.cs.review.json",
            ReviewStore.RecordPathFor("src/Hairs.Core/Runtime/State.cs"));

        Assert.Equal(
            "src/Hairs.Core/Runtime/State.cs",
            ReviewStore.SourcePathFor(".hairs-review/src/Hairs.Core/Runtime/State.cs.review.json"));
    }

    [Fact(Timeout = 600000)]
    public void Two_Files_Have_Two_Records()
    {
        Assert.NotEqual(
            ReviewStore.RecordPathFor("src/A.cs"),
            ReviewStore.RecordPathFor("src/B.cs"));
    }

    [Fact(Timeout = 600000)]
    public void A_Record_Cannot_Acquire_A_Record_Of_Its_Own() =>
        Assert.Null(ReviewStore.RecordPathFor(".hairs-review/src/A.cs.review.json"));

    [Fact(Timeout = 600000)]
    public void A_Path_Escaping_The_Workspace_Is_Refused() =>
        Assert.Null(ReviewStore.RecordPathFor("../../../etc/passwd"));

    [Fact(Timeout = 600000)]
    public async Task A_File_With_No_Record_Reads_As_Empty_Not_As_An_Error()
    {
        StorageResult<FileReview> read = await Store().ReadAsync("src/A.cs");

        Assert.True(read.Succeeded);
        Assert.Equal(ReviewStatus.Unreviewed, read.Value!.Status);
        Assert.True(read.Value.IsEmpty);
    }

    [Fact(Timeout = 600000)]
    public async Task A_Recorded_Review_Round_Trips()
    {
        ReviewStore store = Store();
        FileReview written = FileReview.Empty("src/A.cs")
            .WithDecision(ReviewStatus.Reviewed, "Enrico", "class A { }\n", Stamp, "a31f0e27")
            .AddNote(new ReviewNote
            {
                Id = "n1",
                Kind = ReviewNoteKind.Question,
                Text = "Check whether verification is needed here.",
                Author = "Enrico",
                CreatedAt = Stamp,
                Anchor = new ReviewAnchor(142, 142, "        var state = GetState();", "State.Get"),
            });

        Assert.True((await store.WriteAsync(written)).Succeeded);

        FileReview read = (await store.ReadAsync("src/A.cs")).Value!;

        Assert.Equal(ReviewStatus.Reviewed, read.Status);
        Assert.Equal("Enrico", read.Reviewer);
        Assert.Equal("a31f0e27", read.ReviewedRevision);
        Assert.Equal(written.ReviewedContentHash, read.ReviewedContentHash);
        Assert.Equal(Stamp, read.ReviewedAt);

        ReviewNote note = Assert.Single(read.Notes);
        Assert.Equal("n1", note.Id);
        Assert.Equal(ReviewNoteKind.Question, note.Kind);
        Assert.Equal(142, note.Anchor.StartLine);
        Assert.Equal("State.Get", note.Anchor.Symbol);
        Assert.Equal("        var state = GetState();", note.Anchor.AnchorText);
    }

    [Fact(Timeout = 600000)]
    public void Writing_An_Unchanged_Record_Is_Byte_Identical()
    {
        FileReview review = FileReview.Empty("src/A.cs")
            .WithDecision(ReviewStatus.Reviewed, "Enrico", "class A { }\n", Stamp, "a31f0e27");

        string first = ReviewJson.Write(review);
        string second = ReviewJson.Write(ReviewJson.Read(first, "src/A.cs")!);

        Assert.Equal(first, second);
    }

    [Fact(Timeout = 600000)]
    public void A_Record_Ends_With_A_Newline_And_Records_Its_Version()
    {
        string json = ReviewJson.Write(FileReview.Empty("src/A.cs")
            .WithDecision(ReviewStatus.Reviewed, "Enrico", "class A { }\n", Stamp));

        Assert.EndsWith("\n", json, StringComparison.Ordinal);
        Assert.Contains("\"version\": 1", json, StringComparison.Ordinal);
        Assert.Contains("\"status\": \"reviewed\"", json, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void Non_Ascii_Note_Text_Is_Not_Escaped()
    {
        string json = ReviewJson.Write(FileReview.Empty("src/A.cs").AddNote(new ReviewNote
        {
            Id = "n1",
            Kind = ReviewNoteKind.Question,
            Text = "Warum wird die Konvertierung vor Get() ausgeführt?",
            Author = "Enrico",
            CreatedAt = Stamp,
        }));

        Assert.Contains("ausgeführt", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u", json, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public async Task A_Corrupt_Record_Is_Reported_And_Left_Alone()
    {
        string path = Path.Combine(_root, ".hairs-review", "src", "A.cs.review.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "{ this is not json");

        StorageResult<FileReview> read = await Store().ReadAsync("src/A.cs");

        Assert.False(read.Succeeded);
        Assert.Equal("{ this is not json", await File.ReadAllTextAsync(path));
    }

    [Fact(Timeout = 600000)]
    public async Task Clearing_A_Review_Deletes_Its_Record()
    {
        ReviewStore store = Store();
        string path = Path.Combine(_root, ".hairs-review", "src", "A.cs.review.json");

        await store.WriteAsync(FileReview.Empty("src/A.cs")
            .WithDecision(ReviewStatus.Reviewed, "Enrico", "class A { }\n", Stamp));
        Assert.True(File.Exists(path));

        await store.WriteAsync(FileReview.Empty("src/A.cs"));
        Assert.False(File.Exists(path));
    }

    [Fact(Timeout = 600000)]
    public async Task Reading_Everything_Finds_Records_At_Any_Depth()
    {
        ReviewStore store = Store();
        foreach (string file in (string[])["A.cs", "src/B.cs", "src/deep/nested/C.cs"])
        {
            await store.WriteAsync(FileReview.Empty(file)
                .WithDecision(ReviewStatus.Reviewed, "Enrico", "class X { }\n", Stamp));
        }

        ReviewStore.ReviewRecordSet all = await store.ReadAllAsync();

        Assert.Equal(3, all.Reviews.Count);
        Assert.Empty(all.Unreadable);
        Assert.True(all.Reviews.ContainsKey("src/deep/nested/C.cs"));
        Assert.Equal("src/deep/nested/C.cs", all.Reviews["src/deep/nested/C.cs"].Path);
    }

    [Fact(Timeout = 600000)]
    public async Task Reading_Everything_From_A_Workspace_With_No_Records_Is_Empty()
    {
        ReviewStore.ReviewRecordSet all = await Store().ReadAllAsync();

        Assert.Empty(all.Reviews);
        Assert.Empty(all.Unreadable);
    }

    [Fact(Timeout = 600000)]
    public async Task An_Unreadable_Record_Is_Reported_By_Read_All()
    {
        ReviewStore store = Store();
        await store.WriteAsync(FileReview.Empty("src/Good.cs")
            .WithDecision(ReviewStatus.Reviewed, "Enrico", "class G { }\n", Stamp));

        string broken = Path.Combine(_root, ".hairs-review", "src", "Bad.cs.review.json");
        Directory.CreateDirectory(Path.GetDirectoryName(broken)!);
        await File.WriteAllTextAsync(broken, "<<<<<<< HEAD\n{ }\n=======");

        ReviewStore.ReviewRecordSet all = await store.ReadAllAsync();

        Assert.Single(all.Reviews);
        StorageFailure failure = Assert.Single(all.Unreadable);
        Assert.Contains("src/Bad.cs", failure.Message, StringComparison.Ordinal);
    }

    private ReviewStore Store() => new(new FileSystemHairsStorage(_root));

    private static DateTimeOffset Stamp => new(2026, 9, 4, 9, 14, 0, TimeSpan.Zero);
}
