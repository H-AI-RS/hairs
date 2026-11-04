// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;

namespace Hairs.Assurance;

public readonly record struct AssuranceField(string Key, string Value);

public sealed class AssuranceAnnotation
{
    private AssuranceAnnotation(
        int aiLine,
        int? falsifiedIfLine,
        int humanLine,
        string indent,
        IReadOnlyList<AssuranceField> fields,
        string criterion,
        string humanBody)
    {
        AiLine = aiLine;
        FalsifiedIfLine = falsifiedIfLine;
        HumanLine = humanLine;
        Indent = indent;
        Fields = fields;
        Criterion = criterion;
        HumanBody = humanBody;
    }

    public int AiLine { get; }
    public int? FalsifiedIfLine { get; }
    public int HumanLine { get; }
    public string Indent { get; }
    public IReadOnlyList<AssuranceField> Fields { get; }
    public string Criterion { get; }
    public string HumanBody { get; }

    public int FirstLine => AiLine;
    public int LastLine => HumanLine;
    public bool HasCriterion => Criterion.Length > 0;
    public bool HasCriterionLine => FalsifiedIfLine is not null;
    public string? FalsifiedIf => FalsifiedIfLine is null ? null : Criterion;

    public (string Reviewer, string Fingerprint)? Previous
    {
        get
        {
            const string prefix = "Previous=";
            foreach (string part in HumanBody.Split(';', StringSplitOptions.TrimEntries))
            {
                if (!part.StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                string value = part[prefix.Length..];
                int at = value.LastIndexOf('@');
                return at < 0 ? (value, string.Empty) : (value[..at], value[(at + 1)..]);
            }

            return null;
        }
    }

    public string? RecordedFingerprint => Field(AssuranceVocabulary.FingerprintField);
    public string? ExemptReason => Field(AssuranceVocabulary.ExemptField);

    public bool HumanIsPending =>
        string.Equals(HumanBody, AssuranceVocabulary.Pending, StringComparison.Ordinal);

    public bool HumanIsStale =>
        HumanBody.StartsWith(AssuranceVocabulary.Stale, StringComparison.Ordinal);

    public string? Reviewer
    {
        get
        {
            if (HumanIsPending || HumanIsStale || HumanBody.Length == 0)
                return null;

            int semicolon = HumanBody.IndexOf(';', StringComparison.Ordinal);
            string name = (semicolon < 0 ? HumanBody : HumanBody[..semicolon]).Trim();
            return AssuranceVocabulary.IsAlias(name) ? name : null;
        }
    }

    public string? HumanFingerprint
    {
        get
        {
            foreach (string part in HumanBody.Split(';', StringSplitOptions.TrimEntries))
            {
                if (part.StartsWith(AssuranceVocabulary.FingerprintField + "=", StringComparison.Ordinal))
                    return part[(AssuranceVocabulary.FingerprintField.Length + 1)..];
            }

            return null;
        }
    }

    public IReadOnlyList<string> HumanAssessment
    {
        get
        {
            if (Reviewer is null)
                return [];

            var assessment = new List<string>();
            bool first = true;
            foreach (string part in HumanBody.Split(';', StringSplitOptions.TrimEntries))
            {
                if (first)
                {
                    first = false;
                    continue;
                }

                if (part.Length > 0 &&
                    !part.StartsWith(AssuranceVocabulary.FingerprintField + "=", StringComparison.Ordinal))
                {
                    assessment.Add(part);
                }
            }

            return assessment;
        }
    }

    public static bool TryParse(AssuranceLines lines, int aiLine, out AssuranceAnnotation? annotation)
    {
        ArgumentNullException.ThrowIfNull(lines);
        annotation = null;

        if (aiLine < 0 || aiLine >= lines.Count)
            return false;

        string ai = lines[aiLine];
        string trimmedAi = ai.TrimStart();
        if (!AssuranceVocabulary.IsAiMarker(trimmedAi))
            return false;

        string aiMarker = AssuranceVocabulary.MatchAiMarker(trimmedAi);

        int next = aiLine + 1;
        int? criterionLine = null;
        string criterion = string.Empty;

        if (next < lines.Count && AssuranceVocabulary.IsFalsifiedIfMarker(lines[next].TrimStart()))
        {
            string fMarker = AssuranceVocabulary.MatchFalsifiedIfMarker(lines[next].TrimStart());
            criterionLine = next;
            criterion = Body(lines[next], fMarker);
            next++;
        }

        if (next >= lines.Count || !AssuranceVocabulary.IsHumanMarker(lines[next].TrimStart()))
            return false;

        string hMarker = AssuranceVocabulary.MatchHumanMarker(lines[next].TrimStart());

        annotation = new AssuranceAnnotation(
            aiLine,
            criterionLine,
            next,
            AssuranceLines.IndentOf(ai),
            ParseFields(Body(ai, aiMarker)),
            criterion,
            Body(lines[next], hMarker));

        return true;
    }

    public static bool TryParseStrict(
        AssuranceLines lines, int aiLine, out AssuranceAnnotation? annotation, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(lines);
        annotation = null;
        problem = null;

        if (aiLine < 0 || aiLine >= lines.Count)
        {
            problem = $"line {aiLine + 1} is outside the file";
            return false;
        }

        string ai = lines[aiLine].Trim();
        if (!AssuranceVocabulary.IsAiMarker(ai))
        {
            problem = $"line {aiLine + 1} does not open with '{AssuranceVocabulary.AiMarker}'";
            return false;
        }

        string aiMarker = AssuranceVocabulary.MatchAiMarker(ai);

        int next = aiLine + 1;
        int? criterionLine = null;
        string criterion = string.Empty;

        if (next < lines.Count && AssuranceVocabulary.IsFalsifiedIfMarker(lines[next].Trim()))
        {
            string fMarker = AssuranceVocabulary.MatchFalsifiedIfMarker(lines[next].Trim());
            criterion = lines[next].Trim()[fMarker.Length..].Trim();
            criterionLine = next;
            next++;

            if (next < lines.Count && AssuranceVocabulary.IsFalsifiedIfMarker(lines[next].Trim()))
            {
                problem = $"line {next + 1} carries a second '{AssuranceVocabulary.FalsifiedIfMarker}' line, " +
                    "and a falsification criterion is one line";
                return false;
            }
        }

        if (next >= lines.Count)
        {
            problem = $"line {aiLine + 1} has no '{AssuranceVocabulary.HumanMarker}' line under it";
            return false;
        }

        string human = lines[next].Trim();
        if (!AssuranceVocabulary.IsHumanMarker(human))
        {
            problem = $"line {aiLine + 1} is not immediately followed by a '{AssuranceVocabulary.HumanMarker}' line";
            return false;
        }

        string hMarker = AssuranceVocabulary.MatchHumanMarker(human);

        var fields = new List<AssuranceField>();
        foreach (string part in ai[aiMarker.Length..].Split(';'))
        {
            if (part.Trim().Length == 0)
                continue;

            int separator = part.IndexOf('=', StringComparison.Ordinal);
            if (separator < 0)
            {
                problem = $"line {aiLine + 1} has a field with no '=': '{part.Trim()}'";
                return false;
            }

            fields.Add(new AssuranceField(part[..separator].Trim(), part[(separator + 1)..].Trim()));
        }

        if (fields.Count == 0)
        {
            problem = $"line {aiLine + 1} carries no fields";
            return false;
        }

        annotation = new AssuranceAnnotation(
            aiLine,
            criterionLine,
            next,
            AssuranceLines.IndentOf(lines[aiLine]),
            fields,
            criterion,
            human[hMarker.Length..].Trim());

        return true;
    }

    public static string RenderLine(string indent, string marker, string value)
    {
        ArgumentNullException.ThrowIfNull(indent);
        ArgumentNullException.ThrowIfNull(marker);
        ArgumentNullException.ThrowIfNull(value);

        return value.Length == 0
            ? indent + marker
            : indent + marker.PadRight(AssuranceVocabulary.LabelWidth) + " " + value;
    }

    public string? Field(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        foreach (AssuranceField field in Fields)
        {
            if (string.Equals(field.Key, key, StringComparison.Ordinal))
                return field.Value;
        }

        return null;
    }

    public string RenderAiLine()
    {
        var builder = new StringBuilder();
        for (int index = 0; index < Fields.Count; index++)
        {
            if (index > 0)
                builder.Append("; ");

            builder.Append(Fields[index].Key);
            if (Fields[index].Value.Length > 0)
                builder.Append('=').Append(Fields[index].Value);
        }

        return RenderLine(Indent, AssuranceVocabulary.AiMarker, builder.ToString());
    }

    public string RenderHumanLine(string body) =>
        RenderLine(Indent, AssuranceVocabulary.HumanMarker, body);

    public static string RenderAiLine(string indent, IEnumerable<AssuranceField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var builder = new StringBuilder();
        foreach (AssuranceField field in fields)
        {
            if (builder.Length > 0)
                builder.Append("; ");

            builder.Append(field.Key).Append('=').Append(field.Value);
        }

        return RenderLine(indent, AssuranceVocabulary.AiMarker, builder.ToString());
    }

    public static string RenderFalsifiedIfLine(string indent, string criterion) =>
        RenderLine(indent, AssuranceVocabulary.FalsifiedIfMarker, criterion);

    public static string RenderHumanLine(string indent, string body) =>
        RenderLine(indent, AssuranceVocabulary.HumanMarker, body);

    private static string Body(string line, string marker)
    {
        string trimmed = line.TrimStart();
        return trimmed[marker.Length..].Trim();
    }

    private static IReadOnlyList<AssuranceField> ParseFields(string body)
    {
        if (body.Length == 0)
            return [];

        var fields = new List<AssuranceField>();
        foreach (string part in body.Split(';', StringSplitOptions.TrimEntries))
        {
            if (part.Length == 0)
                continue;

            int equals = part.IndexOf('=', StringComparison.Ordinal);
            fields.Add(equals < 0
                ? new AssuranceField(part, string.Empty)
                : new AssuranceField(part[..equals], part[(equals + 1)..]));
        }

        return fields;
    }
}
