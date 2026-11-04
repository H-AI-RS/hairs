// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Hairs.SupplyChain;

/// <summary>
/// Summary and detailed inventory of a repository's supply chain integrity audit.
/// </summary>
public sealed record SupplyChainAuditReport
{
    public required IReadOnlyList<SupplyChainAsset> Assets { get; init; }

    public int TotalAssets => Assets.Count;

    public int VerifiedCount => Assets.Count(a => a.IsVerified);

    public int StaleCount => Assets.Count(a => a.IsStale);

    public int UnreviewedCount => Assets.Count(a => a.Status == ReviewStatus.Unreviewed);

    public int FlaggedCount => Assets.Count(a => a.Status is ReviewStatus.Question or ReviewStatus.NeedsChange);

    public double CoveragePercent => TotalAssets == 0 ? 100.0 : Math.Round(VerifiedCount * 100.0 / TotalAssets, 2);

    public bool IsCompliant => StaleCount == 0 && FlaggedCount == 0 && UnreviewedCount == 0;

    public IReadOnlyDictionary<SupplyChainAssetType, int> BreakdownByType =>
        Assets.GroupBy(a => a.AssetType).ToDictionary(g => g.Key, g => g.Count());

    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Supply Chain Integrity Audit");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**{CoveragePercent.ToString("0.0", CultureInfo.InvariantCulture)} %** of {TotalAssets} supply chain assets are verified against their current content.");
        sb.AppendLine();
        sb.AppendLine("| Status | Assets | Share |");
        sb.AppendLine("| --- | ---: | ---: |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Verified & Fresh | {VerifiedCount} | {FormatPercent(VerifiedCount)} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Stale (Modified since review) | {StaleCount} | {FormatPercent(StaleCount)} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Flagged / Needs Change | {FlaggedCount} | {FormatPercent(FlaggedCount)} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Unreviewed | {UnreviewedCount} | {FormatPercent(UnreviewedCount)} |");
        sb.AppendLine();
        sb.AppendLine("### Assets Inventory");
        sb.AppendLine();
        sb.AppendLine("| Type | Path | Reviewer | Status | Fresh? |");
        sb.AppendLine("| --- | --- | --- | --- | --- |");

        foreach (SupplyChainAsset asset in Assets.OrderBy(a => a.AssetType).ThenBy(a => a.Path, StringComparer.Ordinal))
        {
            string fresh = asset.IsFresh ? "Yes" : "**STALE**";
            string reviewer = string.IsNullOrWhiteSpace(asset.Reviewer) ? "-" : asset.Reviewer;
            sb.AppendLine(CultureInfo.InvariantCulture, $"| {asset.AssetType} | `{asset.Path}` | {reviewer} | {asset.Status} | {fresh} |");
        }

        return sb.ToString();
    }

    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("totalAssets", TotalAssets);
            writer.WriteNumber("verified", VerifiedCount);
            writer.WriteNumber("stale", StaleCount);
            writer.WriteNumber("flagged", FlaggedCount);
            writer.WriteNumber("unreviewed", UnreviewedCount);
            writer.WriteNumber("coveragePercent", CoveragePercent);
            writer.WriteBoolean("isCompliant", IsCompliant);

            writer.WriteStartArray("assets");
            foreach (SupplyChainAsset a in Assets)
            {
                writer.WriteStartObject();
                writer.WriteString("path", a.Path);
                writer.WriteString("type", a.AssetType.ToString());
                writer.WriteString("description", a.Description);
                writer.WriteString("status", a.Status.ToString());
                writer.WriteBoolean("isFresh", a.IsFresh);
                writer.WriteString("reviewer", a.Reviewer);
                if (a.ReviewedAt.HasValue)
                    writer.WriteString("reviewedAt", a.ReviewedAt.Value.ToString("O", CultureInfo.InvariantCulture));
                if (a.ContentHash is not null)
                    writer.WriteString("contentHash", a.ContentHash);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private string FormatPercent(int count) =>
        TotalAssets == 0 ? "0.0 %" : (count * 100.0 / TotalAssets).ToString("0.0", CultureInfo.InvariantCulture) + " %";
}
