// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hairs;
using Hairs.Cli;
using Hairs.Cli.Assurance;
using Hairs.SupplyChain;

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    PrintUsage();
    return 0;
}

if (args[0] == "assurance")
{
    var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    using var output = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true };
    using var error = new StreamWriter(Console.OpenStandardError(), utf8) { AutoFlush = true };
    return AssuranceCommand.Run(args[1..], output, error);
}

string command = args[0];
string root = ValueOf("--root") ?? Directory.GetCurrentDirectory();

if (!Directory.Exists(root))
{
    Console.Error.WriteLine($"hairs: '{root}' is not a directory.");
    return 2;
}

IReadOnlyList<string> files = SourceInventory.Enumerate(root, new InventoryOptions
{
    Extensions = ValueOf("--extensions") is { Length: > 0 } extensions
        ? [.. extensions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]
        : [".cs"],
});

IReadOnlyList<ReviewedFile> evaluated = await ReviewReport.EvaluateAsync(root, files).ConfigureAwait(false);

switch (command)
{
    case "coverage":
        return Coverage(evaluated);

    case "check":
        return Check(evaluated);

    case "list":
        return List(evaluated);

    case "supply-chain" or "supplychain":
        return await SupplyChainAsync(root).ConfigureAwait(false);

    default:
        Console.Error.WriteLine($"hairs: unknown command '{command}'.");
        PrintUsage();
        return 2;
}

int Coverage(IReadOnlyList<ReviewedFile> results)
{
    string markdown = ReviewReport.ToMarkdown(results);
    Console.Out.Write(markdown);

    if (ValueOf("--markdown") is { Length: > 0 } markdownPath)
        WriteReport(markdownPath, markdown);

    if (ValueOf("--json") is { Length: > 0 } jsonPath)
        WriteReport(jsonPath, ReviewReport.ToJson(results));

    if (ValueOf("--minimum") is not { Length: > 0 } minimumText)
        return 0;

    if (!double.TryParse(minimumText, NumberStyles.Float, CultureInfo.InvariantCulture, out double minimum))
    {
        Console.Error.WriteLine($"hairs: '--minimum {minimumText}' is not a number.");
        return 2;
    }

    ReviewCoverageTotals overall = ReviewCoverage.Overall(results);
    if (overall.VerifiedPercent + 0.0001 >= minimum)
        return 0;

    Console.Error.WriteLine(
        $"::error::Human review coverage is {overall.FormatPercent(overall.VerifiedPercent)}, " +
        $"below the required {minimum.ToString("0.0", CultureInfo.InvariantCulture)} %.");
    return 1;
}

int Check(IReadOnlyList<ReviewedFile> results)
{
    IReadOnlyList<ReviewedFile> regressions = ReviewReport.Regressions(results);

    if (ValueOf("--changed") is { Length: > 0 } changedPath && File.Exists(changedPath))
    {
        string[] changed = [.. File.ReadAllLines(changedPath)
            .Select(line => line.Trim().Replace('\\', '/'))
            .Where(line => line.Length > 0)];

        regressions = [.. regressions.Where(file => changed.Any(entry =>
            file.Path.Equals(entry, StringComparison.OrdinalIgnoreCase) ||
            file.Path.StartsWith(entry.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)))];
    }

    if (regressions.Count == 0)
    {
        Console.Out.WriteLine("No file has changed since it was reviewed, and none is marked as needing a change.");
        return 0;
    }

    foreach (ReviewedFile file in regressions)
    {
        string reviewer = file.Reviewer.Length > 0 ? file.Reviewer : "somebody";
        Console.Out.WriteLine(file.State.IsStaleApproval
            ? $"::warning file={file.Path}::{reviewer} reviewed this file; it has changed since. The review is now stale."
            : $"::warning file={file.Path}::{reviewer} marked this file as needing a change.");
    }

    Console.Out.WriteLine();
    Console.Out.WriteLine($"{regressions.Count} files need a human review again.");

    return HasFlag("--fail-on-stale") ? 1 : 0;
}

int List(IReadOnlyList<ReviewedFile> results)
{
    string? wanted = ValueOf("--status");
    foreach (ReviewedFile file in results.OrderBy(file => file.Path, StringComparer.Ordinal))
    {
        string state = file.State.ToDisplayString();
        if (wanted is { Length: > 0 } && !state.Contains(wanted, StringComparison.OrdinalIgnoreCase))
            continue;

        Console.Out.WriteLine($"{state,-28} {file.Path}");
    }

    return 0;
}

