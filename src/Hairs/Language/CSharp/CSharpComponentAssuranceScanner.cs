// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0
//
// HAIRS Code Assurance
// ----------------------
// Relevant units:   10
// Annotated:        10/10
// Exempt:           4
// Human-reviewed:   0/10
// IP risk:          Low
// Security risk:    High
// Criteria:         9/9
// Resource impact:  6/10 max
// Unverified:       10
//
// GENERATED - DO NOT EDIT MANUALLY

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Hairs.Assurance;

namespace Hairs.Language.CSharp;

/// <summary>
/// The editor's scanner for the files under one granted directory: each file
/// is scanned as the component it belongs to configures it, so the review pane
/// shows a unit as exempt, or as relevant, exactly where that component's own
/// command-line runs do.
///
/// A file belongs to the component whose <c>assurance.config.json</c> is the
/// nearest one in the file's directory or above it, up to the root and never
/// past it: the root is what the user granted, and nothing outside it is read.
/// That configuration's preprocessor symbols, exemption predicate and named
/// values decide the scan. A file under no configuration, or under one that
/// cannot be read, is scanned with the owning component's defaults, as the
/// parameterless <see cref="CSharpAssuranceScanner"/> scans it: the pane only
/// reads, and a configuration the command-line tool would refuse is that
/// tool's to report.
/// </summary>
// Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=557605
// Hairs-Falsified-If: an assurance.config.json outside the granted root, reached through a '..' segment, a link below the root or a sibling directory whose name begins with the root's, decides how a file is scanned
// Hairs-Human:        PENDING
public sealed class CSharpComponentAssuranceScanner : IAssuranceUnitScanner
{
    /// <summary>A configuration larger than this is not read; one is a few hundred bytes.</summary>
    // Hairs-AI:           Origin=AI; IP=None; Security=High; Resources=0; Fingerprint=6152DB
    // Hairs-Falsified-If: a configuration file of several megabytes is read and parsed on the thread that rescans the open file
    // Hairs-Human:        PENDING
    private const long MaxConfigurationBytes = 1024 * 1024;

    // Hairs-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=767317
    // Hairs-Human:        PENDING
    private static readonly CSharpAssuranceScanner Default = new();

    private readonly string _root;
    private readonly string _rootPrefix;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, (DateTime Written, long Length, CSharpAssuranceScanner Scanner)> _configured =
        new(StringComparer.Ordinal);

    /// <param name="root">The granted directory the paths given to <see cref="Scan"/> are relative to.</param>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=1; Fingerprint=6F6F0F
    // Hairs-Falsified-If: the root prefix is built without a trailing separator, so a sibling directory such as work2 beside the root work counts as inside it
    // Hairs-Human:        PENDING
    public CSharpComponentAssuranceScanner(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        _rootPrefix = Path.EndsInDirectorySeparator(_root) ? _root : _root + Path.DirectorySeparatorChar;
    }

    /// <inheritdoc/>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=6236D4
    // Hairs-Falsified-If: a file under a configuration that watches named values is scanned with the default scanner, so its named values are listed as relevant units with no block
    // Hairs-Human:        PENDING
    public IReadOnlyList<AssuranceScannedUnit> Scan(string text, string path)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(path);

        return ScannerFor(path).Scan(text, path);
    }

    /// <summary>
    /// The configuration file that governs <paramref name="path"/>, relative
    /// to the root, or null when none does.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=C93A51
    // Hairs-Falsified-If: a relative path whose '..' segments leave the root, or whose directory below the root is a link, yields a configuration file outside the root
    // Hairs-Human:        PENDING
    public string? ConfigurationFor(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (path.Length == 0 || Path.IsPathRooted(path))
            return null;

        string file;
        try
        {
            file = Path.GetFullPath(Path.Combine(_root, path));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        if (!IsUnderRoot(file))
            return null;

        // Upwards from the file's own directory, and the root is the last one
        // looked in. A directory below the root that is a link is not entered,
        // as the workspace's storage does not follow one either.
        for (string? directory = Path.GetDirectoryName(file);
             directory is not null && (IsUnderRoot(directory) || IsRoot(directory));
             directory = IsRoot(directory) ? null : Path.GetDirectoryName(directory))
        {
            if (!IsRoot(directory) && IsLink(new DirectoryInfo(directory)))
                return null;

            string candidate = Path.Combine(directory, AssuranceComponentConfig.FileName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    /// <summary>The scanner the configuration governing <paramref name="path"/> asks for.</summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=4; Fingerprint=ECF614
    // Hairs-Falsified-If: a configuration file that is a link, or one larger than MaxConfigurationBytes, is read and parsed
    // Hairs-Human:        PENDING
    private CSharpAssuranceScanner ScannerFor(string path)
    {
        if (ConfigurationFor(path) is not { } configuration)
            return Default;

        var file = new FileInfo(configuration);
        if (!file.Exists || IsLink(file) || file.Length > MaxConfigurationBytes)
            return Default;

        // Read again only when the file changed, so a configuration edited in
        // the editor itself takes effect on the next scan.
        lock (_gate)
        {
            if (_configured.TryGetValue(configuration, out var cached) &&
                cached.Written == file.LastWriteTimeUtc &&
                cached.Length == file.Length)
            {
                return cached.Scanner;
            }
        }

        CSharpAssuranceScanner scanner;
        try
        {
            AssuranceComponentConfig config = AssuranceComponentConfig.Parse(File.ReadAllText(configuration));
            scanner = new CSharpAssuranceScanner(config.PreprocessorSymbols, config.ExemptionPredicate, config.NamedValues);
        }
        catch (Exception exception) when (exception is AssuranceConfigException or IOException or UnauthorizedAccessException)
        {
            scanner = Default;
        }

        lock (_gate)
            _configured[configuration] = (file.LastWriteTimeUtc, file.Length, scanner);

        return scanner;
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=1; Fingerprint=D160B2
    // Hairs-Falsified-If: a directory above the root is answered as the root, so the walk reads the configuration there
    // Hairs-Human:        PENDING
    private bool IsRoot(string directory) =>
        string.Equals(Path.TrimEndingDirectorySeparator(directory), _root, StringComparison.Ordinal);

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=1; Fingerprint=FE85CC
    // Hairs-Falsified-If: a path in a sibling directory whose name begins with the root's, such as work2 beside the root work, is answered as under the root
    // Hairs-Human:        PENDING
    private bool IsUnderRoot(string path) =>
        path.StartsWith(_rootPrefix, StringComparison.Ordinal) && path.Length > _rootPrefix.Length;

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=0; Fingerprint=AD21F3
    // Hairs-Falsified-If: a directory junction or a symbolic link is answered false, so the walk follows it out of the root
    // Hairs-Human:        PENDING
    private static bool IsLink(FileSystemInfo entry) =>
        entry.Exists && (entry.Attributes & FileAttributes.ReparsePoint) != 0;
}
