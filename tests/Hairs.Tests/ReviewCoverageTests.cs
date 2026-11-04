// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using Hairs;
using Hairs.Cli;

namespace Hairs.Tests;

/// <summary>
/// What review coverage counts and refuses to count.
/// </summary>
public sealed class ReviewCoverageTests
{
    [Fact(Timeout = 600000)]
    public void A_Stale_Approval_Does_Not_Count_As_Reviewed()
    {
        ReviewCoverageTotals totals = ReviewCoverage.Overall(
        [
            File("a.cs", ReviewStatus.Reviewed, ReviewFreshness.Current),
            File("b.cs", ReviewStatus.Reviewed, ReviewFreshness.Stale),
        ]);

        Assert.Equal(2, totals.Total);
        Assert.Equal(1, totals.Verified);
        Assert.Equal(1, totals.StaleApprovals);
        Assert.Equal(50, totals.VerifiedPercent);
    }

    [Fact(Timeout = 600000)]
    public void The_Buckets_Sum_To_The_Total()
    {
        ReviewCoverageTotals totals = ReviewCoverage.Overall(
        [
            File("a.cs", ReviewStatus.Reviewed, ReviewFreshness.Current),
            File("b.cs", ReviewStatus.Reviewed, ReviewFreshness.Stale),
            File("c.cs", ReviewStatus.Question, ReviewFreshness.Current),
            File("d.cs", ReviewStatus.NeedsChange, ReviewFreshness.Current),
            File("e.cs", ReviewStatus.InReview, ReviewFreshness.Current),
            File("f.cs", ReviewStatus.Unreviewed, ReviewFreshness.NotReviewed),
        ]);

        Assert.Equal(
            totals.Total,
            totals.Verified + totals.StaleApprovals + totals.Flagged + totals.Unreviewed);
    }

    [Fact(Timeout = 600000)]
    public void An_Empty_Workspace_Reports_Zero_Rather_Than_Dividing_By_Zero()
    {
        ReviewCoverageTotals totals = ReviewCoverage.Overall([]);

        Assert.Equal(0, totals.Total);
        Assert.Equal(0, totals.VerifiedPercent);
    }

    [Fact(Timeout = 600000)]
    public void Components_Are_Reported_Worst_First()
    {
        IReadOnlyList<ReviewCoverageTotals> components = ReviewCoverage.ByComponent(
        [
            File("Hairs.Cli/src/a.cs", ReviewStatus.Reviewed, ReviewFreshness.Current),
            File("Hairs.Core/src/b.cs", ReviewStatus.Unreviewed, ReviewFreshness.NotReviewed),
        ]);

        Assert.Equal("Hairs.Core", components[0].Name);
        Assert.Equal("Hairs.Cli", components[1].Name);
    }

    [Theory(Timeout = 600000)]
    [InlineData("Hairs.Core/src/Hairs.Core/Runtime/State.cs", "Hairs.Core")]
    [InlineData("src/Hairs.Cli/Program.cs", "Hairs.Cli")]
    [InlineData("tests/hairs-tests/x.cs", "tests")]
    [InlineData("Directory.Build.props", "Directory.Build.props")]
    public void A_File_Is_Grouped_Under_Its_Component(string path, string expected) =>
        Assert.Equal(expected, ReviewCoverage.ComponentOf(path));

    [Theory(Timeout = 600000)]
    [InlineData("Hairs.Core/src/Hairs.Core/Runtime/State.cs", "Hairs.Core/Runtime/State.cs")]
    [InlineData("Hairs.Cli/Hairs.Core/src/Hairs.Core/Runtime/State.cs", "Hairs.Core/Runtime/State.cs")]
    [InlineData("src/Hairs.Cli/Program.cs", "Hairs.Cli/Program.cs")]
    [InlineData("eng/solutions.json.cs", "eng/solutions.json.cs")]
    public void A_Nested_Checkout_Has_The_Same_Identity_As_The_Real_File(string path, string identity) =>
        Assert.Equal(identity, SourceInventory.IdentityOf(path));

    [Fact(Timeout = 600000)]
    public void The_Canonical_Checkout_Is_Less_Nested_Than_A_Copy_Of_It()
    {
        Assert.True(
            SourceInventory.ComponentDepth("Hairs.Core/src/Hairs.Core/Node.cs") <
            SourceInventory.ComponentDepth("Hairs.Cli/Hairs.Core/src/Hairs.Core/Node.cs"));
    }

    [Theory(Timeout = 600000)]
    [InlineData("obj/Debug/net10.0/A.AssemblyInfo.cs")]
    [InlineData("src/A.g.cs")]
    [InlineData("src/A.generated.cs")]
    [InlineData("src/Form.Designer.cs")]
    public void Generated_Source_Is_Not_Counted(string path) =>
        Assert.True(SourceInventory.IsGenerated(path));

    [Fact(Timeout = 600000)]
    public void Ordinary_Source_Is_Counted() =>
        Assert.False(SourceInventory.IsGenerated("src/Hairs.Core/Runtime/State.cs"));

    [Fact(Timeout = 600000)]
    public void Only_Invalidated_Reviews_Are_Regressions()
    {
        IReadOnlyList<ReviewedFile> regressions = ReviewReport.Regressions(
        [
            File("stale.cs", ReviewStatus.Reviewed, ReviewFreshness.Stale),
            File("needs-change.cs", ReviewStatus.NeedsChange, ReviewFreshness.Current),
            File("never.cs", ReviewStatus.Unreviewed, ReviewFreshness.NotReviewed),
            File("fine.cs", ReviewStatus.Reviewed, ReviewFreshness.Current),
        ]);

        Assert.Equal(2, regressions.Count);
        Assert.DoesNotContain(regressions, file => file.Path == "never.cs");
        Assert.DoesNotContain(regressions, file => file.Path == "fine.cs");
    }

    [Fact(Timeout = 600000)]
    public void The_Markdown_Report_States_What_It_Excludes()
    {
        string markdown = ReviewReport.ToMarkdown(
            [File("a.cs", ReviewStatus.Reviewed, ReviewFreshness.Stale)]);

        Assert.Contains("Modified since review | 1", markdown, StringComparison.Ordinal);
        Assert.Contains("not** included in the coverage", markdown, StringComparison.Ordinal);
        Assert.Contains("0.0 %", markdown, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void The_Json_Report_Carries_The_Overall_And_Component_Totals()
    {
        string json = ReviewReport.ToJson(
        [
            File("Hairs.Core/a.cs", ReviewStatus.Reviewed, ReviewFreshness.Current),
            File("Hairs.Cli/b.cs", ReviewStatus.Unreviewed, ReviewFreshness.NotReviewed),
        ]);

        Assert.Contains("\"overall\"", json, StringComparison.Ordinal);
        Assert.Contains("\"components\"", json, StringComparison.Ordinal);
        Assert.Contains("\"verifiedPercent\": 50", json, StringComparison.Ordinal);
    }

    private static ReviewedFile File(string path, ReviewStatus status, ReviewFreshness freshness) =>
        new(path, ReviewCoverage.ComponentOf(path), new ReviewState(status, freshness));
}
