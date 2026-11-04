// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace Hairs.Assurance;

public sealed record AssuranceUnit
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required int StartLine { get; init; }
    public required int EndLine { get; init; }
    public required int DeclarationLine { get; init; }
    public bool IsExempt { get; init; }
    public string Exemption { get; init; } = "None";
    public string? Fingerprint { get; init; }
    public AssuranceAnnotation? Annotation { get; init; }
    public AssuranceUnitState State { get; init; } = AssuranceUnitState.New;

    public bool IsRelevant => !IsExempt;
    public bool IsWritable => Annotation is not null && !IsExempt;
    public bool Contains(int line) => line >= StartLine && line <= EndLine;
}

public readonly record struct AssuranceScannedUnit(
    string Name,
    string DisplayName,
    int DeclarationLine,
    int EndLine,
    bool IsExempt,
    string Exemption,
    string Fingerprint)
{
    public string Kind { get; init; } = string.Empty;
    public int DeclarationColumn { get; init; }
}

public interface IAssuranceUnitScanner
{
    IReadOnlyList<AssuranceScannedUnit> Scan(string text, string path);
}
