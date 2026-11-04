// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Hairs.Cli;

/// <summary>
/// Decides which files the coverage number is computed over.
///
/// The denominator is the whole claim. "83% of source files are human reviewed"
/// means nothing until it says which files, so the rules are here, in one place,
/// stated rather than scattered through a walk.
/// </summary>
public sealed record InventoryOptions
{
    /// <summary>
    /// Extensions counted as reviewable source. C# only by default.
    /// </summary>
    public IReadOnlyList<string> Extensions { get; init; } = [".cs"];

    /// <summary>
    /// Directory names never descended into, at any depth. Build output, VCS
    /// metadata, package caches, and the review records themselves.
    /// </summary>
    public IReadOnlyList<string> ExcludedDirectories { get; init; } =
        ["bin", "obj", ".git", ".vs", "node_modules", "artifacts", "packages", ".hairs-review"];

    /// <summary>
    /// Path fragments excluded wherever they occur, matched against the
    /// forward-slash relative path.
    /// </summary>
    public IReadOnlyList<string> ExcludedPaths { get; init; } = ["tests/wpt/", "tests/octane/", "acid/", "css2/", "svg/"];

    /// <summary>
    /// Whether generated files are counted. They are not, by default: a human
    /// cannot meaningfully review a file a tool rewrites.
    /// </summary>
    public bool ExcludeGenerated { get; init; } = true;
}

/// <summary>
/// Walks a repository and lists the source files a review number is computed over.
/// </summary>
public static class SourceInventory
{
    /// <summary>Lists reviewable files under <paramref name="root"/>, as forward-slash relative paths.</summary>
    public static IReadOnlyList<string> Enumerate(string root, InventoryOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        options ??= new InventoryOptions();

        string full = Path.GetFullPath(root);
        var found = new List<string>();
        Walk(full, full, options, found);

        found.Sort(static (left, right) =>
        {
            int byNesting = ComponentDepth(left).CompareTo(ComponentDepth(right));
            if (byNesting != 0)
                return byNesting;

            int bySegments = left.Count(c => c == '/').CompareTo(right.Count(c => c == '/'));
            return bySegments != 0 ? bySegments : string.CompareOrdinal(left, right);
        });

        var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new List<string>(found.Count);
        foreach (string file in found)
        {
            if (identities.Add(IdentityOf(file)))
                files.Add(file);
        }

        return files;
    }

    /// <summary>
    /// A file's identity across nested checkouts: the path from its last component segment onward.
    /// </summary>
    public static string IdentityOf(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        int start = -1;
        int index = 0;
        foreach (string segment in relativePath.Split('/'))
        {
            if (segment.StartsWith("Hairs.", StringComparison.OrdinalIgnoreCase))
            {
                start = index;
            }

            index += segment.Length + 1;
        }

        return start < 0 ? relativePath : relativePath[start..];
    }

    /// <summary>
    /// How many component-named segments a path passes through — its nesting depth.
    /// </summary>
    public static int ComponentDepth(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        int depth = 0;
        foreach (string segment in relativePath.Split('/'))
        {
            if (segment.StartsWith("Hairs.", StringComparison.OrdinalIgnoreCase))
            {
                depth++;
            }
        }

        return depth;
    }

    private static void Walk(string root, string directory, InventoryOptions options, List<string> into)
    {
        foreach (string child in Directory.EnumerateDirectories(directory))
        {
            if (options.ExcludedDirectories.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase))
                continue;

            if (new DirectoryInfo(child).Attributes.HasFlag(FileAttributes.ReparsePoint))
                continue;

            Walk(root, child, options, into);
        }

        foreach (string file in Directory.EnumerateFiles(directory))
        {
            if (!options.Extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                continue;

            string relative = Path.GetRelativePath(root, file).Replace('\\', '/');

            if (options.ExcludedPaths.Any(fragment =>
                    relative.StartsWith(fragment, StringComparison.OrdinalIgnoreCase) ||
                    relative.Contains("/" + fragment, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (options.ExcludeGenerated && IsGenerated(relative))
                continue;

            into.Add(relative);
        }
    }

    /// <summary>
    /// Recognizes generated source by name rather than by reading it.
    /// </summary>
    public static bool IsGenerated(string relativePath) =>
        relativePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
        relativePath.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase) ||
        relativePath.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase) ||
        relativePath.EndsWith("AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase) ||
        relativePath.EndsWith(".AssemblyAttributes.cs", StringComparison.OrdinalIgnoreCase);
}