async Task<int> SupplyChainAsync(string repoRoot)
{
    SupplyChainAuditReport report = await SupplyChainAuditor.AuditRepositoryAsync(repoRoot).ConfigureAwait(false);
    string markdown = report.ToMarkdown();
    Console.Out.Write(markdown);

    if (ValueOf("--markdown") is { Length: > 0 } markdownPath)
        WriteReport(markdownPath, markdown);

    if (ValueOf("--json") is { Length: > 0 } jsonPath)
        WriteReport(jsonPath, report.ToJson());

    foreach (SupplyChainAsset asset in report.Assets)
    {
        if (asset.IsStale)
        {
            string reviewer = string.IsNullOrWhiteSpace(asset.Reviewer) ? "somebody" : asset.Reviewer;
            Console.Error.WriteLine($"::warning file={asset.Path}::{reviewer} reviewed this supply chain asset; it has changed since. The review is now stale.");
        }
        else if (asset.Status is ReviewStatus.NeedsChange)
        {
            Console.Error.WriteLine($"::warning file={asset.Path}::Supply chain asset is marked as needing a change.");
        }
        else if (asset.Status == ReviewStatus.Unreviewed && HasFlag("--fail-on-stale"))
        {
            Console.Error.WriteLine($"::error file={asset.Path}::Supply chain asset has not been reviewed.");
        }
    }

    if (ValueOf("--minimum") is { Length: > 0 } minimumText)
    {
        if (!double.TryParse(minimumText, NumberStyles.Float, CultureInfo.InvariantCulture, out double minimum))
        {
            Console.Error.WriteLine($"hairs: '--minimum {minimumText}' is not a number.");
            return 2;
        }

        if (report.CoveragePercent + 0.0001 < minimum)
        {
            Console.Error.WriteLine(
                $"::error::Supply chain review coverage is {report.CoveragePercent.ToString("0.0", CultureInfo.InvariantCulture)} %, " +
                $"below the required {minimum.ToString("0.0", CultureInfo.InvariantCulture)} %.");
            return 1;
        }
    }

    if (HasFlag("--fail-on-stale") && !report.IsCompliant)
    {
        Console.Error.WriteLine($"::error::Supply chain integrity audit failed: {report.StaleCount} stale, {report.UnreviewedCount} unreviewed, {report.FlaggedCount} flagged.");
        return 1;
    }

    return 0;
}

void WriteReport(string path, string content)
{
    string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
    if (directory is { Length: > 0 })
        Directory.CreateDirectory(directory);

    File.WriteAllText(path, content);
    Console.Error.WriteLine($"hairs: wrote {path}");
}

string? ValueOf(string name)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.Ordinal))
            return args[i + 1];
    }

    return null;
}

bool HasFlag(string name) => args.Contains(name, StringComparer.Ordinal);

static void PrintUsage() => Console.Out.WriteLine(
    """
    hairs — human review and code assurance CLI.

    Usage:
      hairs coverage     [options]   Report how much of the source a human has reviewed.
      hairs check        [options]   Report reviews invalidated by a change.
      hairs list         [options]   Print every file and its review state.
      hairs supply-chain [options]   Audit review integrity of supply chain assets (workflows,
                                     package manifests, build scripts, configs).
      hairs assurance    <command>   Per-declaration assurance annotations of one component
                                     (list, insert, generate, check, status, prune).
                                     "hairs assurance --help" for more.

    Options:
      --root <dir>          Repository root. Defaults to the working directory.
      --extensions <list>   Comma-separated source extensions. Defaults to ".cs".
      --markdown <file>     Write the coverage report as Markdown.
      --json <file>         Write the coverage report as JSON.
      --minimum <percent>   Fail when coverage is below this. Off by default.
      --changed <file>      A file of changed paths, one per line, to restrict "check" to.
      --fail-on-stale       Make "check" or "supply-chain" exit non-zero. Annotates only by default.
      --status <text>       Restrict "list" to states whose label contains this.

    Review records live in .hairs-review/ beside the source and
    are meant to be committed. Staleness is decided by content, not by commit:
    a rebase does not invalidate a review, and a revert restores one.
    """);
