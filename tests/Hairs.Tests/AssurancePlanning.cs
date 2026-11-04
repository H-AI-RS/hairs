// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using Hairs.Assurance;
using Hairs.Language.CSharp;

namespace Hairs.Tests;

/// <summary>
/// Plans over files held in memory, for tests of the generator and the rules.
/// </summary>
internal static class AssurancePlanning
{
    public static readonly CSharpAssuranceFileScanner Scanner = new(null, AssuranceExemptionPredicate.Strict);

    public static IAssuranceFileScanner ScannerFor(AssuranceComponentConfig config) =>
        config.PreprocessorSymbols is null && config.ExemptionPredicate == AssuranceExemptionPredicate.Strict &&
        config.NamedValues == AssuranceNamedValues.Reviewed
            ? Scanner
            : new CSharpAssuranceFileScanner(config.PreprocessorSymbols, config.ExemptionPredicate, config.NamedValues);

    public const string Copyright = "2026 HAIRS contributors";

    public static AssuranceComponentConfig Config(
        AssuranceForgeryVocabulary vocabulary = AssuranceForgeryVocabulary.Narrow,
        bool forbidDirectives = false,
        IReadOnlyList<string>? closed = null,
        IReadOnlyList<AssuranceSpdxOverride>? overrides = null) => new()
        {
            Component = "Probe",
            Projects = ["src/Probe/Probe.csproj"],
            Spdx = new AssuranceSpdx([Copyright], "Apache-2.0"),
            SpdxOverrides = overrides ?? [],
            ForgeryVocabulary = vocabulary,
            ForbidDirectives = forbidDirectives,
            ClosedToEscapeHatch = closed ?? [],
        };

    public static AssuranceCorpus Corpus(
        IReadOnlyDictionary<string, string>? artefacts, params (string Path, string Text)[] files) =>
        new(
            [.. files.Select(static file => new AssuranceSource(file.Path, "Probe", file.Text))],
            [],
            ["Probe"],
            artefacts ?? new Dictionary<string, string>());

    public static AssurancePlan Plan(AssuranceComponentConfig config, params (string Path, string Text)[] files) =>
        AssuranceGenerator.Plan(Corpus(null, files), ScannerFor(config), config);

    public static AssurancePlan Plan(params (string Path, string Text)[] files) => Plan(Config(), files);

    public static AssurancePlan Replan(AssurancePlan first, AssuranceComponentConfig config) =>
        AssuranceGenerator.Plan(
            new AssuranceCorpus(
                [.. first.Files.Select(static file => file.Source with { Text = file.Desired })],
                [],
                ["Probe"],
                first.Artefacts
                    .Where(static artefact => artefact.Kind != AssuranceArtefactKind.Source)
                    .ToDictionary(static artefact => artefact.RelativePath, static artefact => artefact.Desired)),
            ScannerFor(config),
            config);

    public static string Desired(AssurancePlan plan, string path) =>
        plan.Artefacts.Single(artefact => artefact.RelativePath == path).Desired;

    public static string Fingerprint(string text, string nameEnding) =>
        Scanner.ScanFile(text, "x.cs").Units.Single(unit => unit.Unit.Name.EndsWith(nameEnding, StringComparison.Ordinal)).Unit.Fingerprint;

    public static AssuranceCorpusUnit After(AssurancePlan plan, string nameEnding) =>
        plan.UnitsAfter.Single(unit => unit.Name.EndsWith(nameEnding, StringComparison.Ordinal));

    public static string LineWith(string text, string marker)
    {
        var lines = new AssuranceLines(text);
        return Enumerable.Range(0, lines.Count)
            .Select(line => lines[line])
            .Single(line => line.TrimStart().StartsWith(marker, StringComparison.Ordinal));
    }

    public static string Header(
        int relevant,
        int annotated,
        int exempt,
        int verified,
        string ip,
        string security,
        string criteria,
        string resources,
        int unverified,
        string newLine = "\n",
        IReadOnlyList<string>? spdx = null)
    {
        string[] lines =
        [
            .. spdx ?? [$"// SPDX-FileCopyrightText: {Copyright}", "// SPDX-License-Identifier: Apache-2.0"],
            "//",
            "// HAIRS Code Assurance",
            "// --------------------",
            $"// Relevant units:   {relevant}",
            $"// Annotated:        {annotated}/{relevant}",
            $"// Exempt:           {exempt}",
            $"// Human-reviewed:   {verified}/{relevant}",
            $"// IP risk:          {ip}",
            $"// Security risk:    {security}",
            $"// Criteria:         {criteria}",
            $"// Resource impact:  {resources}",
            $"// Unverified:       {unverified}",
            "//",
            "// GENERATED - DO NOT EDIT MANUALLY",
            string.Empty,
        ];

        return string.Concat(lines.Select(line => line + newLine));
    }
}
