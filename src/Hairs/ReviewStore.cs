// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hairs.Storage;

namespace Hairs;

/// <summary>
/// Persistence store for file review records, kept beside the source code.
/// Default review directory is .hairs-review/.
/// </summary>
public sealed class ReviewStore
{
    public const string ReviewDirectory = ".hairs-review";
    public const string RecordSuffix = ".review.json";

    private readonly IHairsStorage _storage;

    public ReviewStore(IHairsStorage storage) =>
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));

    public static string? RecordPathFor(string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(sourcePath);

        string? normalized = HairsPath.Normalize(sourcePath);
        if (string.IsNullOrEmpty(normalized) || IsRecordPath(normalized))
            return null;

        return $"{ReviewDirectory}/{normalized}{RecordSuffix}";
    }

    public static string? SourcePathFor(string recordPath)
    {
        ArgumentNullException.ThrowIfNull(recordPath);

        string? normalized = HairsPath.Normalize(recordPath);
        if (normalized is null || !normalized.EndsWith(RecordSuffix, StringComparison.Ordinal))
            return null;

        if (normalized.StartsWith(ReviewDirectory + "/", StringComparison.Ordinal))
            return normalized[(ReviewDirectory.Length + 1)..^RecordSuffix.Length];

        return null;
    }

    public static bool IsRecordPath(string path) =>
        path.Equals(ReviewDirectory, StringComparison.Ordinal) ||
        path.StartsWith(ReviewDirectory + "/", StringComparison.Ordinal);

    public async ValueTask<StorageResult<FileReview>> ReadAsync(
        string sourcePath, CancellationToken cancellationToken = default)
    {
        if (RecordPathFor(sourcePath) is not { } recordPath)
        {
            return StorageResult<FileReview>.Fail(
                StorageFailureKind.OutsideGrant, $"'{sourcePath}' is not a reviewable workspace path.");
        }

        StorageResult<StorageTextContent> read = await _storage
            .ReadTextAsync(recordPath, cancellationToken).ConfigureAwait(false);

        if (!read.Succeeded)
        {
            return read.Failure?.Kind == StorageFailureKind.NotFound
                ? StorageResult<FileReview>.Ok(FileReview.Empty(NormalizeOrSelf(sourcePath)))
                : StorageResult<FileReview>.Fail(
                    read.Failure?.Kind ?? StorageFailureKind.Unknown,
                    read.Failure?.Message ?? $"'{recordPath}' could not be read.");
        }

        FileReview? parsed = ReviewJson.Read(read.Value!.Text, NormalizeOrSelf(sourcePath));
        return parsed is null
            ? StorageResult<FileReview>.Fail(
                StorageFailureKind.Unknown,
                $"'{recordPath}' is not a valid review record. It was left untouched.")
            : StorageResult<FileReview>.Ok(parsed);
    }

    public async ValueTask<StorageResult<bool>> WriteAsync(
        FileReview review, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(review);

        if (RecordPathFor(review.Path) is not { } recordPath)
        {
            return StorageResult<bool>.Fail(
                StorageFailureKind.OutsideGrant, $"'{review.Path}' is not a reviewable workspace path.");
        }

        if (review.IsEmpty)
        {
            StorageResult<bool> deleted = await _storage
                .DeleteAsync(recordPath, cancellationToken).ConfigureAwait(false);

            return !deleted.Succeeded && deleted.Failure?.Kind == StorageFailureKind.NotFound
                ? StorageResult<bool>.Ok(true)
                : deleted;
        }

        StorageResult<string> written = await _storage.WriteTextAsync(
            recordPath,
            ReviewJson.Write(review),
            TextEncodingInfo.Utf8NoBom,
            expectedRevision: null,
            cancellationToken).ConfigureAwait(false);

        return written.Succeeded
            ? StorageResult<bool>.Ok(true)
            : StorageResult<bool>.Fail(
                written.Failure?.Kind ?? StorageFailureKind.Unknown,
                written.Failure?.Message ?? $"'{recordPath}' could not be written.");
    }

    public readonly record struct ReviewRecordSet(
        IReadOnlyDictionary<string, FileReview> Reviews,
        IReadOnlyList<StorageFailure> Unreadable);

    public async ValueTask<ReviewRecordSet> ReadAllAsync(
        CancellationToken cancellationToken = default)
    {
        var reviews = new Dictionary<string, FileReview>(StringComparer.Ordinal);
        var unreadable = new List<StorageFailure>();
        await CollectAsync(ReviewDirectory, reviews, unreadable, cancellationToken).ConfigureAwait(false);
        return new ReviewRecordSet(reviews, unreadable);
    }

    private async ValueTask CollectAsync(
        string directory,
        Dictionary<string, FileReview> into,
        List<StorageFailure> unreadable,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        StorageResult<IReadOnlyList<StorageEntry>> listed = await _storage
            .ListAsync(directory, cancellationToken).ConfigureAwait(false);

        if (!listed.Succeeded)
            return;

        foreach (StorageEntry entry in listed.Value!)
        {
            if (entry.IsDirectory)
            {
                await CollectAsync(entry.RelativePath, into, unreadable, cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            if (SourcePathFor(entry.RelativePath) is not { } sourcePath)
                continue;

            if (into.ContainsKey(sourcePath))
                continue;

            StorageResult<StorageTextContent> read = await _storage
                .ReadTextAsync(entry.RelativePath, cancellationToken).ConfigureAwait(false);
            if (!read.Succeeded)
            {
                unreadable.Add(read.Failure ?? new StorageFailure(
                    StorageFailureKind.Unknown, $"'{entry.RelativePath}' could not be read."));
                continue;
            }

            if (ReviewJson.Read(read.Value!.Text, sourcePath) is { } review)
                into[sourcePath] = review;
            else
                unreadable.Add(new StorageFailure(
                    StorageFailureKind.Unknown,
                    $"'{entry.RelativePath}' is not a valid review record, so '{sourcePath}' " +
                    "is reported as unreviewed. It was left untouched."));
        }
    }

    private static string NormalizeOrSelf(string path) => HairsPath.Normalize(path) ?? path;
}
