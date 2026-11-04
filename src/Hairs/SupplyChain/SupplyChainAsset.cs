// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;

namespace Hairs.SupplyChain;

/// <summary>
/// Represents a supply-chain critical asset and its current review integrity state.
/// </summary>
public sealed record SupplyChainAsset(
    string Path,
    SupplyChainAssetType AssetType,
    string Description,
    ReviewStatus Status,
    bool IsFresh,
    string Reviewer = "",
    DateTimeOffset? ReviewedAt = null,
    string? ContentHash = null)
{
    public bool IsVerified => Status is ReviewStatus.Reviewed && IsFresh;

    public bool IsStale => Status is ReviewStatus.Reviewed && !IsFresh;

    public bool NeedsReview => Status is ReviewStatus.Unreviewed || IsStale || Status is ReviewStatus.NeedsChange or ReviewStatus.Question;
}
