// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hairs;

public sealed record ReviewedFile(
    string Path,
    string Component,
    ReviewState State,
    string Reviewer = "",
    DateTimeOffset? ReviewedAt = null);

public sealed record ReviewCoverageTotals(
    string Name,
    int Total,
    int Verified,
    int StaleApprovals,
    int Flagged,
    int Unreviewed,
    int OpenNotes)
{
    public double VerifiedPercent => Total == 0 ? 0 : Verified * 100.0 / Total;

    public double StalePercent => Total == 0 ? 0 : StaleApprovals * 100.0 / Total;

    public double UnreviewedPercent => Total == 0 ? 0 : Unreviewed * 100.0 / Total;

    public double FlaggedPercent => Total == 0 ? 0 : Flagged * 100.0 / Total;

    public string FormatPercent(double value) =>
        value.ToString("0.0", CultureInfo.InvariantCulture) + " %";
}

public static class ReviewCoverage
{
    public static IReadOnlyList<ReviewCoverageTotals> ByComponent(IEnumerable<ReviewedFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        return [.. files
            .GroupBy(file => file.Component, StringComparer.Ordinal)
            .Select(group => Count(group.Key, group))
            .OrderBy(totals => totals.VerifiedPercent)
            .ThenBy(totals => totals.Name, StringComparer.Ordinal)];
    }

    public static ReviewCoverageTotals Overall(IEnumerable<ReviewedFile> files, string name = "HAIRS")
    {
        ArgumentNullException.ThrowIfNull(files);
        return Count(name, files);
    }

    public static string ComponentOf(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        string[] segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return "(root)";

        if (segments[0] == "src")
            return segments.Length > 1 ? segments[1] : "src";

        return segments[0];
    }

    private static ReviewCoverageTotals Count(string name, IEnumerable<ReviewedFile> files)
    {
        int total = 0, verified = 0, stale = 0, flagged = 0, unreviewed = 0, openNotes = 0;

        foreach (ReviewedFile file in files)
        {
            total++;
            openNotes += file.State.OpenNotes;

            if (file.State.IsVerified)
                verified++;
            else if (file.State.IsStaleApproval)
                stale++;
            else if (file.State.Status is ReviewStatus.Question or ReviewStatus.NeedsChange)
                flagged++;
            else
                unreviewed++;
        }

        return new ReviewCoverageTotals(name, total, verified, stale, flagged, unreviewed, openNotes);
    }
}
