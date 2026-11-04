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

/// <summary>
/// <c>hairs assurance</c>: the per-declaration assurance annotations of one component.
/// </summary>
internal static partial class AssuranceCommand
{
    public const int Done = 0;
    public const int Refused = 1;
    public const int UsageError = 2;

    private static readonly string[] ListOptions = ["--root", "--files", "--json"];
    private static readonly string[] ListFlags = ["--all-units", "--strict"];
    private static readonly string[] InsertOptions = ["--root", "--assessments", "--json"];
    private static readonly string[] InsertFlags = ["--dry-run"];
    private static readonly string[] GenerateOptions = ["--root"];
    private static readonly string[] GenerateFlags = ["--dry-run", "--adopt"];
    private static readonly string[] CheckOptions = ["--root", "--config", "--json", "--annotation-prefix", "--annotation-limit"];
    private static readonly string[] CheckFlags = ["--release", "--sources-only"];
    private static readonly string[] StatusOptions = ["--root", "--config"];
    private static readonly string[] PruneOptions = ["--root", "--json"];
    private static readonly string[] PruneFlags = ["--dry-run"];

    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Count == 0)
        {
            error.Write(Usage);
            return UsageError;
        }

        if (args[0] is "-h" or "--help" or "help")
        {
            output.Write(Usage);
            return Done;
        }

        try
        {
            return args[0] switch
            {
                "list" => List(Parse(args, ListOptions, ListFlags), output, error),
                "insert" => Insert(Parse(args, InsertOptions, InsertFlags), output, error),
                "generate" => Generate(Parse(args, GenerateOptions, GenerateFlags), output, error),
                "check" => Check(Parse(args, CheckOptions, CheckFlags), output, error),
                "status" => Status(Parse(args, StatusOptions, []), output, error),
                "prune" => Prune(Parse(args, PruneOptions, PruneFlags), output, error),
                _ => throw new UsageException($"unknown assurance command '{args[0]}'"),
            };
        }
        catch (UsageException exception)
        {
            error.WriteLine($"hairs assurance: {exception.Message}");
            error.Write(Usage);
            return UsageError;
        }
        catch (Exception exception) when (exception is AssuranceConfigException or ComponentSourceException or AssuranceInputException)
        {
            error.WriteLine($"hairs assurance: {exception.Message}");
            return UsageError;
        }
    }

    internal static CSharpAssuranceFileScanner ScannerFor(AssuranceComponentConfig? config) =>
        config is null
            ? new CSharpAssuranceFileScanner(null, AssuranceExemptionPredicate.Strict)
            : new CSharpAssuranceFileScanner(config.PreprocessorSymbols, config.ExemptionPredicate, config.NamedValues);

    private static int List(Options options, TextWriter output, TextWriter error)
    {
        string root = RootOf(options);
        string? jsonPath = options.Value("--json");
        PrepareOut(jsonPath);

        AssuranceComponentConfig? config = LoadConfig(root);
        ComponentSourceSet set = ComponentSources.Discover(root, config);
        var notes = new List<string>(set.Notes);
        IReadOnlyList<ComponentUnknownPath>? unknown = null;

        if (options.Value("--files") is { } listFile)
        {
            if (!File.Exists(listFile))
                throw new UsageException($"--files: '{listFile}' does not exist");

            set = ComponentSources.Restrict(set, root, File.ReadAllLines(listFile), out unknown);
            foreach (ComponentUnknownPath path in unknown)
                notes.Add($"--files names '{path.Path}', which {path.Reason}");
        }

        CSharpAssuranceFileScanner scanner = ScannerFor(config);
        var files = new List<ListedFile>(set.Files.Count);
        foreach (ComponentSourceFile file in set.Files)
        {
            if (!AssuranceSourceText.TryRead(file.FullPath, out AssuranceSourceText? source, out string? problem))
            {
                files.Add(new ListedFile(file, [], $"the file {problem}"));
                continue;
            }

            AssuranceScannedFile scan = scanner.ScanFile(source!.Text, file.RelativePath);
            files.Add(new ListedFile(file, AssuranceCandidates.Classify(new AssuranceLines(source.Text), scan), null));
        }

        string component = ComponentName(root, config);
        bool allUnits = options.Has("--all-units");

        foreach (string note in notes)
            error.WriteLine($"hairs assurance: {note}");

        bool unreadable = files.Any(static file => file.Problem is not null);
        bool strayed = options.Has("--strict") && unknown is { Count: > 0 };
        int exit = unreadable || strayed ? Refused : Done;

        if (jsonPath is not null)
            return WriteOut(jsonPath, AssuranceJson.List(component, set, files, allUnits, notes, unknown ?? []), output, error) ? exit : Refused;

        WriteListText(component, set, files, allUnits, output);
        return exit;
    }

    private static void WriteListText(
        string component, ComponentSourceSet set, IReadOnlyList<ListedFile> files, bool allUnits, TextWriter output)
    {
        ListTotals totals = ListTotals.Of(files, allUnits);
        output.WriteLine(
            Invariant($"{component}: {totals.Files} files, {totals.Units} units, {totals.Relevant} relevant, ") +
            Invariant($"{totals.Annotated} annotated, {totals.Unannotated} unannotated, {totals.Insertable} insertable"));

        foreach (ListedFile file in files)
        {
            ListTotals counts = ListTotals.Of([file], allUnits);
            if (file.Problem is not null)
            {
                output.WriteLine($"{file.File.RelativePath}  unreadable: {file.Problem}");
                continue;
            }

            if (counts.Listed == 0)
                continue;

            output.WriteLine(
                Invariant($"{file.File.RelativePath}  relevant {counts.Relevant}  annotated {counts.Annotated}  ") +
                Invariant($"unannotated {counts.Unannotated}  insertable {counts.Insertable}"));

            foreach (AssuranceCandidate candidate in AssuranceJson.Listed(file, allUnits))
            {
                AssuranceScannedUnit unit = candidate.Unit;
                string where = Invariant($"{unit.DeclarationLine + 1}:{unit.DeclarationColumn + 1}");

                string status = candidate.Insertable
                    ? "insertable"
                    : candidate.Detail is null
                        ? candidate.Reason
                        : candidate.Detail.StartsWith(candidate.Reason, StringComparison.Ordinal)
                            ? candidate.Detail
                            : $"{candidate.Reason}: {candidate.Detail}";

                output.WriteLine(
                    $"  {where,-9} {unit.Kind,-12} {unit.Fingerprint}  {unit.Name}  [{AssuranceStateMachine.Name(candidate.State)}] {status}");
            }
        }

        foreach (ComponentExcludedFile excluded in set.Excluded)
            output.WriteLine($"{excluded.RelativePath}  not covered: {excluded.Reason}");
    }

    private static int Insert(Options options, TextWriter output, TextWriter error)
    {
        string root = RootOf(options);
        string assessmentsPath = options.Value("--assessments")
            ?? throw new UsageException("insert needs --assessments <file.json>");

        string? jsonPath = options.Value("--json");
        PrepareOut(jsonPath);

        AssuranceComponentConfig config = OwnedConfig(root, "insert");

        if (!File.Exists(assessmentsPath))
            throw new UsageException($"--assessments: '{assessmentsPath}' does not exist");

        AssessmentInput input = AssuranceJson.ReadAssessments(File.ReadAllText(assessmentsPath));
        ComponentSourceSet set = ComponentSources.Discover(root, config);
        bool dryRun = options.Has("--dry-run");

        var closed = new HashSet<string>(config.ClosedToEscapeHatch, StringComparer.Ordinal);
        CSharpAssuranceFileScanner scanner = ScannerFor(config);
        var results = new List<AssuranceInsertEntryResult>(input.Refused);

        var located = new List<(AssuranceAssessment Entry, ComponentSourceFile? File, string? Why)>();
        foreach (AssuranceAssessment entry in input.Entries)
        {
            ComponentSourceFile? file = ComponentSources.Find(set, root, entry.File, out ComponentUnknownPath? unknown);
            located.Add(file is null
                ? (entry, null, unknown!.Reason)
                : (entry with { File = file.RelativePath }, file, null));
        }

        foreach ((AssuranceAssessment entry, _, string? why) in located.Where(static entry => entry.File is null))
            results.Add(new AssuranceInsertEntryResult(entry, false, $"the file {why}"));

        foreach (IGrouping<ComponentSourceFile, (AssuranceAssessment Entry, ComponentSourceFile? File, string? Why)> group in located
            .Where(static entry => entry.File is not null)
            .GroupBy(static entry => entry.File!)
            .OrderBy(static group => group.Key.RelativePath, StringComparer.Ordinal))
        {
            ComponentSourceFile file = group.Key;
            List<AssuranceAssessment> entries = [.. group.Select(static entry => entry.Entry).OrderBy(static entry => entry.Index)];

            if (!AssuranceSourceText.TryRead(file.FullPath, out AssuranceSourceText? source, out string? problem))
            {
                results.AddRange(entries.Select(entry =>
                    new AssuranceInsertEntryResult(entry, false, $"the file {problem}")));
                continue;
            }

            AssuranceInsertFileResult applied = AssuranceInsertion.Apply(
                source!.Text,
                file.RelativePath,
                scanner,
                entries,
                closed.Contains(file.Project.AssemblyName),
                file.Project.AssemblyName);

            if (dryRun)
            {
                results.AddRange(applied.Entries.Select(static entry => entry.Applied
                    ? entry with { Message = entry.Message + " (dry run: not written)" }
                    : entry));
                continue;
            }

            if (applied.Changed && !source.TryWrite(file.FullPath, applied.Text, out string? writeProblem))
            {
                results.AddRange(applied.Entries.Select(entry => entry.Applied
                    ? entry with { Applied = false, Message = $"the file {writeProblem}", Line = null }
                    : entry));
                continue;
            }

            results.AddRange(applied.Entries);
        }

        results.Sort(static (left, right) => left.Entry.Index.CompareTo(right.Entry.Index));

        WriteInsertText(results, dryRun, jsonPath == "-" ? error : output);

        int exit = results.All(static result => result.Applied) ? Done : Refused;
        if (jsonPath is not null && !WriteOut(jsonPath, AssuranceJson.InsertReport(results, dryRun), output, error))
        {
            if (!dryRun && results.Any(static result => result.Applied))
                error.WriteLine("hairs assurance: the blocks reported above as inserted were written to the sources.");

            return Refused;
        }

        return exit;
    }

    private static void WriteInsertText(IReadOnlyList<AssuranceInsertEntryResult> results, bool dryRun, TextWriter output)
    {
        foreach (AssuranceInsertEntryResult result in results)
        {
            string number = result.Entry.Index.ToString(CultureInfo.InvariantCulture);
            if (result.Applied)
            {
                string verb = dryRun ? "would insert" : "inserted";
                output.WriteLine(Invariant(
                    $"#{number} {verb}  {result.Entry.File}:{result.Line}  {result.Entry.Unit}"));
            }
            else
            {
                output.WriteLine(
                    $"#{number} refused  {result.Entry.File}  {result.Entry.Unit}: {result.Message}");
            }
        }

        int applied = results.Count(static result => result.Applied);
        output.WriteLine(
            Invariant($"{applied} {(dryRun ? "would be applied" : "applied")}, {results.Count - applied} refused") +
            (dryRun ? " (dry run: nothing was written)" : string.Empty));
    }

    private static AssuranceComponentConfig? LoadConfig(string root)
    {
        string path = Path.Combine(root, AssuranceComponentConfig.FileName);
        if (File.Exists(path))
            return Named(AssuranceComponentConfig.Parse(File.ReadAllText(path)), root);

        string legacyPath = Path.Combine(root, "assurance.config.json");
        if (File.Exists(legacyPath))
            return Named(AssuranceComponentConfig.Parse(File.ReadAllText(legacyPath)), root);

        return null;
    }

    private static AssuranceComponentConfig Named(AssuranceComponentConfig config, string root) =>
        config.Component.Length > 0 ? config : config with { Component = ComponentName(root, null) };

    private static string ComponentName(string root, AssuranceComponentConfig? config) =>
        config is { Component.Length: > 0 }
            ? config.Component
            : Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)));

    private static string RootOf(Options options)
    {
        string root = options.Value("--root") ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(root))
            throw new UsageException($"--root: '{root}' is not a directory");

        return root;
    }

    private static void PrepareOut(string? path)
    {
        if (path is null or "-")
            return;

        try
        {
            string full = Path.GetFullPath(path);
            if (Directory.Exists(full))
                throw new UsageException($"--json: '{path}' is a directory");

            if (Path.GetDirectoryName(full) is { Length: > 0 } directory)
                Directory.CreateDirectory(directory);

            bool existed = File.Exists(full);
            using (new FileStream(full, FileMode.OpenOrCreate, FileAccess.Write))
            {
            }

            if (!existed)
                File.Delete(full);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new UsageException($"--json: '{path}' cannot be written: {exception.Message}");
        }
    }

    private static bool WriteOut(string path, string content, TextWriter output, TextWriter error)
    {
        if (path == "-")
        {
            output.Write(content);
            return true;
        }

        try
        {
            string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (directory is { Length: > 0 })
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, content);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            error.WriteLine($"hairs assurance: could not write {path}: {exception.Message}");
            return false;
        }

        error.WriteLine($"hairs assurance: wrote {path}");
        return true;
    }

    private static Options Parse(IReadOnlyList<string> args, string[] valued, string[] flags)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var set = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 1; index < args.Count; index++)
        {
            string name = args[index];
            if (valued.Contains(name, StringComparer.Ordinal))
            {
                if (index + 1 >= args.Count)
                    throw new UsageException($"{name} needs a value");

                if (!values.TryAdd(name, args[++index]))
                    throw new UsageException($"{name} is given twice");
            }
            else if (flags.Contains(name, StringComparer.Ordinal))
            {
                set.Add(name);
            }
            else
            {
                throw new UsageException($"'{name}' is not an option of 'assurance {args[0]}'");
            }
        }

        return new Options(values, set);
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    private const string Usage =
        """
        Usage:
          hairs assurance list   --root <dir> [--files <list>] [--strict] [--json <out>|-] [--all-units]
          hairs assurance insert --root <dir> --assessments <file.json> [--dry-run] [--json <out>|-]
          hairs assurance generate --root <dir> [--dry-run] [--adopt]
          hairs assurance check  --root <dir> [--config <file>] [--release] [--sources-only] [--json <out>|-]
                                 [--annotation-prefix <path>] [--annotation-limit <n>]
          hairs assurance status --root <dir> [--config <file>]
          hairs assurance prune  --root <dir> [--dry-run] [--json <out>|-]

        list    Relevant units that carry no annotation block, with the file, line, column, indent,
                qualified name, kind, fingerprint and extent a tool needs to write one, and whether
                one can be inserted there now (and if not, why). --all-units lists every unit.
                --files restricts the run to the paths in a file, one per line ('#' starts a
                comment): root-relative or absolute under --root, either slash, any case on Windows.
                A path that is not a covered file is reported (unknownFiles in JSON), and with
                --strict fails the run. A covered file that cannot be read always does.
        insert  Writes machine assessments above those units:
                  { "schema": 1, "assessments": [ { "file", "unit", "fingerprint", "origin",
                    "spec"?, "ip", "security", "resources", "cs"?, "cl"?, "falsifiedIf"? } ] }
                or { "file", "unit", "fingerprint"?, "exempt": "<reason>" } for the EXEMPT form.
                Each block records Fingerprint=TBF and a human line of PENDING. The input has no
                field for the human line. Needs hairs.config.json at the root.
        generate  Rewrites every covered file's generated header and annotation blocks (fills
                Fingerprint=TBF, moves an outrun decision to STALE) and the report, manifest and
                human-review record. Writes only what changed, keeping each file's byte-order
                mark and line endings. Needs hairs.config.json at the root, refuses in
                "mode": "external", and will not replace a report or record it did not write
                unless --adopt is given.
        check   Computes what generate would write and reports every difference, and every
                annotation rule the tree breaks, as ::error lines (J1-J7, J9, J10; J11 with
                --release). --sources-only compares only the covered files and the manifest's
                arrays, for a component whose own tooling owns the prose. --config reads the
                configuration from elsewhere, for a component that has none.
                --annotation-prefix puts a path before every file= (the component's directory,
                when the workflow runs from a parent); --annotation-limit caps the ::error lines
                per rule and counts the rest.
        status  A short summary of units, annotations and states.
        prune   Removes the annotation lines the rubric no longer asks for: the whole block above
                a unit the exemption predicate exempts (a named value, once "namedValues" is
                "watched"), and the Hairs-Falsified-If line of a block assessed Security=None,
                Low or Medium. Only where the human line reads exactly PENDING: a block naming a
                reviewer or reading STALE is left in place and reported. Whole lines only; each
                written file is scanned again and must keep every fingerprint. Run generate
                afterwards. Needs hairs.config.json at the root and refuses in external mode.

        Exit codes: 0 done, 1 something was refused, check found a violation, list could
        not read a covered file (or, with --strict, was named a path that is not one),
        prune left a block in place or could not read, verify or write a file, or a JSON
        report could not be written after the command's own writes; 2 usage or
        configuration error, before anything was written.

        """;

    private sealed record Options(IReadOnlyDictionary<string, string> Values, IReadOnlySet<string> Flags)
    {
        public string? Value(string name) => Values.TryGetValue(name, out string? value) ? value : null;
        public bool Has(string name) => Flags.Contains(name);
    }

    private sealed class UsageException : Exception
    {
        public UsageException(string message)
            : base(message)
        {
        }
    }
}
