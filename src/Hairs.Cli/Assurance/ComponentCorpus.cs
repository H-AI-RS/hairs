// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Hairs.Assurance;

namespace Hairs.Cli.Assurance;

/// <summary>
/// One component-level artefact as it is on disk, with what it takes to write
/// it back in the same style.
/// </summary>
internal sealed record ComponentArtefact(string RelativePath, string FullPath, AssuranceSourceText? Source, string NewLine)
{
    /// <summary><paramref name="desired"/>, which the generator rendered in LF, in this file's line endings.</summary>
    public string Restyle(string desired) =>
        NewLine == "\r\n" ? desired.Replace("\n", "\r\n", StringComparison.Ordinal) : desired;
}

/// <summary>
/// A component read from disk for the generator: the covered files as
/// <see cref="AssuranceSource"/>, the component-level artefacts, and every
/// file that could not be read.
/// </summary>
internal sealed class ComponentCorpus
{
    private ComponentCorpus(
        AssuranceCorpus corpus,
        ComponentSourceSet set,
        IReadOnlyDictionary<string, (ComponentSourceFile File, AssuranceSourceText Text)> sources,
        IReadOnlyDictionary<string, ComponentArtefact> artefacts,
        IReadOnlyList<AssuranceViolation> problems)
    {
        Corpus = corpus;
        Set = set;
        Sources = sources;
        Artefacts = artefacts;
        Problems = problems;
    }

    public AssuranceCorpus Corpus { get; }

    public ComponentSourceSet Set { get; }

    /// <summary>The covered files that were read, by root-relative path.</summary>
    public IReadOnlyDictionary<string, (ComponentSourceFile File, AssuranceSourceText Text)> Sources { get; }

    /// <summary>The component-level artefacts, by root-relative path.</summary>
    public IReadOnlyDictionary<string, ComponentArtefact> Artefacts { get; }

    /// <summary>Files that could not be read as UTF-8. They are missing from <see cref="Corpus"/>.</summary>
    public IReadOnlyList<AssuranceViolation> Problems { get; }

    public static ComponentCorpus Load(string root, AssuranceComponentConfig config)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(config);

        ComponentSourceSet set = ComponentSources.Discover(root, config);
        var problems = new List<AssuranceViolation>();
        var sources = new Dictionary<string, (ComponentSourceFile, AssuranceSourceText)>(StringComparer.Ordinal);
        var files = new List<AssuranceSource>(set.Files.Count);

        foreach (ComponentSourceFile file in set.Files)
        {
            if (!AssuranceSourceText.TryRead(file.FullPath, out AssuranceSourceText? text, out string? problem))
            {
                problems.Add(new AssuranceViolation("IO", file.RelativePath, null, $"{file.RelativePath} {problem}"));
                continue;
            }

            sources[file.RelativePath] = (file, text!);
            files.Add(new AssuranceSource(file.RelativePath, file.Project.AssemblyName, text!.Text));
        }

        var artefacts = new Dictionary<string, ComponentArtefact>(StringComparer.Ordinal);
        var current = new Dictionary<string, string>(StringComparer.Ordinal);
        string fullRoot = Path.GetFullPath(root);

        foreach (string path in new[] { config.Artefacts.Report, config.Artefacts.Manifest, config.Artefacts.HumanReview })
        {
            string full = Path.Combine(fullRoot, path.Replace('/', Path.DirectorySeparatorChar));
            CheckWritable(fullRoot, path, full);

            if (!File.Exists(full))
            {
                artefacts[path] = new ComponentArtefact(path, full, null, "\n");
                continue;
            }

            if (!AssuranceSourceText.TryRead(full, out AssuranceSourceText? text, out string? problem))
            {
                problems.Add(new AssuranceViolation("IO", path, null, $"{path} {problem}"));
                artefacts[path] = new ComponentArtefact(path, full, null, "\n");
                continue;
            }

            string newLine = new AssuranceLines(text!.Text).NewLine;
            artefacts[path] = new ComponentArtefact(path, full, text, newLine);
            current[path] = newLine == "\r\n"
                ? text.Text.Replace("\r\n", "\n", StringComparison.Ordinal)
                : text.Text;
        }

