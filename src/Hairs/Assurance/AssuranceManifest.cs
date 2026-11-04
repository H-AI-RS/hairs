// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0
//
// HAIRS Code Assurance
// --------------------
// Relevant units:   21
// Annotated:        21/21
// Exempt:           0
// Human-reviewed:   0/21
// IP risk:          Low
// Security risk:    High
// Criteria:         15/10
// Resource impact:  5/10 max
// Unverified:       21
//
// GENERATED - DO NOT EDIT MANUALLY

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Hairs.Assurance;

/// <summary>One unit entry of the manifest.</summary>
// Hairs-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=B25853
// Hairs-Human:        PENDING
public sealed record AssuranceManifestEntry(string Name, string File, bool Exempt, string Exemption, string Fingerprint);

/// <summary>One file entry of the manifest: the fingerprint over the file's whole token stream.</summary>
// Hairs-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=0CA434
// Hairs-Human:        PENDING
public sealed record AssuranceManifestFile(string File, string Fingerprint);

/// <summary>
/// <c>hairs.manifest.json</c> (or <c>assurance.manifest.json</c>): every covered file with a fingerprint over
/// all of it, and every code unit in them, exempt and relevant alike, with a
/// fingerprint of its own.
/// </summary>
// Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=5; Fingerprint=2C1E3B
// Hairs-Falsified-If: a manifest whose recorded unit or file fingerprint differs from the value the tree computes now produces no J7 violation
// Hairs-Human:        PENDING
public static class AssuranceManifest
{
    /// <summary>The sentence every description of the manifest carries, so no reader takes an entry for a review.</summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=None; Resources=0; Fingerprint=C40271
    // Hairs-Human:        PENDING
    public const string ChangeDetectionStatement = "This manifest is a change-detection record, not a review.";

    /// <summary>The sentence that states what the file entries add.</summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=None; Resources=0; Fingerprint=C9C2C5
    // Hairs-Human:        PENDING
    public const string CompletenessStatement =
        "Nothing in a covered file can change without something moving here, whatever kind of declaration it is.";

    // Hairs-AI:           Origin=AI; IP=None; Security=High; Resources=0; Fingerprint=1EED07
    // Hairs-Falsified-If: the opening text matches a generated manifest at a position other than the start of its files array
    // Hairs-Human:        PENDING
    private const string FilesArrayOpening = "\n  \"files\": [";

    /// <summary>The <c>$comment</c> lines, naming this component's commands.</summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=2; Fingerprint=628000
    // Hairs-Falsified-If: the comment for a single covered assembly speaks of '1 covered assemblies'
    // Hairs-Human:        PENDING
    public static IReadOnlyList<string> Comment(string generateCommand, string checkCommand, int assemblies)
    {
        ArgumentNullException.ThrowIfNull(generateCommand);
        ArgumentNullException.ThrowIfNull(checkCommand);

        string covered = assemblies == 1
            ? "the covered assembly is"
            : string.Create(CultureInfo.InvariantCulture, $"the {assemblies} covered assemblies is");

        return
        [
            "GENERATED - DO NOT EDIT MANUALLY. Regenerate with",
            $"`{generateCommand}`.",
            string.Empty,
            ChangeDetectionStatement + $" Every code unit in {covered} listed",
            "here, exempt and relevant alike, with the fingerprint of its declaration. An entry",
            "records what that declaration's tokens hashed to when the generator last ran. It is not",
            "an assessment, it is not an approval, and it is not evidence that anyone has read the",
            "unit. Exempt units need no annotation.",
            string.Empty,
            "The 'files' array beside the units is what makes this record COMPLETE. A unit entry exists",
            "only for a declaration kind the scanner enumerates, and that enumeration is a whitelist: an",
            "assembly-level attribute is a member of nothing and can be in no unit at all. Each file",
            "entry is a fingerprint over the complete token stream of that file's compilation unit.",
            CompletenessStatement,
            string.Empty,
            "What the manifest adds is that a unit the exemption predicate treats as trivial is no",
            "longer invisible: a semantic change to one moves a fingerprint in a generated file the",
            "check compares byte for byte, so the change appears in a diff and fails an unregenerated",
            $"tree. `{checkCommand}` holds this file to the tree - every unit and every covered file",
            "present, no extras, every fingerprint current.",
        ];
    }

