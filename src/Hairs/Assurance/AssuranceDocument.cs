// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0
//
// HAIRS Code Assurance
// --------------------
// Relevant units:   24
// Annotated:        24/24
// Exempt:           13
// Human-reviewed:   0/24
// IP risk:          Low
// Security risk:    High
// Criteria:         22/13
// Resource impact:  6/10 max
// Unverified:       24
//
// GENERATED - DO NOT EDIT MANUALLY

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Hairs.Assurance;

/// <summary>Why a source rewrite was refused, or that it was applied.</summary>
// Hairs-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=8BF80C
// Hairs-Human:        PENDING
public enum AssuranceEditOutcome
{
    Applied = 0,

    /// <summary>The declaration carries no annotation block, so there is no line to write.</summary>
    NotAnnotated,

    /// <summary>The unit is exempt. The assurance system expects no human line to move on it.</summary>
    Exempt,

    /// <summary>The reviewer's name is missing, or would not survive the format.</summary>
    NoReviewer,

    /// <summary>The line already says this. Rewriting it would be a diff with no change in it.</summary>
    NothingToDo,
}

/// <summary>
/// The result of rewriting one annotation: the new file text, and a sentence
/// saying what happened to it.
/// </summary>
/// <param name="Outcome">Whether the rewrite was applied.</param>
/// <param name="Text">The file as it should now be. The original text when nothing was applied.</param>
/// <param name="Message">A sentence for the status line.</param>
/// <param name="HeaderUpdated">
/// Whether the generated file header was recounted as part of the same edit.
/// False is normal and not a failure — see <see cref="AssuranceDocument.BannerIsReproducible"/>.
/// </param>
// Hairs-AI:           Origin=AI; IP=None; Security=Medium; Resources=0; Fingerprint=749709
// Hairs-Falsified-If: a result built for a refusal reports Succeeded, so the controller writes its text into the buffer
// Hairs-Human:        PENDING
public readonly record struct AssuranceEditResult(
    AssuranceEditOutcome Outcome,
    string Text,
    string Message,
    bool HeaderUpdated)
{
    // Hairs-AI:           Origin=AI; IP=None; Security=Medium; Resources=0; Fingerprint=C9514B
    // Hairs-Falsified-If: an outcome of NothingToDo, NoReviewer, Exempt or NotAnnotated reports Succeeded true
    // Hairs-Human:        PENDING
    public bool Succeeded => Outcome == AssuranceEditOutcome.Applied;
}

/// <summary>
/// One source file, read as the assurance system reads it: a generated header,
/// and a sequence of code units each carrying what a machine assessed and what a
/// human has said.
/// </summary>
// Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=59E35A
// Hairs-Falsified-If: signing a unit writes a body carrying a fingerprint part that the unit's human line did not already hold
// Hairs-Human:        PENDING
public sealed class AssuranceDocument
{
    private readonly AssuranceLines _lines;
    private readonly int _bannerLength;
    private readonly IAssuranceUnitScanner? _scanner;
    private readonly string _path;

    // Hairs-AI:           Origin=AI; IP=None; Security=High; Resources=0; Fingerprint=112B63
    // Hairs-Falsified-If: a document constructed with a null scanner reports HasUnitScanner true
    // Hairs-Human:        PENDING
    private AssuranceDocument(
        AssuranceLines lines,
        IReadOnlyList<AssuranceUnit> units,
        int bannerLength,
        IAssuranceUnitScanner? scanner,
        string path)
    {
        _lines = lines;
        _bannerLength = bannerLength;
        _scanner = scanner;
        _path = path;
        Units = units;
        HasUnitScanner = scanner is not null;
    }

    /// <summary>The file's units, in document order.</summary>
    public IReadOnlyList<AssuranceUnit> Units { get; }

    /// <summary>
    /// True when a language service supplied the units, so exemptions,
    /// fingerprints and therefore the header's counts are all known.
    /// </summary>
    public bool HasUnitScanner { get; }

    /// <summary>True when the file carries a generated assurance header.</summary>
    // Hairs-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=4185AF
    // Hairs-Falsified-If: a file with no generated header reports HasBanner true
    // Hairs-Human:        PENDING
    public bool HasBanner => _bannerLength > 0;

