// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Hairs.Storage;
using Hairs.SupplyChain;
using Xunit;

namespace Hairs.Tests;

public sealed class SupplyChainTests
{
    private static readonly DateTimeOffset Stamp = DateTimeOffset.Parse("2026-11-04T12:00:00Z");

    [Theory]
    [InlineData(".github/workflows/ci.yml", SupplyChainAssetType.BuildPipeline)]
    [InlineData(".github/workflows/release.yaml", SupplyChainAssetType.BuildPipeline)]
    [InlineData("pack.ps1", SupplyChainAssetType.BuildPipeline)]
    [InlineData("build.sh", SupplyChainAssetType.BuildPipeline)]
    [InlineData("Directory.Packages.props", SupplyChainAssetType.PackageManifest)]
    [InlineData("Directory.Build.props", SupplyChainAssetType.PackageManifest)]
    [InlineData("src/MyLib/MyLib.csproj", SupplyChainAssetType.PackageManifest)]
    [InlineData("packages.lock.json", SupplyChainAssetType.PackageManifest)]
    [InlineData("hairs.config.json", SupplyChainAssetType.Configuration)]
    [InlineData(".gitattributes", SupplyChainAssetType.Configuration)]
    [InlineData("vendor/external.c", SupplyChainAssetType.ThirdPartyCode)]
    public void Classifies_Supply_Chain_Assets_Correctly(string path, SupplyChainAssetType expectedType)
    {
        Assert.True(SupplyChainAuditor.TryClassify(path, out SupplyChainAssetType actualType, out string desc));
        Assert.Equal(expectedType, actualType);
        Assert.False(string.IsNullOrWhiteSpace(desc));
    }

    [Theory]
    [InlineData("src/MyLib/Engine.cs")]
    [InlineData("tests/MyLib.Tests/UnitTest1.cs")]
    [InlineData("docs/index.md")]
    public void Non_Supply_Chain_Assets_Are_Not_Classified(string path)
    {
        Assert.False(SupplyChainAuditor.TryClassify(path, out _, out _));
        Assert.False(SupplyChainAuditor.IsSupplyChainAsset(path));
    }

    [Fact]
    public async Task Audits_Unreviewed_Supply_Chain_Asset()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "hairs-sc-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string workflowDir = Path.Combine(tempDir, ".github", "workflows");
            Directory.CreateDirectory(workflowDir);
            File.WriteAllText(Path.Combine(workflowDir, "ci.yml"), "name: CI\non: push\n");

            var storage = new FileSystemHairsStorage(tempDir);
            SupplyChainAuditReport report = await SupplyChainAuditor.AuditAsync(
                storage, [".github/workflows/ci.yml"]);

            Assert.Equal(1, report.TotalAssets);
            Assert.Equal(0, report.VerifiedCount);
            Assert.Equal(1, report.UnreviewedCount);
            Assert.False(report.IsCompliant);
            Assert.Equal(0.0, report.CoveragePercent);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Audits_Verified_And_Stale_Supply_Chain_Assets()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "hairs-sc-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string scriptPath = Path.Combine(tempDir, "pack.ps1");
            const string originalContent = "Write-Host 'Building'\n";
            File.WriteAllText(scriptPath, originalContent);

            var storage = new FileSystemHairsStorage(tempDir);
            var store = new ReviewStore(storage);

            // Record human review
            FileReview review = FileReview.Empty("pack.ps1")
                .WithDecision(ReviewStatus.Reviewed, "Alice", originalContent, Stamp);
            await store.WriteAsync(review);

            // 1. Audit before change -> Verified
            SupplyChainAuditReport report1 = await SupplyChainAuditor.AuditAsync(
                storage, ["pack.ps1"]);

            Assert.Equal(1, report1.TotalAssets);
            Assert.Equal(1, report1.VerifiedCount);
            Assert.Equal(0, report1.StaleCount);
            Assert.True(report1.IsCompliant);
            Assert.Equal(100.0, report1.CoveragePercent);

            // 2. Tamper/change script content -> Stale!
            File.WriteAllText(scriptPath, "Write-Host 'Tampered script'\n");

            SupplyChainAuditReport report2 = await SupplyChainAuditor.AuditAsync(
                storage, ["pack.ps1"]);

            Assert.Equal(1, report2.TotalAssets);
            Assert.Equal(0, report2.VerifiedCount);
            Assert.Equal(1, report2.StaleCount);
            Assert.False(report2.IsCompliant);
            Assert.Equal(0.0, report2.CoveragePercent);
            Assert.True(report2.Assets[0].IsStale);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Markdown_And_Json_Reports_Render_Properly()
    {
        var assets = new List<SupplyChainAsset>
        {
            new("pack.ps1", SupplyChainAssetType.BuildPipeline, "Build script", ReviewStatus.Reviewed, true, "Alice", Stamp, "hash1"),
            new("Directory.Packages.props", SupplyChainAssetType.PackageManifest, "CPM props", ReviewStatus.Reviewed, false, "Bob", Stamp, "hash2"),
            new("hairs.config.json", SupplyChainAssetType.Configuration, "Assurance config", ReviewStatus.Unreviewed, false),
        };

        var report = new SupplyChainAuditReport { Assets = assets };

        Assert.Equal(3, report.TotalAssets);
        Assert.Equal(1, report.VerifiedCount);
        Assert.Equal(1, report.StaleCount);
        Assert.Equal(1, report.UnreviewedCount);
        Assert.Equal(33.33, report.CoveragePercent);
        Assert.False(report.IsCompliant);

        string md = report.ToMarkdown();
        Assert.Contains("## Supply Chain Integrity Audit", md);
        Assert.Contains("`pack.ps1`", md);
        Assert.Contains("`Directory.Packages.props`", md);
        Assert.Contains("**STALE**", md);

        string json = report.ToJson();
        Assert.Contains("\"totalAssets\": 3", json);
        Assert.Contains("\"verified\": 1", json);
        Assert.Contains("\"stale\": 1", json);
        Assert.Contains("\"coveragePercent\": 33.33", json);
    }
}
