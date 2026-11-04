// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0
//
// HAIRS Code Assurance
// --------------------
// Relevant units:   8
// Annotated:        8/8
// Exempt:           8
// Human-reviewed:   0/8
// IP risk:          None
// Security risk:    High
// Criteria:         2/2
// Resource impact:  1/10 max
// Unverified:       8
//
// GENERATED - DO NOT EDIT MANUALLY

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Hairs.Assurance;

/// <summary>One covered source file, decoded, as the generator reads it.</summary>
/// <param name="RelativePath">Root-relative, forward slashes.</param>
/// <param name="Assembly">The assembly the file's project builds.</param>
/// <param name="Text">The decoded text, without a byte-order mark.</param>
// Hairs-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=0CB756
// Hairs-Human:        PENDING
public sealed record AssuranceSource(string RelativePath, string Assembly, string Text);

/// <summary>A file under a covered project that the configuration leaves out, and why.</summary>
// Hairs-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=E52E05
// Hairs-Human:        PENDING
public sealed record AssuranceExcludedSource(string RelativePath, string Reason);

/// <summary>
/// Everything the generator reads: the covered files, the files left out, and
/// the component-level artefacts as they are now.
///
/// Pure data. The command-line tool fills it from disk; a test fills it by
/// hand. Nothing downstream of this record touches a file system, which is what
/// lets the check compute exactly what the generator would write without
/// writing it.
/// </summary>
/// <param name="Files">The covered files, ordered by path (ordinal).</param>
/// <param name="Excluded">Files the configuration leaves out, for the report.</param>
/// <param name="Assemblies">The covered assemblies, for generated prose.</param>
/// <param name="Artefacts">
/// The current text of each component-level artefact, by root-relative path.
/// A path that is absent here does not exist on disk.
/// </param>
// Hairs-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=2C83F4
// Hairs-Human:        PENDING
public sealed record AssuranceCorpus(
    IReadOnlyList<AssuranceSource> Files,
    IReadOnlyList<AssuranceExcludedSource> Excluded,
    IReadOnlyList<string> Assemblies,
    IReadOnlyDictionary<string, string> Artefacts)
{
    /// <summary>
    /// Review records a person keeps beside the generated ones: a file at an
    /// artefact's default path (<c>HUMAN_REVIEW.md</c>, <c>CODE-ASSURANCE.md</c>)
    /// that exists while the configuration writes that artefact elsewhere. The
    /// report names them, so that it is not read as the component's only word
    /// on review.
    /// </summary>
    public IReadOnlyList<string> SeparateRecords { get; init; } = [];
}

/// <summary>
/// One code unit of the component, placed in its file: what the report,
/// manifest and rules are computed from.
/// </summary>
/// <param name="File">The file's root-relative path.</param>
/// <param name="Assembly">The file's assembly.</param>
/// <param name="Candidate">The unit, classified from the file's text.</param>
// Hairs-AI:           Origin=AI; IP=None; Security=High; Resources=1; Fingerprint=492720
// Hairs-Falsified-If: a candidate the exemption predicate does not exempt reads as not relevant, so the J1 and J11 rules skip it
// Hairs-Human:        PENDING
public sealed record AssuranceCorpusUnit(string File, string Assembly, AssuranceCandidate Candidate)
{
    public string Name => Candidate.Unit.Name;

    public string Fingerprint => Candidate.Unit.Fingerprint;

    /// <summary>The 1-based line of the declaration's first token, attributes included.</summary>
    // Hairs-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=0DB13E
    // Hairs-Human:        PENDING
    public int Line => Candidate.Unit.DeclarationLine + 1;

    public AssuranceAnnotation? Annotation => Candidate.Annotation;

    public bool IsExempt => Candidate.IsExempt;

    // Hairs-AI:           Origin=AI; IP=None; Security=High; Resources=0; Fingerprint=DC666B
    // Hairs-Falsified-If: a candidate the exemption predicate does not exempt reads as not relevant, so the J1 and J11 rules skip it
    // Hairs-Human:        PENDING
    public bool IsRelevant => !Candidate.IsExempt;

    /// <summary>The exemption case, <c>DeclaredInSource</c> for <c>EXEMPT=</c>, or <c>None</c>.</summary>
    public string Exemption => Candidate.Exemption;

    public AssuranceUnitState State => Candidate.State;

    /// <summary>
    /// The unit as every message names it: <c>path(line): name</c>.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=2D3788
    // Hairs-Human:        PENDING
    public string Where => string.Create(CultureInfo.InvariantCulture, $"{File}({Line}): {Name}");
}

/// <summary>
/// One thing the check found wrong, or one reason the generator refused.
/// </summary>
/// <param name="Rule">
/// The rule it breaks: <c>J1</c> to <c>J11</c>, or <c>IO</c> for a file the tool cannot read or cannot index by line.
/// </param>
/// <param name="File">The root-relative file it is about, or null for the component.</param>
/// <param name="Line">The 1-based line, when there is one.</param>
/// <param name="Message">The message explaining the issue.</param>
// Hairs-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=2BAEB4
// Hairs-Human:        PENDING
public sealed record AssuranceViolation(string Rule, string? File, int? Line, string Message)
{
    /// <summary>
    /// The command that resolves the violation, where there is one, for a
    /// reader who has only the annotation in front of them: <c>generate</c> for
    /// a fingerprint, <c>list</c> and <c>insert</c> for a missing block.
    /// </summary>
    public string? Remedy { get; init; }
}