    /// <summary>True when the file carries anything this pane can talk about.</summary>
    // Hairs-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=DF5A05
    // Hairs-Falsified-If: a file with neither annotated units nor a header reports IsAnnotated true
    // Hairs-Human:        PENDING
    public bool IsAnnotated => Units.Count > 0 || HasBanner;

    /// <summary>
    /// The counts the generated header reports, or null when this build cannot
    /// establish them.
    /// </summary>
    public AssuranceSummary? Summary { get; private set; }

    /// <summary>
    /// True when this build reproduces the header the file already carries,
    /// exactly.
    /// </summary>
    public bool BannerIsReproducible { get; private set; }

    /// <summary>
    /// Reads <paramref name="text"/>.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=EB72F1
    // Hairs-Falsified-If: Read with no scanner yields a Summary or sets BannerIsReproducible true
    // Hairs-Human:        PENDING
    public static AssuranceDocument Read(
        string text, IAssuranceUnitScanner? scanner = null, string path = "")
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(path);

        var lines = new AssuranceLines(text);
        int banner = AssuranceBanner.Length(lines);

        IReadOnlyList<AssuranceUnit> units = scanner is null
            ? FromAnnotations(lines)
            : FromScanner(lines, scanner, text, path);

        var document = new AssuranceDocument(lines, units, banner, scanner, path);
        document.Summarize();
        return document;
    }

    /// <summary>
    /// The unit <paramref name="line"/> falls in, or null.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=EA36A9
    // Hairs-Falsified-If: a caret on a line inside a member's body returns the enclosing type rather than the member
    // Hairs-Human:        PENDING
    public AssuranceUnit? UnitAt(int line)
    {
        AssuranceUnit? found = null;
        foreach (AssuranceUnit unit in Units)
        {
            if (!unit.Contains(line))
                continue;

            if (found is null || unit.StartLine >= found.StartLine)
                found = unit;
        }

        return found;
    }

    /// <summary>The unit with this qualified name, or null.</summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=162D55
    // Hairs-Falsified-If: a name that matches a unit only when case is ignored returns that unit
    // Hairs-Human:        PENDING
    public AssuranceUnit? UnitNamed(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        foreach (AssuranceUnit unit in Units)
        {
            if (string.Equals(unit.Name, name, StringComparison.Ordinal))
                return unit;
        }

        return null;
    }

    /// <summary>
    /// The body to write when <paramref name="reviewer"/> approves
    /// <paramref name="unit"/>.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=7D6D15
    // Hairs-Falsified-If: for a unit whose line names a different alias beside a fingerprint part, the returned body still carries a fingerprint part
    // Hairs-Human:        PENDING
    public static string ApprovalBody(AssuranceUnit unit, string reviewer)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(reviewer);

        AssuranceAnnotation annotation = unit.Annotation
            ?? throw new ArgumentException("The unit carries no annotation.", nameof(unit));

        string name = reviewer.Trim();
        if (!string.Equals(annotation.Reviewer, name, StringComparison.Ordinal))
            return name;

        if (unit.State != AssuranceUnitState.Stale)
            return annotation.HumanBody;

        var builder = new StringBuilder(name);
        foreach (string part in annotation.HumanAssessment)
            builder.Append("; ").Append(part);

        return builder.ToString();
    }

    /// <summary>
    /// Records <paramref name="reviewer"/> as having reviewed
    /// <paramref name="unit"/>, and recounts the header when it may.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=D1F2FB
    // Hairs-Falsified-If: signing an exempt unit, or signing with a placeholder name such as TODO, returns Applied
    // Hairs-Human:        PENDING
    public AssuranceEditResult Approve(AssuranceUnit unit, string reviewer)
    {
        ArgumentNullException.ThrowIfNull(unit);

        if (unit.IsExempt)
        {
            return Refused(
                AssuranceEditOutcome.Exempt,
                $"{unit.DisplayName} is exempt ({unit.Exemption}); the assurance system expects no review on it.");
        }

        if (unit.Annotation is not { } annotation)
        {
            return Refused(
                AssuranceEditOutcome.NotAnnotated,
                $"{unit.DisplayName} carries no assurance annotation, so there is no line to sign.");
        }

        if (!AssuranceVocabulary.IsWritableReviewer(reviewer))
        {
            return Refused(
                AssuranceEditOutcome.NoReviewer,
                reviewer is null || reviewer.Trim().Length == 0
                    ? "Set a reviewer name before signing a unit — an approval with no name is not evidence."
                    : $"'{reviewer.Trim()}' cannot be written as a reviewer: a name opens with a letter, " +
                      "holds letters, digits, '.', '_', '-', ''' and single spaces, is at most " +
                      $"{AssuranceVocabulary.MaxAliasLength} characters long, and is not a placeholder such as " +
                      "TODO, NONE or PENDING.");
        }

        return Rewrite(unit, annotation, ApprovalBody(unit, reviewer), Approved(unit, reviewer));
    }

    /// <summary>
    /// Puts <paramref name="unit"/> back to pending.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=C57ABC
    // Hairs-Falsified-If: Withdraw leaves a name or a fingerprint part on the human line instead of the bare pending word
    // Hairs-Human:        PENDING
    public AssuranceEditResult Withdraw(AssuranceUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        if (unit.Annotation is not { } annotation)
        {
            return Refused(
                AssuranceEditOutcome.NotAnnotated,
                $"{unit.DisplayName} carries no assurance annotation.");
        }

        return Rewrite(
            unit,
            annotation,
            AssuranceVocabulary.Pending,
            $"{unit.DisplayName} set back to {AssuranceVocabulary.Pending}.");
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=65405D
    // Hairs-Human:        PENDING
    private static string Approved(AssuranceUnit unit, string reviewer) =>
        $"{unit.DisplayName} signed by {reviewer.Trim()}; the assurance generator fills the fingerprint in.";

    // Hairs-AI:           Origin=AI; IP=None; Security=Low; Resources=3; Fingerprint=EEC5C4
    // Hairs-Falsified-If: a refusal carries text other than the document's lines as they stand
    // Hairs-Human:        PENDING
    private AssuranceEditResult Refused(AssuranceEditOutcome outcome, string message) =>
        new(outcome, _lines.Render(), message, false);

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=614506
    // Hairs-Falsified-If: a rewrite changes a line other than the annotation's human line and the header's own lines
    // Hairs-Human:        PENDING
    private AssuranceEditResult Rewrite(
        AssuranceUnit unit, AssuranceAnnotation annotation, string body, string message)
    {
        string line = annotation.RenderHumanLine(body);
        if (string.Equals(_lines[annotation.HumanLine], line, StringComparison.Ordinal))
        {
            return Refused(
                AssuranceEditOutcome.NothingToDo,
                $"{unit.DisplayName} already records exactly that.");
        }

        _lines.Replace(annotation.HumanLine, line);
        bool header = RecountBanner();

        return new AssuranceEditResult(
            AssuranceEditOutcome.Applied,
            _lines.Render(),
            header ? message + " The file header was recounted." : message,
            header);
    }

    /// <summary>
    /// Rewrites the generated header from the file as it now is, when this build
    /// had already proved it counts the same way.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=6D4DDE
    // Hairs-Falsified-If: header lines are replaced in a document whose header did not reproduce when it was read
    // Hairs-Human:        PENDING
    private bool RecountBanner()
    {
        if (!BannerIsReproducible || _bannerLength == 0)
            return false;

        AssuranceDocument edited = Read(_lines.Render(), _scanner, _path);
        if (edited.Summary is not { } summary)
            return false;

        IReadOnlyList<string> banner = AssuranceBanner.Render(summary, _lines[0], _lines[1]);

        bool changed = false;
        for (int line = 0; line < banner.Count && line < _bannerLength; line++)
        {
            if (!string.Equals(banner[line], _lines[line], StringComparison.Ordinal))
                changed = true;
        }

        if (!changed)
            return false;

        for (int line = 0; line < banner.Count; line++)
            _lines.Replace(line, banner[line]);

        return true;
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=707900
    // Hairs-Falsified-If: a unit whose criterion line is present but empty is left out of the Criteria count that AssuranceSummary.Of includes it in
    // Hairs-Human:        PENDING
    private void Summarize()
    {
        if (!HasUnitScanner)
        {
            Summary = null;
            BannerIsReproducible = false;
            return;
        }

        int relevant = 0;
        int annotated = 0;
        int exempt = 0;
        int verified = 0;
        int unverified = 0;
        int criteria = 0;
        int criteriaRequired = 0;
        int? maxResources = null;
        var assessed = new List<AssuranceAnnotation>();

        foreach (AssuranceUnit unit in Units)
        {
            if (unit.Annotation is { HasCriterion: true })
                criteria++;

            if (unit.Annotation is { ExemptReason: null } stated &&
                stated.Field("Security") is "High" or "Critical")
            {
                criteriaRequired++;
            }

            if (unit.IsExempt)
            {
                exempt++;
                continue;
            }

            relevant++;

            if (AssuranceStateMachine.BlocksRelease(unit.State))
                unverified++;

            if (unit.State == AssuranceUnitState.Verified)
                verified++;

            if (unit.Annotation is not { ExemptReason: null } annotation)
                continue;

            annotated++;
            assessed.Add(annotation);

            if (annotation.Field("Resources") is { } score &&
                int.TryParse(score, NumberStyles.None, CultureInfo.InvariantCulture, out int resources))
            {
                maxResources = maxResources is { } current ? Math.Max(current, resources) : resources;
            }
        }

        var summary = new AssuranceSummary(
            relevant,
            annotated,
            exempt,
            verified,
            unverified,
            AssuranceBanner.Worst(assessed, "IP", AssuranceVocabulary.IpRiskValues),
            AssuranceBanner.Worst(assessed, "Security", AssuranceVocabulary.SecurityRiskValues),
            criteria,
            criteriaRequired,
            maxResources);

        Summary = summary;
        BannerIsReproducible = ReproducesBanner(summary);
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=2; Fingerprint=08F3BE
    // Hairs-Falsified-If: a header that differs from the rendered block in one row, or carries a second copyright line, is reported as reproduced
    // Hairs-Human:        PENDING
    private bool ReproducesBanner(AssuranceSummary summary)
    {
        if (_bannerLength < 2)
            return false;

        IReadOnlyList<string> rendered = AssuranceBanner.Render(summary, _lines[0], _lines[1]);
        if (rendered.Count != _bannerLength)
            return false;

        for (int line = 0; line < rendered.Count; line++)
        {
            if (!string.Equals(rendered[line], _lines[line], StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Units as a language service found them, with the annotation above each
    /// one attached.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=75DC54
    // Hairs-Falsified-If: a unit whose block is an exemption line is returned with IsExempt false
    // Hairs-Human:        PENDING
    private static IReadOnlyList<AssuranceUnit> FromScanner(
        AssuranceLines lines, IAssuranceUnitScanner scanner, string text, string path)
    {
        IReadOnlyList<AssuranceScannedUnit> scanned = scanner.Scan(text, path);
        var units = new List<AssuranceUnit>(scanned.Count);

        foreach (AssuranceScannedUnit unit in scanned)
        {
            AssuranceAnnotation? annotation = AnnotationAbove(lines, unit.DeclarationLine);
            units.Add(new AssuranceUnit
            {
                Name = unit.Name,
                DisplayName = unit.DisplayName,
                StartLine = annotation?.FirstLine ?? unit.DeclarationLine,
                EndLine = Math.Max(unit.EndLine, unit.DeclarationLine),
                DeclarationLine = unit.DeclarationLine,
                IsExempt = unit.IsExempt || annotation?.ExemptReason is not null,
                Exemption = annotation?.ExemptReason is not null ? "DeclaredInSource" : unit.Exemption,
                Fingerprint = unit.Fingerprint,
                Annotation = annotation,
                State = AssuranceStateMachine.Resolve(annotation, unit.IsExempt, unit.Fingerprint),
            });
        }

        return units;
    }

    /// <summary>
    /// Units read from the annotation blocks alone.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=63D0DF
    // Hairs-Falsified-If: a machine line with no human line under it, directly or after one criterion line, is returned as a unit
    // Hairs-Human:        PENDING
    private static IReadOnlyList<AssuranceUnit> FromAnnotations(AssuranceLines lines)
    {
        var found = new List<AssuranceAnnotation>();
        for (int line = 0; line < lines.Count; line++)
        {
            if (AssuranceAnnotation.TryParse(lines, line, out AssuranceAnnotation? annotation) &&
                annotation is not null)
            {
                found.Add(annotation);
                line = annotation.LastLine;
            }
        }

        var units = new List<AssuranceUnit>(found.Count);
        for (int index = 0; index < found.Count; index++)
        {
            AssuranceAnnotation annotation = found[index];
            int declaration = Math.Min(annotation.LastLine + 1, lines.Count - 1);
            int end = index + 1 < found.Count ? found[index + 1].FirstLine - 1 : lines.Count - 1;
            string name = DeclarationName(lines, declaration);

            units.Add(new AssuranceUnit
            {
                Name = name,
                DisplayName = name,
                StartLine = annotation.FirstLine,
                EndLine = Math.Max(end, declaration),
                DeclarationLine = declaration,
                IsExempt = annotation.ExemptReason is not null,
                Exemption = annotation.ExemptReason is not null ? "DeclaredInSource" : "None",
                Fingerprint = null,
                Annotation = annotation,
                State = AssuranceStateMachine.Resolve(annotation, isExemptByPredicate: false, currentFingerprint: null),
            });
        }

        return units;
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=65ECB4
    // Hairs-Falsified-If: a block separated from the declaration by an attribute line that is not the declaration's own, such as an assembly attribute, is attached to that declaration
    // Hairs-Human:        PENDING
    private static AssuranceAnnotation? AnnotationAbove(AssuranceLines lines, int declarationLine)
    {
        AssuranceAnnotation? found = null;

        for (int line = declarationLine - 1; line >= 0; line--)
        {
            string trimmed = lines[line].TrimStart();

            if (trimmed.StartsWith(AssuranceVocabulary.AiMarker, StringComparison.Ordinal))
            {
                if (AssuranceAnnotation.TryParse(lines, line, out AssuranceAnnotation? annotation))
                    found = annotation;

                continue;
            }

            if (trimmed.Length == 0 ||
                trimmed.StartsWith("//", StringComparison.Ordinal) ||
                trimmed.StartsWith("/*", StringComparison.Ordinal) ||
                trimmed.StartsWith('*') ||
                trimmed.StartsWith('#') ||
                trimmed.StartsWith('['))
            {
                continue;
            }

            break;
        }

        return found;
    }

    /// <summary>
    /// A name for the declaration under an annotation, worked out from the text.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=2; Fingerprint=283F26
    // Hairs-Falsified-If: a declaration preceded by an attribute line is named after the attribute
    // Hairs-Human:        PENDING
    private static string DeclarationName(AssuranceLines lines, int declarationLine)
    {
        for (int line = declarationLine; line < lines.Count && line <= declarationLine + 3; line++)
        {
            string trimmed = lines[line].Trim();

            if (trimmed.Length == 0 || trimmed.StartsWith('[') || trimmed.StartsWith("//", StringComparison.Ordinal))
                continue;

            return Shorten(trimmed);
        }

        return string.Create(CultureInfo.InvariantCulture, $"line {declarationLine + 1}");
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=2; Fingerprint=4E0307
    // Hairs-Falsified-If: a declaration longer than 72 characters comes back longer than 72 characters
    // Hairs-Human:        PENDING
    private static string Shorten(string declaration)
    {
        string text = declaration.TrimEnd('{', ' ', '\t');
        int arrow = text.IndexOf("=>", StringComparison.Ordinal);
        if (arrow > 0)
            text = text[..arrow];

        text = text.TrimEnd();
        return text.Length <= 72 ? text : text[..71] + "…";
    }
}