    /// <summary>The unit entries <paramref name="units"/> imply, in the order they are written.</summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=7434AC
    // Hairs-Falsified-If: an exempt unit of the tree has no entry, or two units are ordered by line rather than by file, name and fingerprint
    // Hairs-Human:        PENDING
    public static IReadOnlyList<AssuranceManifestEntry> Entries(IEnumerable<AssuranceCorpusUnit> units)
    {
        ArgumentNullException.ThrowIfNull(units);

        return [.. units
            .Select(static unit => new AssuranceManifestEntry(
                unit.Name, unit.File, unit.IsExempt, unit.Exemption, unit.Fingerprint))
            .OrderBy(static entry => entry.File, StringComparer.Ordinal)
            .ThenBy(static entry => entry.Name, StringComparer.Ordinal)
            .ThenBy(static entry => entry.Fingerprint, StringComparer.Ordinal)];
    }

    /// <summary>The manifest as it is written to disk. Deterministic, LF, no trailing whitespace.</summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=07AA9A
    // Hairs-Falsified-If: a manifest rendered from files and units does not read back through the unit and file readers to the same names, files and fingerprints
    // Hairs-Human:        PENDING
    public static string Render(
        IReadOnlyList<string> comment,
        IEnumerable<AssuranceManifestFile> files,
        IEnumerable<AssuranceCorpusUnit> units)
    {
        ArgumentNullException.ThrowIfNull(comment);
        ArgumentNullException.ThrowIfNull(files);

        IReadOnlyList<AssuranceManifestEntry> entries = Entries(units);
        List<AssuranceManifestFile> covered = [.. files.OrderBy(static file => file.File, StringComparer.Ordinal)];
        var json = new StringBuilder();

        json.Append("{\n  \"$comment\": [\n");
        for (int line = 0; line < comment.Count; line++)
        {
            json.Append("    \"").Append(Escape(comment[line])).Append('"');
            json.Append(line == comment.Count - 1 ? "\n" : ",\n");
        }

        json.Append("  ],\n");
        json.Append("  \"files\": [\n");
        for (int index = 0; index < covered.Count; index++)
        {
            json.Append("    {");
            json.Append(" \"file\": \"").Append(Escape(covered[index].File)).Append("\",");
            json.Append(" \"fingerprint\": \"").Append(Escape(covered[index].Fingerprint)).Append("\" }");
            json.Append(index == covered.Count - 1 ? "\n" : ",\n");
        }

        json.Append("  ],\n");
        json.Append("  \"units\": [\n");
        for (int index = 0; index < entries.Count; index++)
        {
            AssuranceManifestEntry entry = entries[index];
            json.Append("    {");
            json.Append(" \"name\": \"").Append(Escape(entry.Name)).Append("\",");
            json.Append(" \"file\": \"").Append(Escape(entry.File)).Append("\",");
            json.Append(" \"exempt\": ").Append(entry.Exempt ? "true" : "false").Append(',');
            json.Append(" \"exemption\": \"").Append(Escape(entry.Exemption)).Append("\",");
            json.Append(" \"fingerprint\": \"").Append(Escape(entry.Fingerprint)).Append("\" }");
            json.Append(index == entries.Count - 1 ? "\n" : ",\n");
        }

        json.Append("  ]\n}\n");
        return json.ToString();
    }

    /// <summary>
    /// The part of a manifest after its <c>$comment</c>: the <c>files</c> and
    /// <c>units</c> arrays, as text. Null when the text has no <c>files</c>
    /// array where this layout puts it. For comparing two manifests whose
    /// prose differs by owner.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=D5575B
    // Hairs-Falsified-If: two manifests whose files or units arrays differ return equal strings
    // Hairs-Human:        PENDING
    public static string? ArraysOf(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        int at = text.IndexOf(FilesArrayOpening, StringComparison.Ordinal);
        return at < 0 ? null : text[(at + 1)..];
    }