        var corpus = new AssuranceCorpus(
            files,
            [.. set.Excluded.Select(static excluded => new AssuranceExcludedSource(excluded.RelativePath, excluded.Reason))],
            [.. set.Projects.Select(static project => project.AssemblyName).Distinct(StringComparer.Ordinal)],
            current)
        {
            SeparateRecords = SeparateRecords(fullRoot, config.Artefacts),
        };

        return new ComponentCorpus(corpus, set, sources, artefacts, problems);
    }

    private static void CheckWritable(string root, string relative, string full)
    {
        string? directory = Path.GetDirectoryName(relative.Replace('/', Path.DirectorySeparatorChar))?.Replace('\\', '/');
        if (directory is { Length: > 0 } && ComponentSources.Unenterable(root, directory) is { } barrier)
        {
            throw new ComponentSourceException(
                $"{AssuranceComponentConfig.FileName}: the artefact '{relative}' lies inside '{barrier.Directory}', which " +
                $"{barrier.What}; the generator writes nothing there");
        }

        if (ComponentSources.IsLink(full))
        {
            throw new ComponentSourceException(
                $"{AssuranceComponentConfig.FileName}: the artefact '{relative}' is a symbolic link; the generator " +
                "writes nothing through a link");
        }
    }

    private static IReadOnlyList<string> SeparateRecords(string root, AssuranceArtefactPaths paths)
    {
        var configured = new[] { paths.Report, paths.HumanReview, paths.Manifest };
        var records = new List<string>();

        foreach (string path in new[] { new AssuranceArtefactPaths().HumanReview, new AssuranceArtefactPaths().Report })
        {
            if (configured.Contains(path, StringComparer.OrdinalIgnoreCase))
                continue;

            string full = Path.Combine(root, path);
            if (File.Exists(full) && AssuranceSourceText.TryRead(full, out AssuranceSourceText? text, out _) &&
                !AssuranceGenerator.IsGenerated(text!.Text))
            {
                records.Add(path);
            }
        }

        return records;
    }

    public static IReadOnlySet<string> AdrRecords(string root, string directory)
    {
        string full = Path.Combine(Path.GetFullPath(root), directory.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(full))
            return new HashSet<string>(StringComparer.Ordinal);

        return Directory.EnumerateFiles(full, "*.md")
            .Select(static path => Path.GetFileName(path).Split('-')[0])
            .ToHashSet(StringComparer.Ordinal);
    }

    public bool TryWrite(AssuranceArtefact artefact, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(artefact);

        if (artefact.Kind == AssuranceArtefactKind.Source)
        {
            (ComponentSourceFile file, AssuranceSourceText text) = Sources[artefact.RelativePath];
            return text.TryWrite(file.FullPath, artefact.Desired, out problem);
        }

        ComponentArtefact target = Artefacts[artefact.RelativePath];
        if (target.Source is not null)
            return target.Source.TryWrite(target.FullPath, target.Restyle(artefact.Desired), out problem);

        problem = null;
        try
        {
            if (File.Exists(target.FullPath))
            {
                problem = "appeared on disk while the generator ran; nothing was written to it";
                return false;
            }

            string? directory = Path.GetDirectoryName(target.FullPath);
            if (directory is { Length: > 0 })
                Directory.CreateDirectory(directory);

            File.WriteAllBytes(target.FullPath, AssuranceSourceText.Encode(target.Restyle(artefact.Desired), false));
            return true;
        }
        catch (IOException exception)
        {
            problem = string.Create(CultureInfo.InvariantCulture, $"could not be written: {exception.Message}");
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            problem = string.Create(CultureInfo.InvariantCulture, $"could not be written: {exception.Message}");
            return false;
        }
    }
}
