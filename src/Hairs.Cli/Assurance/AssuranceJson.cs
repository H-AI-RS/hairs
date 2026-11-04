// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Hairs.Assurance;

namespace Hairs.Cli.Assurance;

/// <summary>Why an assessments file was refused as a whole.</summary>
internal sealed class AssuranceInputException : Exception
{
    public AssuranceInputException(string message)
        : base(message)
    {
    }
}

/// <summary>The entries of an assessments file, and those refused before any file was read.</summary>
internal sealed record AssessmentInput(
    IReadOnlyList<AssuranceAssessment> Entries,
    IReadOnlyList<AssuranceInsertEntryResult> Refused);

/// <summary>One covered file as <c>assurance list</c> reports it.</summary>
internal sealed record ListedFile(
    ComponentSourceFile File,
    IReadOnlyList<AssuranceCandidate> Candidates,
    string? Problem);

/// <summary>
/// The JSON the assurance commands read and write.
/// </summary>
internal static class AssuranceJson
{
    public const int Schema = 1;

    private static readonly string[] AssessmentFields =
        ["file", "unit", "fingerprint", "origin", "spec", "ip", "security", "resources", "cs", "cl", "CS", "CL", "falsifiedIf", "exempt"];

    /// <summary>
    /// Reads an assessments file:
    /// <c>{ "schema": 1, "assessments": [ { "file", "unit", "fingerprint", "origin",
    /// "spec"?, "ip", "security", "resources", "cs"?, "cl"?, "falsifiedIf"? } ] }</c>, or an
    /// entry of the form <c>{ "file", "unit", "fingerprint"?, "exempt": "reason" }</c>.
    /// </summary>
    public static AssessmentInput ReadAssessments(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
        }
        catch (JsonException exception)
        {
            throw new AssuranceInputException($"the assessments file is not valid JSON: {exception.Message}");
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new AssuranceInputException("the assessments file must be an object");

            JsonElement? assessments = null;
            bool schema = false;
            foreach (JsonProperty property in root.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "schema":
                        if (property.Value.ValueKind != JsonValueKind.Number ||
                            !property.Value.TryGetInt32(out int version) || version != Schema)
                        {
                            throw new AssuranceInputException($"\"schema\" must be {Schema}");
                        }

                        schema = true;
                        break;

                    case "assessments":
                        assessments = property.Value;
                        break;

                    case "$comment":
                        break;

                    default:
                        throw new AssuranceInputException(
                            $"\"{property.Name}\" is not a property of an assessments file");
                }
            }

            if (!schema)
                throw new AssuranceInputException($"the assessments file has no \"schema\": {Schema}");

            if (assessments is not { ValueKind: JsonValueKind.Array } array)
                throw new AssuranceInputException("the assessments file has no \"assessments\" array");

            var entries = new List<AssuranceAssessment>();
            var refused = new List<AssuranceInsertEntryResult>();
            int index = 0;

            foreach (JsonElement element in array.EnumerateArray())
            {
                (AssuranceAssessment entry, string? problem) = ReadEntry(element, index++);
                if (problem is null)
                    entries.Add(entry);
                else
                    refused.Add(new AssuranceInsertEntryResult(entry, false, problem));
            }