    /// <summary>
    /// Every disagreement between a manifest text and the tree: missing,
    /// extra, stale and duplicate unit and file entries, and anything at the
    /// top of the manifest beside its three properties.
    /// </summary>
    /// <param name="manifestPath">The manifest, for messages.</param>
    /// <param name="files">The covered files and their fingerprints.</param>
    /// <param name="units">Every unit of the tree.</param>
    /// <param name="manifestText">The manifest as it is on disk; empty when it does not exist.</param>
    /// <param name="remedy">The command that rewrites the manifest, for every violation it resolves.</param>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=5; Fingerprint=4AFDF1
    // Hairs-Falsified-If: a manifest entry whose fingerprint differs from the one its unit computes now produces no J7 violation
    // Hairs-Human:        PENDING
    public static IReadOnlyList<AssuranceViolation> Violations(
        string manifestPath,
        IEnumerable<AssuranceManifestFile> files,
        IEnumerable<AssuranceCorpusUnit> units,
        string manifestText,
        string? remedy = null)
    {
        ArgumentNullException.ThrowIfNull(manifestPath);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(manifestText);

        var violations = new List<AssuranceViolation>();
        var expected = new Dictionary<(string File, string Name), (AssuranceManifestEntry Entry, int Line)>();

        AssuranceViolation Whole(string message) => new("J7", manifestPath, null, message) { Remedy = remedy };

        AssuranceViolation AtUnit(string file, int line, string message) => new("J7", file, line, message) { Remedy = remedy };

        foreach ((AssuranceManifestEntry entry, int line) in units
            .Select(static unit => (Entry: Entry(unit), unit.Line))
            .OrderBy(static unit => unit.Entry.File, StringComparer.Ordinal)
            .ThenBy(static unit => unit.Entry.Name, StringComparer.Ordinal)
            .ThenBy(static unit => unit.Entry.Fingerprint, StringComparer.Ordinal))
        {
            if (!expected.TryAdd((entry.File, entry.Name), (entry, line)))
            {
                violations.Add(new AssuranceViolation(
                    "J7",
                    entry.File,
                    line,
                    $"{entry.File} declares more than one unit named {entry.Name}, and a manifest " +
                    "entry addresses a unit by its file and its name"));
            }
        }

        var messages = new List<string>();
        List<AssuranceManifestEntry>? read = ReadUnits(manifestPath, manifestText, messages);
        violations.AddRange(messages.Select(Whole));
        if (read is null)
            return violations;

        var recorded = new Dictionary<(string File, string Name), AssuranceManifestEntry>();
        foreach (AssuranceManifestEntry entry in read)
        {
            if (!recorded.TryAdd((entry.File, entry.Name), entry))
                violations.Add(Whole($"{manifestPath} carries more than one entry for {entry.Name} in {entry.File}"));
        }

        foreach (((string File, string Name) key, (AssuranceManifestEntry entry, int line)) in expected.OrderBy(static pair => pair.Key, KeyOrder.Instance))
        {
            if (!recorded.TryGetValue(key, out AssuranceManifestEntry? found))
            {
                violations.Add(AtUnit(entry.File, line,
                    $"{entry.File}: {entry.Name} is a code unit in the product tree and " +
                    $"{manifestPath} does not cover it, so nothing records a change to it"));
                continue;
            }

            if (!string.Equals(found.Fingerprint, entry.Fingerprint, StringComparison.Ordinal))
            {
                violations.Add(AtUnit(entry.File, line,
                    $"{entry.File}: {entry.Name} is recorded in {manifestPath} as " +
                    $"{found.Fingerprint} and the current code computes {entry.Fingerprint}"));
            }

            if (!string.Equals(found.Exemption, entry.Exemption, StringComparison.Ordinal) || found.Exempt != entry.Exempt)
            {
                violations.Add(AtUnit(entry.File, line,
                    $"{entry.File}: {entry.Name} is recorded in {manifestPath} as {found.Exemption} " +
                    $"and the predicate answers {entry.Exemption}"));
            }
        }

        foreach (((string File, string Name) key, AssuranceManifestEntry entry) in recorded.OrderBy(static pair => pair.Key, KeyOrder.Instance))
        {
            if (!expected.ContainsKey(key))
            {
                violations.Add(Whole(
                    $"{entry.File}: {manifestPath} carries an entry for {entry.Name}, which is not " +
                    "a code unit in the product tree"));
            }
        }

        foreach ((string? file, string message) in FileViolations(manifestPath, files, manifestText))
        {
            violations.Add(file is null
                ? Whole(message)
                : new AssuranceViolation("J7", file, null, message) { Remedy = remedy });
        }

        violations.AddRange(ShapeViolations(manifestPath, manifestText).Select(Whole));
        return violations;
    }

