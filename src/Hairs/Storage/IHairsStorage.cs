// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hairs.Storage;

/// <summary>
/// Asynchronous, capability-based access to granted storage roots.
/// </summary>
public interface IHairsStorage
{
    StorageCapabilities Capabilities { get; }

    IReadOnlyList<string> GrantedRoots { get; }

    ValueTask<StorageResult<StorageTextContent>> ReadTextAsync(
        string relativePath, CancellationToken cancellationToken = default);

    ValueTask<StorageResult<string>> WriteTextAsync(
        string relativePath,
        string text,
        TextEncodingInfo encoding,
        string? expectedRevision,
        CancellationToken cancellationToken = default);

    ValueTask<StorageResult<IReadOnlyList<StorageEntry>>> ListAsync(
        string relativeDirectory, CancellationToken cancellationToken = default);

    ValueTask<StorageResult<StorageEntry>> StatAsync(
        string relativePath, CancellationToken cancellationToken = default);

    ValueTask<StorageResult<bool>> DeleteAsync(
        string relativePath, CancellationToken cancellationToken = default);

    ValueTask<StorageResult<StorageEntry>> RenameAsync(
        string fromRelativePath, string toRelativePath, CancellationToken cancellationToken = default);
}
