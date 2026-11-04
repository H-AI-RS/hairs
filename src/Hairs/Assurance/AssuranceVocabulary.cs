// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace Hairs.Assurance;

/// <summary>
/// Vocabulary and literal markers of the Human AI Review System (HAIRS) annotations.
/// </summary>
public static class AssuranceVocabulary
{
    public const string AiMarker = "// Hairs-AI:";
    public const string FalsifiedIfMarker = "// Hairs-Falsified-If:";
    public const string HumanMarker = "// Hairs-Human:";

    public static readonly int LabelWidth = FalsifiedIfMarker.Length;

    public const string Pending = "PENDING";
    public const string Stale = "STALE";
    public const string ToBeFilled = "TBF";
    public const int FingerprintWidth = 6;
    public const string FingerprintField = "Fingerprint";
    public const string ExemptField = "EXEMPT";
    public const string TopLevelStatements = "<top-level statements>";

    public static readonly string[] OriginValues =
        ["Original", "AI", "Specification", "Derived", "Ported", "ThirdParty"];

    public static readonly string[] IpRiskValues =
        ["None", "Low", "Medium", "High", "Unknown"];

    public static readonly string[] SecurityRiskValues =
        ["None", "Low", "Medium", "High", "Critical"];

    public const string DeclaredInSource = "DeclaredInSource";
    public const string NamedValue = "NamedValue";

    public static readonly string[] ExemptionCases =
    [
        "TrivialPropertyOrAccessor",
        "ParameterAssigningConstructor",
        "TrivialExpressionBodiedMember",
        "CompilerSuppliedRecordOrEnumMember",
        "DelegatingOverrideOrOperator",
        "InsideAssemblyMarker",
        "FieldDeclaringStorage",
        "EnumMemberOfADeclaredVocabulary",
        NamedValue,
        DeclaredInSource,
    ];

    public static readonly AssuranceUnitState[] States =
    [
        AssuranceUnitState.New,
        AssuranceUnitState.AiAssessed,
        AssuranceUnitState.HumanPending,
        AssuranceUnitState.HumanApprovedPendingFingerprint,
        AssuranceUnitState.Verified,
        AssuranceUnitState.Stale,
        AssuranceUnitState.Exempt,
    ];

    public static readonly string[] RequiredFields =
        ["Origin", "IP", "Security", "Resources", FingerprintField];

    public const string ConceptualStabilityField = "CS";
    public const string ChangeLikelihoodField = "CL";

    public static readonly string[] HumanFieldMarkers =
        [FingerprintField + "=", "IP=", "Security=", "Resources=", ConceptualStabilityField + "=", ChangeLikelihoodField + "="];

    public static bool IsAiMarker(string trimmed) =>
        trimmed.StartsWith(AiMarker, StringComparison.Ordinal);

    public static bool IsFalsifiedIfMarker(string trimmed) =>
        trimmed.StartsWith(FalsifiedIfMarker, StringComparison.Ordinal);

    public static bool IsHumanMarker(string trimmed) =>
        trimmed.StartsWith(HumanMarker, StringComparison.Ordinal);

    public static string MatchAiMarker(string trimmed) => AiMarker;

    public static string MatchFalsifiedIfMarker(string trimmed) => FalsifiedIfMarker;

    public static string MatchHumanMarker(string trimmed) => HumanMarker;

    public static bool IsWellFormedFingerprint(string? value) =>
        value is { Length: FingerprintWidth } &&
        AllHex(value);

    public const int MaxAliasLength = 64;

    private static readonly HashSet<string> PlaceholderWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "PENDING", "STALE", "TBF", "TBD", "TBA", "TBC", "TODO", "FIXME", "XXX", "WIP", "LATER",
        "NONE", "NOBODY", "NOONE", "NOT", "NO", "NA", "NIL", "NULL", "UNKNOWN", "UNASSIGNED",
        "ANONYMOUS", "SOMEONE", "SOMEBODY", "PLACEHOLDER", "MISSING", "SKIP", "SKIPPED", "EXEMPT",
        "REVIEW", "REVIEWS", "REVIEWED", "REVIEWING", "REVIEWER", "UNREVIEWED", "NEEDS", "AWAITING", "WAITING",
        "YES", "OK", "OKAY", "LGTM", "DONE", "APPROVE", "APPROVED", "ACCEPTED", "VERIFIED", "CHECKED",
    };

    public static bool IsAlias(string? value)
    {
        if (value is null || value.Length is 0 or > MaxAliasLength || !char.IsLetter(value[0]) || value[^1] == ' ')
            return false;

        char previous = '\0';
        foreach (char character in value)
        {
            bool allowed = character == ' '
                ? previous != ' '
                : char.IsLetterOrDigit(character) || character is '.' or '_' or '-' or '\'' ||
                  System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) is
                      System.Globalization.UnicodeCategory.NonSpacingMark or
                      System.Globalization.UnicodeCategory.SpacingCombiningMark;

            if (!allowed)
                return false;

            previous = character;
        }

        foreach (string word in value.Split([' ', '.', '_', '-', '\''], StringSplitOptions.RemoveEmptyEntries))
        {
            if (PlaceholderWords.Contains(word))
                return false;
        }

        return true;
    }

    public static bool IsWritableReviewer(string? reviewer) =>
        reviewer is not null && IsAlias(reviewer.Trim());

    private static bool AllHex(string value)
    {
        foreach (char character in value)
        {
            if (character is not (>= '0' and <= '9' or >= 'A' and <= 'F'))
                return false;
        }

        return true;
    }
}