            return new AssessmentInput(entries, refused);
        }
    }

    private static (AssuranceAssessment Entry, string? Problem) ReadEntry(JsonElement element, int index)
    {
        var empty = new AssuranceAssessment { Index = index, File = string.Empty, Unit = string.Empty };
        if (element.ValueKind != JsonValueKind.Object)
            return (empty, "an entry must be an object");

        var strings = new Dictionary<string, string>(StringComparer.Ordinal);
        int? resources = null;
        int? cs = null;
        int? cl = null;
        var problems = new List<string>();

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!AssessmentFields.Contains(property.Name, StringComparer.Ordinal))
            {
                problems.Add(property.Name.Contains("human", StringComparison.OrdinalIgnoreCase)
                    ? $"carries \"{property.Name}\": an insert writes PENDING on the human line and nothing else, " +
                      "and only a human may change it"
                    : $"carries \"{property.Name}\", which this schema does not define");
                continue;
            }

            JsonElement value = property.Value;
            if (value.ValueKind == JsonValueKind.Null)
                continue;

            if (property.Name is "resources" or "cs" or "cl" or "CS" or "CL")
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double score) &&
                    score == Math.Floor(score) && score is >= int.MinValue and <= int.MaxValue)
                {
                    if (property.Name == "resources")
                        resources = (int)score;
                    else if (property.Name is "cs" or "CS")
                        cs = (int)score;
                    else
                        cl = (int)score;
                }
                else
                {
                    problems.Add($"\"{property.Name}\" must be an integer 0 to 10");
                }

                continue;
            }

            if (value.ValueKind != JsonValueKind.String)
            {
                problems.Add($"\"{property.Name}\" must be a string");
                continue;
            }

            strings[property.Name] = value.GetString()!;
        }

        var entry = new AssuranceAssessment
        {
            Index = index,
            File = strings.GetValueOrDefault("file", string.Empty).Trim().Replace('\\', '/'),
            Unit = strings.GetValueOrDefault("unit", string.Empty),
            Fingerprint = strings.GetValueOrDefault("fingerprint")?.Trim(),
            Origin = strings.GetValueOrDefault("origin")?.Trim(),
            Spec = strings.GetValueOrDefault("spec"),
            Ip = strings.GetValueOrDefault("ip")?.Trim(),
            Security = strings.GetValueOrDefault("security")?.Trim(),
            Resources = resources,
            Cs = cs,
            Cl = cl,
            FalsifiedIf = strings.GetValueOrDefault("falsifiedIf"),
            Exempt = strings.GetValueOrDefault("exempt"),
        };

        if (entry.File.Length == 0)
            problems.Insert(0, "has no \"file\"");

        if (entry.Unit.Length == 0)
            problems.Insert(0, "has no \"unit\"");

        return (entry, problems.Count == 0 ? null : string.Join("; ", problems));
    }

    public static string List(
        string component,
        ComponentSourceSet set,
        IReadOnlyList<ListedFile> files,
        bool allUnits,
        IReadOnlyList<string> notes,
        IReadOnlyList<ComponentUnknownPath> unknown)
    {
        return Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("schema", Schema);
            writer.WriteString("component", component);
            writer.WriteBoolean("allUnits", allUnits);

            ListTotals totals = ListTotals.Of(files, allUnits);
            writer.WriteStartObject("totals");
            totals.Write(writer);
            writer.WriteNumber("unknown", unknown.Count);
            writer.WriteEndObject();

            writer.WriteStartArray("unknownFiles");
            foreach (ComponentUnknownPath path in unknown)
            {
                writer.WriteStartObject();
                writer.WriteString("file", path.Path);
                writer.WriteString("reason", path.Reason);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WriteStartArray("files");
            foreach (ListedFile file in files)
            {
                writer.WriteStartObject();
                writer.WriteString("file", file.File.RelativePath);
                writer.WriteString("assembly", file.File.Project.AssemblyName);
                if (file.Problem is not null)
                    writer.WriteString("problem", file.Problem);

                ListTotals.Of([file], allUnits).Write(writer, includeFiles: false);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WriteStartArray("units");
            foreach (ListedFile file in files)
            {
                foreach (AssuranceCandidate candidate in Listed(file, allUnits))
                    WriteUnit(writer, file, candidate);
            }

            writer.WriteEndArray();

            writer.WriteStartArray("excluded");
            foreach (ComponentExcludedFile excluded in set.Excluded)
            {
                writer.WriteStartObject();
                writer.WriteString("file", excluded.RelativePath);
                writer.WriteString("reason", excluded.Reason);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WriteStartArray("notes");
            foreach (string note in notes)
                writer.WriteStringValue(note);

            writer.WriteEndArray();
            writer.WriteEndObject();
        });
    }

    public static IEnumerable<AssuranceCandidate> Listed(ListedFile file, bool allUnits) =>
        allUnits ? file.Candidates : file.Candidates.Where(static candidate => candidate.IsRelevant && !candidate.IsAnnotated);

    public static string InsertReport(IReadOnlyList<AssuranceInsertEntryResult> results, bool dryRun)
    {
        return Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("schema", Schema);
            writer.WriteBoolean("dryRun", dryRun);
            writer.WriteNumber("applied", results.Count(static result => result.Applied));
            writer.WriteNumber("refused", results.Count(static result => !result.Applied));
            writer.WriteStartArray("entries");
            foreach (AssuranceInsertEntryResult result in results.OrderBy(static result => result.Entry.Index))
            {
                writer.WriteStartObject();
                writer.WriteNumber("index", result.Entry.Index);
                writer.WriteString("file", result.Entry.File);
                writer.WriteString("unit", result.Entry.Unit);
                writer.WriteBoolean("applied", result.Applied);
                if (result.Line is { } line)
                    writer.WriteNumber("line", line);

                writer.WriteString("message", result.Message);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });
    }

    public static string PruneReport(string component, IReadOnlyList<PrunedFile> files, bool dryRun)
    {
        return Write(writer =>
        {
            AssurancePruneEntry[] entries = [.. files.SelectMany(static file => file.Entries)];

            writer.WriteStartObject();
            writer.WriteNumber("schema", Schema);
            writer.WriteString("component", component);
            writer.WriteBoolean("dryRun", dryRun);
            writer.WriteNumber("blocksRemoved", entries.Count(static entry => entry.Removed && entry.Kind == AssurancePruneKind.Block));
            writer.WriteNumber("criteriaRemoved", entries.Count(static entry => entry.Removed && entry.Kind == AssurancePruneKind.Criterion));
            writer.WriteNumber("left", entries.Count(static entry => !entry.Removed));
            writer.WriteNumber("problems", files.Count(static file => file.Problem is not null));

            writer.WriteStartArray("files");
            foreach (PrunedFile file in files)
            {
                writer.WriteStartObject();
                writer.WriteString("file", file.File);
                if (file.Problem is not null)
                    writer.WriteString("problem", file.Problem);

                writer.WriteStartArray("entries");
                foreach (AssurancePruneEntry entry in file.Entries)
                {
                    writer.WriteStartObject();
                    writer.WriteString("unit", entry.Unit);
                    writer.WriteNumber("line", entry.Line);
                    writer.WriteString("kind", AssuranceCommand.PruneKindName(entry.Kind));
                    writer.WriteBoolean("removed", entry.Removed);
                    writer.WriteString("reason", entry.Reason);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });
    }

    public static string CheckReport(
        string component, bool release, bool sourcesOnly, IReadOnlyList<AssuranceViolation> violations)
    {
        return Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("schema", Schema);
            writer.WriteString("component", component);
            writer.WriteBoolean("release", release);
            writer.WriteBoolean("sourcesOnly", sourcesOnly);
            writer.WriteNumber("violationCount", violations.Count);

            writer.WriteStartObject("rules");
            foreach (IGrouping<string, AssuranceViolation> rule in violations
                .GroupBy(static violation => violation.Rule, StringComparer.Ordinal)
                .OrderBy(static group => group.Key, StringComparer.Ordinal))
            {
                writer.WriteNumber(rule.Key, rule.Count());
            }

            writer.WriteEndObject();

            writer.WriteStartArray("violations");
            foreach (AssuranceViolation violation in violations)
            {
                writer.WriteStartObject();
                writer.WriteString("rule", violation.Rule);
                if (violation.File is not null)
                    writer.WriteString("file", violation.File);

                if (violation.Line is { } line)
                    writer.WriteNumber("line", line);

                writer.WriteString("message", violation.Message);
                if (violation.Remedy is not null)
                    writer.WriteString("remedy", violation.Remedy);

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });
    }

    private static void WriteUnit(Utf8JsonWriter writer, ListedFile file, AssuranceCandidate candidate)
    {
        AssuranceScannedUnit unit = candidate.Unit;
        writer.WriteStartObject();
        writer.WriteString("file", file.File.RelativePath);
        writer.WriteString("assembly", file.File.Project.AssemblyName);
        writer.WriteNumber("line", unit.DeclarationLine + 1);
        writer.WriteNumber("column", unit.DeclarationColumn + 1);
        writer.WriteString("indent", candidate.Source.Indent);
        writer.WriteString("unit", unit.Name);
        writer.WriteString("displayName", unit.DisplayName);
        writer.WriteString("kind", unit.Kind);
        writer.WriteString("fingerprint", unit.Fingerprint);
        writer.WriteStartObject("extent");
        writer.WriteNumber("startLine", unit.DeclarationLine + 1);
        writer.WriteNumber("endLine", unit.EndLine + 1);
        writer.WriteEndObject();
        writer.WriteBoolean("exempt", candidate.IsExempt);
        writer.WriteString("exemption", candidate.Exemption);
        writer.WriteString("state", AssuranceStateMachine.Name(candidate.State));
        writer.WriteBoolean("insertable", candidate.Insertable);
        writer.WriteString("reason", candidate.Reason);
        if (candidate.Detail is not null)
            writer.WriteString("detail", candidate.Detail);

        writer.WriteEndObject();
    }

    private static string Write(Action<Utf8JsonWriter> body)
    {
        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions
        {
            Indented = true,
            NewLine = "\n",
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            body(writer);
        }

        return Encoding.UTF8.GetString(buffer.ToArray()) + "\n";
    }
}

