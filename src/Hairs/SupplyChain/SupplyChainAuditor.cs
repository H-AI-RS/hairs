// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hairs.Storage;

namespace Hairs.SupplyChain;

/// <summary>
/// Audits repository assets critical to software supply chain integrity.
/// Tracks workflows, build scripts, package management manifests, and configurations.
/// </summary>
public static class SupplyChainAuditor
{
    private static readonly string[] BuildPipelineExtensions = [".ps1", ".sh", ".cmd", ".bat"];
    private static readonly string[] PackageManifestFiles =
    [
        "Directory.Packages.props",
        "Directory.Build.props",
        "Directory.Build.targets",
        "packages.lock.json",
        "package.json",
        "package-lock.json",
        "Cargo.toml",
        "Cargo.lock"
    ];

    private static readonly string[] ConfigurationFiles =
    [
        "hairs.config.json",
        "assurance.config.json",
        ".gitattributes",
        ".gitignore",
        "nuget.config",
        "global.json"
    ];

    /// <summary>
    /// Determines whether a given file path is a supply-chain critical asset.
    /// </summary>
    public static bool IsSupplyChainAsset(string relativePath) =>
        TryClassify(relativePath, out _, out _);

    /// <summary>
    /// Classifies a path into its supply chain asset type and descriptive label.
    /// </summary>
    public static bool TryClassify(
        string relativePath,
        out SupplyChainAssetType assetType,
        out string description)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        string normalized = relativePath.Replace('\\', '/').TrimStart('/');
        string fileName = Path.GetFileName(normalized);
        string extension = Path.GetExtension(normalized);

