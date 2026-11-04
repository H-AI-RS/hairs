// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hairs;

/// <summary>
/// Deterministic JSON serialization and deserialization for FileReview records.
/// </summary>
public static class ReviewJson
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    public static string Write(FileReview review)
    {
        ArgumentNullException.ThrowIfNull(review);

        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();

            writer.WriteNumber("version", Math.Max(review.Version, FileReview.CurrentVersion));
            writer.WriteString("file", review.Path);
            writer.WriteString("status", ToWire(review.Status));

            if (review.Reviewer.Length > 0)
                writer.WriteString("reviewer", review.Reviewer);
            if (review.ReviewedAt is { } at)
                writer.WriteString("reviewedAt", Stamp(at));
            if (review.ReviewedContentHash is { Length: > 0 } hash)
                writer.WriteString("reviewedContentHash", hash);
            if (review.ReviewedRevision is { Length: > 0 } revision)
                writer.WriteString("reviewedRevision", revision);

            if (review.Notes.Count > 0)
            {
                writer.WriteStartArray("notes");
                foreach (ReviewNote note in review.Notes)
                    WriteNote(writer, note);
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray()) + "\n";
    }

    public static FileReview? Read(string json, string path)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(path);

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }

        if (root is not JsonObject document)
            return null;

        var notes = new List<ReviewNote>();
        if (document["notes"] is JsonArray array)
        {
            var taken = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonNode? element in array)
            {
                if (element is JsonObject note && GetString(note, "id") is { Length: > 0 } id)
                    taken.Add(id);
            }

            int next = 1;
            foreach (JsonNode? element in array)
            {
                if (element is not JsonObject note)
                    continue;

                string? id = GetString(note, "id");
                if (id is not { Length: > 0 })
                {
                    while (!taken.Add(id = string.Create(CultureInfo.InvariantCulture, $"n{next}")))
                        next++;
                }

                if (ReadNote(note, id) is { } parsed)
                    notes.Add(parsed);
            }
        }

        return new FileReview
        {
            Path = path,
            Version = GetInt(document, "version") ?? FileReview.CurrentVersion,
            Status = FromWire(GetString(document, "status")),
            Reviewer = GetString(document, "reviewer") ?? string.Empty,
            ReviewedAt = GetTimestamp(document, "reviewedAt"),
            ReviewedContentHash = GetString(document, "reviewedContentHash"),
            ReviewedRevision = GetString(document, "reviewedRevision"),
            Notes = notes,
        };
    }

    private static string Stamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture);

    private static void WriteNote(Utf8JsonWriter writer, ReviewNote note)
    {
        writer.WriteStartObject();
        writer.WriteString("id", note.Id);
        writer.WriteString("kind", ToWire(note.Kind));
        writer.WriteString("author", note.Author);
        writer.WriteString("createdAt", Stamp(note.CreatedAt));
        writer.WriteString("text", note.Text);

        if (!note.Anchor.IsFileLevel)
        {
            writer.WriteStartObject("anchor");
            writer.WriteNumber("startLine", note.Anchor.StartLine + 1);
            writer.WriteNumber("endLine", note.Anchor.EndLine + 1);
            if (note.Anchor.Symbol is { Length: > 0 } symbol)
                writer.WriteString("symbol", symbol);
            writer.WriteString("text", note.Anchor.AnchorText);
            writer.WriteEndObject();
        }

        if (note.Resolution is { } resolution)
        {
            writer.WriteStartObject("resolution");
            writer.WriteString("resolvedAt", Stamp(resolution.ResolvedAt));
            writer.WriteString("resolvedBy", resolution.ResolvedBy);
            writer.WriteString("text", resolution.Text);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static ReviewNote? ReadNote(JsonObject note, string id)
    {
        string? text = GetString(note, "text");
        if (text is null)
            return null;

        ReviewAnchor anchor = ReviewAnchor.File;
        if (note["anchor"] is JsonObject anchorNode)
        {
            int start = Math.Max(0, (GetInt(anchorNode, "startLine") ?? 1) - 1);
            int end = Math.Max(start, (GetInt(anchorNode, "endLine") ?? (start + 1)) - 1);
            anchor = new ReviewAnchor(
                start, end, GetString(anchorNode, "text") ?? string.Empty, GetString(anchorNode, "symbol"));
        }

        ReviewResolution? resolution = null;
        if (note["resolution"] is JsonObject resolved)
        {
            resolution = new ReviewResolution(
                GetTimestamp(resolved, "resolvedAt") ?? default,
                GetString(resolved, "resolvedBy") ?? string.Empty,
                GetString(resolved, "text") ?? string.Empty);
        }

        return new ReviewNote
        {
            Id = id,
            Kind = FromWireKind(GetString(note, "kind")),
            Author = GetString(note, "author") ?? string.Empty,
            CreatedAt = GetTimestamp(note, "createdAt") ?? default,
            Text = text,
            Anchor = anchor,
            Resolution = resolution,
        };
    }

    private static string ToWire(ReviewStatus status) => status switch
    {
        ReviewStatus.InReview => "in-review",
        ReviewStatus.Reviewed => "reviewed",
        ReviewStatus.Question => "question",
        ReviewStatus.NeedsChange => "needs-change",
        _ => "unreviewed",
    };

    private static ReviewStatus FromWire(string? wire) => wire switch
    {
        "in-review" => ReviewStatus.InReview,
        "reviewed" => ReviewStatus.Reviewed,
        "question" => ReviewStatus.Question,
        "needs-change" => ReviewStatus.NeedsChange,
        _ => ReviewStatus.Unreviewed,
    };

    private static string ToWire(ReviewNoteKind kind) => kind switch
    {
        ReviewNoteKind.Concern => "concern",
        ReviewNoteKind.Todo => "todo",
        ReviewNoteKind.Observation => "observation",
        _ => "question",
    };

    private static ReviewNoteKind FromWireKind(string? wire) => wire switch
    {
        "concern" => ReviewNoteKind.Concern,
        "todo" => ReviewNoteKind.Todo,
        "observation" => ReviewNoteKind.Observation,
        _ => ReviewNoteKind.Question,
    };

    private static string? GetString(JsonObject node, string name) =>
        node[name] is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    private static int? GetInt(JsonObject node, string name) =>
        node[name] is JsonValue value && value.TryGetValue(out int number) ? number : null;

    private static DateTimeOffset? GetTimestamp(JsonObject node, string name) =>
        GetString(node, name) is { } text &&
        DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out DateTimeOffset value)
            ? value
            : null;
}