internal readonly record struct ListTotals(
    int Files, int Units, int Relevant, int Exempt, int Annotated, int Unannotated, int Insertable, int Listed, int Unreadable)
{
    public static ListTotals Of(IEnumerable<ListedFile> files, bool allUnits)
    {
        int count = 0, units = 0, relevant = 0, exempt = 0, annotated = 0, unannotated = 0, insertable = 0, listed = 0, unreadable = 0;
        foreach (ListedFile file in files)
        {
            count++;
            if (file.Problem is not null)
                unreadable++;

            foreach (AssuranceCandidate candidate in file.Candidates)
            {
                units++;
                if (candidate.IsExempt)
                {
                    exempt++;
                    continue;
                }

                relevant++;
                if (candidate.IsAnnotated)
                    annotated++;
                else
                    unannotated++;

                if (candidate.Insertable)
                    insertable++;
            }

            listed += AssuranceJson.Listed(file, allUnits).Count();
        }

        return new ListTotals(count, units, relevant, exempt, annotated, unannotated, insertable, listed, unreadable);
    }

    public void Write(Utf8JsonWriter writer, bool includeFiles = true)
    {
        if (includeFiles)
            writer.WriteNumber("files", Files);

        writer.WriteNumber("units", Units);
        writer.WriteNumber("relevant", Relevant);
        writer.WriteNumber("exempt", Exempt);
        writer.WriteNumber("annotated", Annotated);
        writer.WriteNumber("unannotated", Unannotated);
        writer.WriteNumber("insertable", Insertable);
        writer.WriteNumber("listed", Listed);
        writer.WriteNumber("unreadable", Unreadable);
    }
}
