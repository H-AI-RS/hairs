// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

namespace Hairs.Storage;

public enum StorageFailureKind
{
    None = 0,
    NotFound,
    PermissionDenied,
    Conflict,
    OutsideGrant,
    Unsupported,
    Unknown,
}

public sealed record StorageFailure(StorageFailureKind Kind, string Message);

public readonly record struct StorageResult<T>(bool Succeeded, T? Value, StorageFailure? Failure)
{
    public static StorageResult<T> Ok(T value) => new(true, value, null);

    public static StorageResult<T> Fail(StorageFailureKind kind, string message) =>
        new(false, default, new StorageFailure(kind, message));
}

public readonly record struct TextEncodingInfo(string WebName, bool HasByteOrderMark)
{
    public static TextEncodingInfo Utf8NoBom => new("utf-8", false);
    public static TextEncodingInfo Utf8Bom => new("utf-8", true);
}

public sealed record StorageTextContent(
    string Text,
    TextEncodingInfo Encoding,
    string? ExternalRevision,
    string? DurableFileId);

public sealed record StorageEntry(
    string RelativePath,
    bool IsDirectory,
    long SizeBytes,
    string? ExternalRevision,
    string? DurableFileId);
