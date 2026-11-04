// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Hairs.Language.CSharp;
using Hairs.Assurance;

namespace Hairs.Cli.Assurance;

/// <summary>One covered file as <c>assurance prune</c> reports it.</summary>
internal sealed record PrunedFile(string File, IReadOnlyList<AssurancePruneEntry> Entries, string? Problem);

/// <summary>
/// <c>prune</c>: takes out the annotation lines the rubric no longer asks for.
/// </summary>
internal static partial class AssuranceCommand
{
    private static int Prune(Options options, TextWriter output, TextWriter error)
    {
        string root = RootOf(options);
        string? jsonPath = options.Value("--json");
        PrepareOut(jsonPath);

        AssuranceComponentConfig config = OwnedConfig(root, "prune");
        ComponentSourceSet set = ComponentSources.Discover(root, config);
        CSharpAssuranceFileScanner scanner = ScannerFor(config);
        bool dryRun = options.Has("--dry-run");
        var files = new List<PrunedFile>();

        foreach (ComponentSourceFile file in set.Files)
        {
            if (!AssuranceSourceText.TryRead(file.FullPath, out AssuranceSourceText? source, out string? problem))
            {
                files.Add(new PrunedFile(file.RelativePath, [], $"the file {problem}"));
                continue;
            }

            AssurancePruneFileResult result = AssurancePruning.Apply(source!.Text, file.RelativePath, scanner);
            if (result.Entries.Count == 0 && result.Problem is null)
                continue;

            if (dryRun || !result.Changed)
            {
                files.Add(new PrunedFile(
                    file.RelativePath, result.Entries, result.Problem is null ? null : $"the file {result.Problem}"));
                continue;
            }

            string? failure = source.TryWrite(file.FullPath, result.Text, out string? writeProblem)
                ? ReadBack(file, source, result.Text, scanner)
                : $"the file {writeProblem}";

            files.Add(failure is null
                ? new PrunedFile(file.RelativePath, result.Entries, null)
                : new PrunedFile(
                    file.RelativePath,
                    [.. result.Entries.Select(entry => entry.Removed
                        ? entry with { Removed = false, Reason = $"not removed: {failure}" }
                        : entry)],
                    failure));
        }

        string component = ComponentName(root, config);

        WritePruneText(
            component, set.Files.Count, files, dryRun, AssuranceReportContext.CommandsFor(config).Generate,
            jsonPath == "-" ? error : output);

        bool left = files.Any(static file => file.Problem is not null || file.Entries.Any(static entry => !entry.Removed));
        if (jsonPath is not null && !WriteOut(jsonPath, AssuranceJson.PruneReport(component, files, dryRun), output, error))
        {
            if (!dryRun && files.Any(static file => file.Entries.Any(static entry => entry.Removed)))
                error.WriteLine("hairs assurance: the lines reported above as removed were removed from the sources.");

            return Refused;
        }

        return left ? Refused : Done;
    }

    private static string? ReadBack(
        ComponentSourceFile file, AssuranceSourceText source, string written, CSharpAssuranceFileScanner scanner)
    {
        string? problem;
        if (!AssuranceSourceText.TryRead(file.FullPath, out AssuranceSourceText? back, out string? readProblem))
        {
            problem = $"could not be read back after writing: {readProblem}";
        }
        else if (!string.Equals(back!.Text, written, StringComparison.Ordinal) || back.HasByteOrderMark != source.HasByteOrderMark)
        {
            problem = "did not read back as the text that was written";
        }
        else
        {
            problem = AssurancePruning.CodeDifference(
                scanner.ScanFile(source.Text, file.RelativePath),
                scanner.ScanFile(back.Text, file.RelativePath)) is { } difference
                ? $"did not scan the same after writing ({difference})"
                : null;
        }

        if (problem is null)
            return null;

        try
        {
            File.WriteAllBytes(file.FullPath, source.Original);
            return $"the file {problem}; its original bytes were written back";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"the file {problem}, and its original bytes could not be written back: {exception.Message}";
        }
    }

    private static void WritePruneText(
        string component, int covered, IReadOnlyList<PrunedFile> files, bool dryRun, string generate, TextWriter output)
    {
        AssurancePruneEntry[] entries = [.. files.SelectMany(static file => file.Entries)];
        int blocks = entries.Count(static entry => entry.Removed && entry.Kind == AssurancePruneKind.Block);
        int criteria = entries.Count(static entry => entry.Removed && entry.Kind == AssurancePruneKind.Criterion);
        int changed = files.Count(static file => file.Entries.Any(static entry => entry.Removed));
        int left = entries.Count(static entry => !entry.Removed);
        int problems = files.Count(static file => file.Problem is not null);

        output.WriteLine(Invariant(
            $"{component}: {covered} covered files; {blocks} blocks and {criteria} criteria ") +
            (dryRun ? "would be removed" : "removed") +
            Invariant($" from {changed} files, {left} left in place, {problems} files with a problem"));

        foreach (PrunedFile file in files)
        {
            output.WriteLine(file.File);
            foreach (AssurancePruneEntry entry in file.Entries)
            {
                string verb = entry.Removed ? (dryRun ? "would remove" : "removed") : "left";
                output.WriteLine(Invariant(
                    $"  {verb} {PruneKindName(entry.Kind)} at line {entry.Line}  {entry.Unit}: {entry.Reason}"));
            }

            if (file.Problem is not null)
                output.WriteLine($"  problem: {file.Problem}");
        }

        if (dryRun)
            output.WriteLine("Dry run: nothing was written.");
        else if (blocks + criteria > 0)
            output.WriteLine($"Run: {generate}, which rewrites the file headers, the report and the manifest to match.");
    }

    internal static string PruneKindName(AssurancePruneKind kind) =>
        kind == AssurancePruneKind.Block ? "block" : "criterion";
}
