// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Hairs.Assurance;

namespace Hairs.Cli.Assurance;

/// <summary>
/// <c>generate</c>, <c>check</c> and <c>status</c>: the generator, the gate
/// that compares the tree with it, and a summary.
/// </summary>
internal static partial class AssuranceCommand
{
    private static int Generate(Options options, TextWriter output, TextWriter error)
    {
        string root = RootOf(options);
        AssuranceComponentConfig config = OwnedConfig(root, "generate");
        RequireSpdx(config);

        ComponentCorpus loaded = ComponentCorpus.Load(root, config);
        AssurancePlan plan = AssuranceGenerator.Plan(loaded.Corpus, ScannerFor(config), config);
        string component = ComponentName(root, config);

        List<AssuranceViolation> refusals = [.. loaded.Problems, .. plan.Problems];
        List<AssuranceArtefact> changes = [.. plan.Changes];

        List<AssuranceArtefact> handWritten = options.Has("--adopt")
            ? []
            : [.. changes.Where(static artefact =>
                artefact.Kind != AssuranceArtefactKind.Source &&
                artefact.Exists &&
                artefact.Current.Length > 0 &&
                !AssuranceGenerator.IsGenerated(artefact.Current))];

        if (refusals.Count > 0)
        {
            error.WriteLine($"hairs assurance: the generator refused {Plural(refusals.Count, "time")}:");
            foreach (AssuranceViolation refusal in refusals)
                error.WriteLine($"  {refusal.Rule} {Indent(refusal.Message)}");
        }

        foreach (AssuranceArtefact artefact in handWritten)
        {
            error.WriteLine(
                $"hairs assurance: {artefact.RelativePath} exists and was not written by the generator " +
                $"(it carries no '{AssuranceGenerator.GeneratedNotice}' line). Point " +
                $"\"artefacts.{AssuranceGenerator.ConfigKey(artefact.Kind)}\" in {AssuranceComponentConfig.FileName} " +
                "at another file to keep it, or pass --adopt to replace it.");
        }

        if (refusals.Count > 0 || handWritten.Count > 0)
        {
            error.WriteLine("hairs assurance: nothing was written.");
            return Refused;
        }

        bool dryRun = options.Has("--dry-run");
        int sources = changes.Count(static artefact => artefact.Kind == AssuranceArtefactKind.Source);
        output.WriteLine(Invariant(
            $"{component}: {plan.Files.Count} covered files; {changes.Count} artefacts ") +
            (dryRun ? "would change" : "to write") +
            Invariant($" ({sources} source files, {changes.Count - sources} component artefacts)"));

        if (dryRun)
        {
            foreach (AssuranceArtefact artefact in changes)
                output.WriteLine($"  would write {artefact.RelativePath}{(artefact.Exists ? string.Empty : " (new)")}");

            output.WriteLine("Dry run: nothing was written.");
            return Done;
        }

        var failed = new List<string>();
        foreach (AssuranceArtefact artefact in changes.OrderBy(static artefact => artefact.Kind))
        {
            if (loaded.TryWrite(artefact, out string? problem))
            {
                output.WriteLine($"  wrote {artefact.RelativePath}");
                continue;
            }

            failed.Add($"{artefact.RelativePath} {problem}");
        }

        foreach (string failure in failed)
            error.WriteLine($"hairs assurance: {failure}");

        output.WriteLine(Invariant($"{changes.Count - failed.Count} written, {failed.Count} failed."));
        return failed.Count == 0 ? Done : Refused;
    }

    private static int Check(Options options, TextWriter output, TextWriter error)
    {
        string root = RootOf(options);
        string? jsonPath = options.Value("--json");
        PrepareOut(jsonPath);

        string? prefix = options.Value("--annotation-prefix");
        int? limit = null;
        if (options.Value("--annotation-limit") is { } text)
        {
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value))
                throw new UsageException($"--annotation-limit: '{text}' is not a whole number");

