// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hairs.Assurance;

public readonly record struct AssuranceSummary(
    int Relevant,
    int Annotated,
    int Exempt,
    int Verified,
    int Unverified,
    string? MaxIpRisk,
    string? MaxSecurityRisk,
    int Criteria,
    int CriteriaRequired,
    int? MaxResources)
{
    public double? MeanResources { get; init; }

    public static AssuranceSummary Of(IEnumerable<AssuranceCorpusUnit> units)
    {
        ArgumentNullException.ThrowIfNull(units);

        int relevant = 0, exempt = 0, annotated = 0, verified = 0, unverified = 0, criteria = 0, required = 0;
        var assessed = new List<AssuranceAnnotation>();
        var scores = new List<int>();

        foreach (AssuranceCorpusUnit unit in units)
        {
            AssuranceAnnotation? annotation = unit.Annotation;

            if (annotation is { HasCriterionLine: true })
                criteria++;

            if (annotation is not null && AssuranceRules.RequiresFalsificationCriterion(annotation))
                required++;

            if (unit.IsExempt)
            {
                exempt++;
                continue;
            }

            relevant++;

            if (unit.State == AssuranceUnitState.Verified)
                verified++;

            if (AssuranceStateMachine.BlocksRelease(unit.State))
                unverified++;

            if (annotation is not { ExemptReason: null })
                continue;

            annotated++;
            assessed.Add(annotation);

            if (int.TryParse(annotation.Field("Resources"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int score))
                scores.Add(score);
        }

        return new AssuranceSummary(
            relevant,
            annotated,
            exempt,
            verified,
            unverified,
            AssuranceBanner.Worst(assessed, "IP", AssuranceVocabulary.IpRiskValues),
            AssuranceBanner.Worst(assessed, "Security", AssuranceVocabulary.SecurityRiskValues),
            criteria,
            required,
            scores.Count == 0 ? null : scores.Max())
        {
            MeanResources = scores.Count == 0 ? null : scores.Average(),
        };
    }
}

public static class AssuranceBanner
{
    public const string SpdxCopyrightPrefix = "// SPDX-FileCopyrightText:";
    public const string SpdxLicensePrefix = "// SPDX-License-Identifier:";
    public const string SpdxPrefix = "// SPDX-";
    public const string GeneratedMarker = "// GENERATED - DO NOT EDIT MANUALLY";
    public const string Banner = "// HAIRS Code Assurance";
    public const string BannerRule = "// --------------------";

    public const int LabelWidth = 18;
    public const string NotAssessed = "not assessed";

    public static string Row(string label, string value)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(value);

        return string.Concat("// ", label.PadRight(LabelWidth), value);
    }

    public static IReadOnlyList<string> Render(AssuranceSummary summary, string copyright, string licence)
    {
        ArgumentNullException.ThrowIfNull(copyright);
        ArgumentNullException.ThrowIfNull(licence);

        return Render(summary, [copyright, licence]);
    }

    public static IReadOnlyList<string> Render(AssuranceSummary summary, AssuranceSpdx spdx)
    {
        ArgumentNullException.ThrowIfNull(spdx);

        return Render(summary, SpdxLines(spdx));
    }

    public static IReadOnlyList<string> SpdxLines(AssuranceSpdx spdx)
    {
        ArgumentNullException.ThrowIfNull(spdx);

        var lines = new List<string>(spdx.Copyright.Count + 1);
        foreach (string holder in spdx.Copyright)
            lines.Add(SpdxCopyrightPrefix + " " + holder);

        lines.Add(SpdxLicensePrefix + " " + spdx.License);
        return lines;
    }

    public static readonly IReadOnlyList<string> RowLabels =
    [
        "Relevant units:",
        "Annotated:",
        "Exempt:",
        "Human-reviewed:",
        "IP risk:",
        "Security risk:",
        "Criteria:",
        "Resource impact:",
        "Unverified:",
    ];

    private static IReadOnlyList<string> Render(AssuranceSummary summary, IReadOnlyList<string> spdxLines)
    {
        return
        [
            .. spdxLines,
            "//",
            Banner,
            BannerRule,
            Row("Relevant units:", Count(summary.Relevant)),
            Row("Annotated:", Fraction(summary.Annotated, summary.Relevant)),
            Row("Exempt:", Count(summary.Exempt)),
            Row("Human-reviewed:", Fraction(summary.Verified, summary.Relevant)),
            Row("IP risk:", summary.MaxIpRisk ?? NotAssessed),
            Row("Security risk:", summary.MaxSecurityRisk ?? NotAssessed),
            Row("Criteria:", Fraction(summary.Criteria, summary.CriteriaRequired)),
            Row(
                "Resource impact:",
                summary.MaxResources is { } score
                    ? string.Create(CultureInfo.InvariantCulture, $"{score}/10 max")
                    : NotAssessed),
            Row("Unverified:", Count(summary.Unverified)),
            "//",
            GeneratedMarker,
        ];
    }

    public static int Length(AssuranceLines lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (lines.Count == 0 ||
            !lines[0].StartsWith(SpdxCopyrightPrefix, StringComparison.Ordinal))
        {
            return 0;
        }

        for (int line = 0; line < lines.Count && lines[line].StartsWith("//", StringComparison.Ordinal); line++)
        {
            if (string.Equals(lines[line], GeneratedMarker, StringComparison.Ordinal))
                return line + 1;
        }

        return 0;
    }

    public static string? Worst(
        IEnumerable<AssuranceAnnotation> assessed, string field, IReadOnlyList<string> vocabulary)
    {
        ArgumentNullException.ThrowIfNull(assessed);
        ArgumentNullException.ThrowIfNull(vocabulary);

        int worst = -1;
        foreach (AssuranceAnnotation annotation in assessed)
        {
            string? value = annotation.Field(field);
            if (value is null)
                continue;

            for (int rank = 0; rank < vocabulary.Count; rank++)
            {
                if (string.Equals(vocabulary[rank], value, StringComparison.Ordinal) && rank > worst)
                    worst = rank;
            }
        }

        return worst < 0 ? null : vocabulary[worst];
    }

    private static string Count(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string Fraction(int numerator, int denominator) =>
        string.Create(CultureInfo.InvariantCulture, $"{numerator}/{denominator}");
}
