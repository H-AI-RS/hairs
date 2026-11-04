// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System.Threading;
using System.Threading.Tasks;

namespace Hairs;

public interface IRevisionProvider
{
    ValueTask<string?> GetCurrentRevisionAsync(CancellationToken cancellationToken = default);
}

public sealed class NoRevisionProvider : IRevisionProvider
{
    public static NoRevisionProvider Instance { get; } = new();

    public ValueTask<string?> GetCurrentRevisionAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<string?>(null);
}