        // 1. CI/CD & Build Pipelines
        if (normalized.StartsWith(".github/workflows/", StringComparison.OrdinalIgnoreCase) &&
            (extension.Equals(".yml", StringComparison.OrdinalIgnoreCase) || extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase)))
        {
            assetType = SupplyChainAssetType.BuildPipeline;
            description = "GitHub Actions CI/CD workflow definition";
            return true;
        }

        if (normalized.StartsWith(".github/", StringComparison.OrdinalIgnoreCase) &&
            (extension.Equals(".yml", StringComparison.OrdinalIgnoreCase) || extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase)))
        {
            assetType = SupplyChainAssetType.BuildPipeline;
            description = "Repository automation configuration";
            return true;
        }

        if (BuildPipelineExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) &&
            !normalized.Contains("/tests/", StringComparison.OrdinalIgnoreCase) &&
            !normalized.StartsWith("tests/", StringComparison.OrdinalIgnoreCase))
        {
            assetType = SupplyChainAssetType.BuildPipeline;
            description = "Build and packaging automation script";
            return true;
        }

        if (fileName.StartsWith("Dockerfile", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("Containerfile", StringComparison.OrdinalIgnoreCase))
        {
            assetType = SupplyChainAssetType.BuildPipeline;
            description = "Container build recipe";
            return true;
        }

        // 2. Package Manifests & Dependencies
        if (PackageManifestFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase))
        {
            assetType = SupplyChainAssetType.PackageManifest;
            description = "Central package management / dependency manifest";
            return true;
        }

        if (extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".fsproj", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".sln", StringComparison.OrdinalIgnoreCase))
        {
            assetType = SupplyChainAssetType.PackageManifest;
            description = "Project and solution structure specification";
            return true;
        }

        // 3. Configurations & Health
        if (ConfigurationFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase))
        {
            assetType = SupplyChainAssetType.Configuration;
            description = "Repository security, assurance, or environment configuration";
            return true;
        }

        // 4. Vendored / Third-Party Code Paths
        if (normalized.StartsWith("vendor/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("third_party/", StringComparison.OrdinalIgnoreCase))
        {
            assetType = SupplyChainAssetType.ThirdPartyCode;
            description = "Vendored external code dependency";
            return true;
        }

        assetType = default;
        description = string.Empty;
        return false;
    }

    /// <summary>
    /// Audits all supply-chain assets in the storage against recorded file reviews.
    /// </summary>
    public static async ValueTask<SupplyChainAuditReport> AuditAsync(
        IHairsStorage storage,
        IReadOnlyList<string>? candidatePaths = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(storage);

        var store = new ReviewStore(storage);
        ReviewStore.ReviewRecordSet records = await store.ReadAllAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<string, FileReview> reviewMap = records.Reviews;

        var paths = candidatePaths ?? await DiscoverAssetsAsync(storage, cancellationToken).ConfigureAwait(false);
        var audited = new List<SupplyChainAsset>();

        foreach (string path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryClassify(path, out SupplyChainAssetType type, out string desc))
                continue;

            StorageResult<StorageTextContent> contentResult = await storage.ReadTextAsync(path, cancellationToken).ConfigureAwait(false);
            if (!contentResult.Succeeded || contentResult.Value is null)
                continue;

            string content = contentResult.Value.Text;
            string currentHash = ReviewContentHash.Compute(content);

            if (reviewMap.TryGetValue(path, out FileReview? review))
            {
                bool isFresh = review.Status == ReviewStatus.Reviewed &&
                               string.Equals(review.ReviewedContentHash, currentHash, StringComparison.Ordinal);

                audited.Add(new SupplyChainAsset(
                    Path: path,
                    AssetType: type,
                    Description: desc,
                    Status: review.Status,
                    IsFresh: isFresh,
                    Reviewer: review.Reviewer,
                    ReviewedAt: review.ReviewedAt,
                    ContentHash: currentHash));
            }
            else
            {
                audited.Add(new SupplyChainAsset(
                    Path: path,
                    AssetType: type,
                    Description: desc,
                    Status: ReviewStatus.Unreviewed,
                    IsFresh: false,
                    ContentHash: currentHash));
            }
        }

        return new SupplyChainAuditReport { Assets = audited };
    }

    /// <summary>
    /// Audits a local filesystem repository for supply chain integrity.
    /// </summary>
    public static async ValueTask<SupplyChainAuditReport> AuditRepositoryAsync(
        string root,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);

        var storage = new FileSystemHairsStorage(root);
        IReadOnlyList<string> candidatePaths = EnumeratePhysicalCandidates(root);
        return await AuditAsync(storage, candidatePaths, cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<string> EnumeratePhysicalCandidates(string root)
    {
        var found = new List<string>();
        string[] ignoredDirs = [".git", "bin", "obj", ".vs", "artifacts", "node_modules", ".hairs-review"];

        void Walk(string currentDir)
        {
            foreach (string dir in Directory.EnumerateDirectories(currentDir))
            {
                string dirName = Path.GetFileName(dir);
                if (ignoredDirs.Contains(dirName, StringComparer.OrdinalIgnoreCase))
                    continue;

                Walk(dir);
            }

            foreach (string file in Directory.EnumerateFiles(currentDir))
            {
                string rel = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (IsSupplyChainAsset(rel))
                    found.Add(rel);
            }
        }

        Walk(root);
        return found;
    }

    private static async ValueTask<IReadOnlyList<string>> DiscoverAssetsAsync(
        IHairsStorage storage,
        CancellationToken cancellationToken)
    {
        var found = new List<string>();

        async ValueTask WalkAsync(string dir)
        {
            StorageResult<IReadOnlyList<StorageEntry>> list = await storage.ListAsync(dir, cancellationToken).ConfigureAwait(false);
            if (!list.Succeeded || list.Value is null)
                return;

            foreach (StorageEntry entry in list.Value)
            {
                if (entry.IsDirectory)
                {
                    if (entry.RelativePath.Equals(".hairs-review", StringComparison.OrdinalIgnoreCase) ||
                        entry.RelativePath.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                        entry.RelativePath.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                        entry.RelativePath.Equals("obj", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    await WalkAsync(entry.RelativePath).ConfigureAwait(false);
                }
                else if (IsSupplyChainAsset(entry.RelativePath))
                {
                    found.Add(entry.RelativePath);
                }
            }
        }

        await WalkAsync(string.Empty).ConfigureAwait(false);
        return found;
    }
}