            limit = value;
        }

        AssuranceComponentConfig config = ReadableConfig(root, options, "check");
        RequireSpdx(config);

        ComponentCorpus loaded = ComponentCorpus.Load(root, config);
        AssurancePlan plan = AssuranceGenerator.Plan(loaded.Corpus, ScannerFor(config), config);

        bool release = options.Has("--release");
        bool sourcesOnly = options.Has("--sources-only");
        var checkOptions = new AssuranceCheckOptions(
            release, sourcesOnly, ComponentCorpus.AdrRecords(root, config.AdrDirectory));

        List<AssuranceViolation> violations = [.. loaded.Problems, .. AssuranceChecks.Run(plan, config, checkOptions)];
        string component = ComponentName(root, config);

        TextWriter annotations = jsonPath == "-" ? error : output;
        var shown = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (AssuranceViolation violation in violations)
        {
            int count = shown.GetValueOrDefault(violation.Rule) + 1;
            shown[violation.Rule] = count;
            if (limit is null || count <= limit)
                annotations.WriteLine(WorkflowCommand(violation, prefix));
        }

        foreach ((string rule, int count) in shown.OrderBy(static pair => RuleOrder(pair.Key)))
        {
            if (limit is { } most && count > most)
                annotations.WriteLine(Invariant($"{component}: {count - most} further {rule} violations are not shown as annotations (--annotation-limit {most})."));
        }

        annotations.WriteLine(Summary(component, violations, release, sourcesOnly));

        if (jsonPath is not null &&
            !WriteOut(jsonPath, AssuranceJson.CheckReport(component, release, sourcesOnly, violations), output, error))
        {
            return Refused;
        }

        return violations.Count == 0 ? Done : Refused;
    }

    private static int Status(Options options, TextWriter output, TextWriter error)
    {
        string root = RootOf(options);
        AssuranceComponentConfig config = ReadableConfig(root, options, "status");
        RequireSpdx(config);

        ComponentCorpus loaded = ComponentCorpus.Load(root, config);
        AssurancePlan plan = AssuranceGenerator.Plan(loaded.Corpus, ScannerFor(config), config);
        IReadOnlyList<AssuranceCorpusUnit> units = plan.UnitsBefore;
        AssuranceSummary summary = AssuranceSummary.Of(units);

        AssuranceAnnotation[] assessed = [.. units
            .Where(static unit => unit.IsRelevant && unit.Annotation is { ExemptReason: null })
            .Select(static unit => unit.Annotation!)];

        int pending = assessed.Count(static annotation => annotation.HumanIsPending);
        int stale = units.Count(static unit => unit.IsRelevant && unit.State == AssuranceUnitState.Stale);

        output.WriteLine(
            Invariant($"{ComponentName(root, config)}: {plan.Files.Count} covered files ({loaded.Set.Excluded.Count} not covered), ") +
            Invariant($"{units.Count} code units, {summary.Relevant} relevant, {summary.Exempt} exempt"));
        output.WriteLine($"  Annotated:       {Portion(summary.Annotated, summary.Relevant)}");
        output.WriteLine($"  Human-reviewed:  {Portion(summary.Verified, summary.Relevant)}");
        output.WriteLine(Invariant($"  Human line:      {pending} PENDING, {stale} STALE"));
        output.WriteLine("  States:          " + string.Join(", ", AssuranceVocabulary.States.Select(state =>
            Invariant($"{AssuranceStateMachine.Name(state)} {units.Count(unit => unit.State == state)}"))));
        output.WriteLine("  Security:        " + string.Join(", ", AssuranceVocabulary.SecurityRiskValues.Select(value =>
            Invariant($"{value} {assessed.Count(annotation => annotation.Field("Security") == value)}"))) +
            Invariant($", not assessed {summary.Relevant - summary.Annotated}"));

        int changes = plan.Changes.Count();
        int refusals = loaded.Problems.Count + plan.Problems.Count;
        output.WriteLine(refusals > 0
            ? Invariant($"  Generate:        refuses ({refusals} problems; 'check' names them)")
            : changes == 0
                ? "  Generate:        nothing to write"
                : Invariant($"  Generate:        would write {changes} artefacts"));

        return Done;
    }

    private static AssuranceComponentConfig OwnedConfig(string root, string command)
    {
        AssuranceComponentConfig config = LoadConfig(root)
            ?? throw new AssuranceConfigException(
                $"{Path.Combine(root, AssuranceComponentConfig.FileName)} does not exist. A component opts in " +
                $"to written annotations by committing that file; {command} writes nothing without it.");

        if (config.Mode == AssuranceMode.External)
        {
            throw new AssuranceConfigException(
                $"{AssuranceComponentConfig.FileName} says \"mode\": \"external\": another tool writes this " +
                "component's annotations, and this one only reads them.");
        }

        return config;
    }

    private static AssuranceComponentConfig ReadableConfig(string root, Options options, string command)
    {
        if (options.Value("--config") is { } path)
        {
            if (!File.Exists(path))
                throw new UsageException($"--config: '{path}' does not exist");

            return Named(AssuranceComponentConfig.Parse(File.ReadAllText(path)), root);
        }

        return LoadConfig(root)
            ?? throw new AssuranceConfigException(
                $"{Path.Combine(root, AssuranceComponentConfig.FileName)} does not exist; {command} needs it, " +
                "or --config naming one.");
    }

    private static void RequireSpdx(AssuranceComponentConfig config)
    {
        if (config.Spdx is null)
        {
            throw new AssuranceConfigException(
                $"{AssuranceComponentConfig.FileName}: $.spdx is required to generate or check headers: " +
                "{ \"copyright\": [ \"<year> <holder>\" ], \"license\": \"<SPDX expression>\" }");
        }
    }

    internal static string WorkflowCommand(AssuranceViolation violation, string? prefix = null)
    {
        var properties = new List<string>();
        if (violation.File is { } file)
        {
            string located = prefix is { Length: > 0 } ? prefix.Replace('\\', '/').TrimEnd('/') + "/" + file : file;
            properties.Add("file=" + EscapeProperty(located));
        }

        if (violation.Line is { } line)
            properties.Add("line=" + line.ToString(CultureInfo.InvariantCulture));

        string remedy = violation.Remedy is null ? string.Empty : $"\n  Run: {violation.Remedy}";
        string head = properties.Count == 0 ? "::error" : "::error " + string.Join(',', properties);
        return $"{head}::{EscapeData(violation.Rule + " " + violation.Message + remedy)}";
    }

    private static string EscapeData(string value) => value
        .Replace("%", "%25", StringComparison.Ordinal)
        .Replace("\r", "%0D", StringComparison.Ordinal)
        .Replace("\n", "%0A", StringComparison.Ordinal);

    private static string EscapeProperty(string value) => EscapeData(value)
        .Replace(":", "%3A", StringComparison.Ordinal)
        .Replace(",", "%2C", StringComparison.Ordinal);

    private static string Summary(
        string component, IReadOnlyList<AssuranceViolation> violations, bool release, bool sourcesOnly)
    {
        string mode = (release, sourcesOnly) switch
        {
            (true, true) => " (release, sources only)",
            (true, false) => " (release)",
            (false, true) => " (sources only)",
            _ => string.Empty,
        };

        if (violations.Count == 0)
            return $"{component}: no violations{mode}.";

        var counts = new StringBuilder();
        foreach (IGrouping<string, AssuranceViolation> rule in violations
            .GroupBy(static violation => violation.Rule, StringComparer.Ordinal)
            .OrderBy(static group => RuleOrder(group.Key)))
        {
            if (counts.Length > 0)
                counts.Append(", ");

            counts.Append(Invariant($"{rule.Key} {rule.Count()}"));
        }

        return Invariant($"{component}: {Plural(violations.Count, "violation")}{mode}: {counts}.");
    }

    private static int RuleOrder(string rule) =>
        rule.Length > 1 && rule[0] == 'J' &&
        int.TryParse(rule.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int number)
            ? number
            : 0;

    private static string Portion(int part, int whole) => whole == 0
        ? part.ToString(CultureInfo.InvariantCulture)
        : Invariant($"{part} of {whole} ({(int)Math.Round(100.0 * part / whole)}%)");

    private static string Plural(int count, string noun) =>
        Invariant($"{count} {noun}{(count == 1 ? string.Empty : "s")}");

    private static string Indent(string message) => message.Replace("\n", "\n    ", StringComparison.Ordinal);
}