    // Hairs-AI:           Origin=AI; IP=None; Security=High; Resources=1; Fingerprint=CD42F7
    // Hairs-Falsified-If: the expected entry for a unit carries a fingerprint, exempt flag or exemption other than the unit's own, so a changed unit matches its old manifest entry
    // Hairs-Human:        PENDING
    private static AssuranceManifestEntry Entry(AssuranceCorpusUnit unit) =>
        new(unit.Name, unit.File, unit.IsExempt, unit.Exemption, unit.Fingerprint);

    /// <summary>
    /// Anything at the top of the manifest but its three properties, in their
    /// order, and a <c>$comment</c> that is not an array of strings.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=4; Fingerprint=0489BC
    // Hairs-Falsified-If: a manifest with a fourth top-level property, or with its three properties in another order, yields no shape message
    // Hairs-Human:        PENDING
    private static IEnumerable<string> ShapeViolations(string manifestPath, string text)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            yield break;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                yield break;

            string[] names = [.. document.RootElement.EnumerateObject().Select(static property => property.Name)];
            string[] layout = ["$comment", "files", "units"];
            if (!names.SequenceEqual(layout, StringComparer.Ordinal))
            {
                yield return $"{manifestPath} carries the top-level properties {string.Join(", ", names)}; " +
                    $"the generator writes {string.Join(", ", layout)}, in that order, and nothing else";
            }

