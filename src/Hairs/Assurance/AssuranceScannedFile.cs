// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace Hairs.Assurance;

public readonly record struct AssuranceDirective(int Line, string Text);

public sealed record AssuranceFileUnit(
    AssuranceScannedUnit Unit,
    string Indent,
    bool StartsOwnLine,
    int? AnnotationLine,
    IReadOnlyList<int> LeadingAssuranceLines,
    int HeaderEndLine);

public sealed record AssuranceScannedFile(
    IReadOnlyList<AssuranceFileUnit> Units,
    string FileFingerprint,
    IReadOnlyList<AssuranceDirective> Directives,
    IReadOnlyList<int> AssuranceCommentLines,
    int LineCount)
{
    public IReadOnlyList<AssuranceCommentLine> CommentLines { get; init; } = [];
}

public readonly record struct AssuranceCommentLine(int Line, string Text);

public interface IAssuranceFileScanner
{
    AssuranceScannedFile ScanFile(string text, string path);
}