            if (document.RootElement.TryGetProperty("$comment", out JsonElement comment) &&
                (comment.ValueKind != JsonValueKind.Array ||
                 comment.EnumerateArray().Any(static line => line.ValueKind != JsonValueKind.String)))
            {
                yield return $"{manifestPath} carries a '$comment' that is not an array of strings";
            }
        }
    }

    /// <summary>
    /// JSON string escaping, written out: <c>"</c>, <c>\</c>, the three
    /// common controls by name, every other control as <c>\uXXXX</c>, and
    /// everything else verbatim.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=A27206
    // Hairs-Falsified-If: a name or path containing a quote, a backslash or a control character is written so that parsing the manifest back yields a different string
    // Hairs-Human:        PENDING
    public static string Escape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var escaped = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            switch (character)
            {
                case '"': escaped.Append("\\\""); break;
                case '\\': escaped.Append("\\\\"); break;
                case '\n': escaped.Append("\\n"); break;
                case '\r': escaped.Append("\\r"); break;
                case '\t': escaped.Append("\\t"); break;
                default:
                    if (character < ' ')
                        escaped.Append("\\u").Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
                    else
                        escaped.Append(character);

                    break;
            }
        }

        return escaped.ToString();
    }

    /// <summary>
    /// The file-entry disagreements, each with the covered file it is about,
    /// or null when it is about the manifest itself.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=4; Fingerprint=FAEA34
    // Hairs-Falsified-If: a covered file whose recorded file fingerprint differs from the one it computes now produces no message
    // Hairs-Human:        PENDING
    private static List<(string? File, string Message)> FileViolations(
        string manifestPath, IEnumerable<AssuranceManifestFile> files, string manifestText)
    {
        var found = new List<(string?, string)>();
        var expected = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (AssuranceManifestFile file in files)
            expected[file.File] = file.Fingerprint;

        var messages = new List<string>();
        List<AssuranceManifestFile>? read = ReadFiles(manifestPath, manifestText, messages);
        found.AddRange(messages.Select(static message => ((string?)null, message)));
        if (read is null)
            return found;

        var recorded = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (AssuranceManifestFile entry in read)
        {
            if (!recorded.TryAdd(entry.File, entry.Fingerprint))
                found.Add((null, $"{manifestPath} carries more than one file entry for {entry.File}"));
        }

        foreach ((string file, string fingerprint) in expected.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (!recorded.TryGetValue(file, out string? recordedFingerprint))
            {
                found.Add((file,
                    $"{file} is a covered file and {manifestPath} records no fingerprint for it, so " +
                    "nothing watches the whole of it"));
                continue;
            }

            if (!string.Equals(recordedFingerprint, fingerprint, StringComparison.Ordinal))
            {
                found.Add((file,
                    $"{file} is recorded in {manifestPath} as file fingerprint {recordedFingerprint} and the " +
                    $"current file computes {fingerprint}"));
            }
        }

        foreach ((string file, _) in recorded.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (!expected.ContainsKey(file))
                found.Add((null, $"{manifestPath} carries a file entry for {file}, which is not a covered file"));
        }

        return found;
    }

    /// <summary>The unit entries, or null (with the reason added) when there are none to read.</summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=4; Fingerprint=4C8862
    // Hairs-Falsified-If: a manifest whose units member is missing or is not an array is read as an empty list of entries instead of being reported
    // Hairs-Human:        PENDING
    private static List<AssuranceManifestEntry>? ReadUnits(string manifestPath, string text, List<string> messages)
    {
        var entries = new List<AssuranceManifestEntry>();
        if (text.Length == 0)
        {
            messages.Add($"{manifestPath} is absent or empty, so no unit is covered at all");
            return null;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException problem)
        {
            messages.Add($"{manifestPath} does not parse as JSON: {problem.Message}");
            return null;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("units", out JsonElement units) ||
                units.ValueKind != JsonValueKind.Array)
            {
                messages.Add($"{manifestPath} carries no 'units' array");
                return null;
            }

            foreach (JsonElement unit in units.EnumerateArray())
            {
                entries.Add(new AssuranceManifestEntry(
                    Text(unit, "name"),
                    Text(unit, "file"),
                    unit.ValueKind == JsonValueKind.Object &&
                        unit.TryGetProperty("exempt", out JsonElement exempt) && exempt.ValueKind == JsonValueKind.True,
                    Text(unit, "exemption"),
                    Text(unit, "fingerprint")));
            }
        }

        return entries;
    }

    /// <summary>The file entries, or null (with the reason added) when there are none to read.</summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=4; Fingerprint=EC7686
    // Hairs-Falsified-If: a manifest whose files member is missing or is not an array is read as an empty list of file entries instead of being reported
    // Hairs-Human:        PENDING
    private static List<AssuranceManifestFile>? ReadFiles(string manifestPath, string text, List<string> messages)
    {
        var entries = new List<AssuranceManifestFile>();

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("files", out JsonElement files) ||
                files.ValueKind != JsonValueKind.Array)
            {
                messages.Add($"{manifestPath} carries no 'files' array, so no covered file is watched whole");
                return null;
            }

            foreach (JsonElement file in files.EnumerateArray())
                entries.Add(new AssuranceManifestFile(Text(file, "file"), Text(file, "fingerprint")));
        }

        return entries;
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=C812A6
    // Hairs-Falsified-If: a property holding a JSON number, null, array or object is read as anything other than the empty string
    // Hairs-Human:        PENDING
    private static string Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    /// <summary>
    /// Ordinal order over (file, name).
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=EEF520
    // Hairs-Human:        PENDING
    private sealed class KeyOrder : IComparer<(string File, string Name)>
    {
        // Hairs-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=BC6032
        // Hairs-Human:        PENDING
        public static readonly KeyOrder Instance = new();

        // Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=2C32B2
        // Hairs-Falsified-If: two keys with the same file are ordered by something other than the ordinal order of their names
        // Hairs-Human:        PENDING
        public int Compare((string File, string Name) x, (string File, string Name) y)
        {
            int file = string.CompareOrdinal(x.File, y.File);
            return file != 0 ? file : string.CompareOrdinal(x.Name, y.Name);
        }
    }
}
