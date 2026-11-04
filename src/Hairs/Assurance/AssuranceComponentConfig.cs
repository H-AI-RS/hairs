// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Hairs.Assurance;

public enum AssuranceMode
{
    Owned = 0,
    External,
}

public enum AssuranceForgeryVocabulary
{
    Narrow = 0,
    Strict,
}

public enum AssuranceExemptionPredicate
{
    Strict = 0,
    OwningComponent,
}

public enum AssuranceNamedValues
{
    Reviewed = 0,
    Watched,
}

public enum AssuranceCriteriaBelowHigh
{
    Permitted = 0,
    Refused,
}

public sealed record AssuranceExclusion(AssuranceGlob Glob, string Reason);

public sealed record AssuranceSpdx(IReadOnlyList<string> Copyright, string License);

public sealed record AssuranceSpdxOverride(AssuranceGlob Glob, AssuranceSpdx Spdx);

public sealed record AssuranceArtefactPaths
{
    public string Report { get; init; } = "CODE-ASSURANCE.md";
    public string HumanReview { get; init; } = "HUMAN_REVIEW.md";
    public string Manifest { get; init; } = "hairs.manifest.json";
}

public sealed class AssuranceConfigException : Exception
{
    public AssuranceConfigException(string message)
        : base(message)
    {
    }

    public AssuranceConfigException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

public sealed record AssuranceComponentConfig
{
    public const string FileName = "hairs.config.json";
    public const string LegacyFileName = "assurance.config.json";
    public const int CurrentSchema = 1;

    public string Component { get; init; } = string.Empty;
    public AssuranceMode Mode { get; init; } = AssuranceMode.Owned;
    public IReadOnlyList<string> Projects { get; init; } = [];
    public IReadOnlyList<AssuranceExclusion> Exclude { get; init; } = [];
    public bool ExcludeBuildOutputAtAnyDepth { get; init; } = true;
    public AssuranceExemptionPredicate ExemptionPredicate { get; init; } = AssuranceExemptionPredicate.Strict;
    public AssuranceNamedValues NamedValues { get; init; } = AssuranceNamedValues.Reviewed;
    public AssuranceCriteriaBelowHigh CriteriaBelowHigh { get; init; } = AssuranceCriteriaBelowHigh.Permitted;
    public AssuranceSpdx? Spdx { get; init; }
    public IReadOnlyList<AssuranceSpdxOverride> SpdxOverrides { get; init; } = [];
    public AssuranceArtefactPaths Artefacts { get; init; } = new();
    public IReadOnlyList<string>? PreprocessorSymbols { get; init; }
    public bool ForbidDirectives { get; init; }
    public AssuranceForgeryVocabulary ForgeryVocabulary { get; init; } = AssuranceForgeryVocabulary.Narrow;
    public IReadOnlyList<string> ClosedToEscapeHatch { get; init; } = [];
    public string AdrDirectory { get; init; } = "docs/adr";
    public string? RegenerateCommand { get; init; }
    public IReadOnlyList<string>? ManifestComment { get; init; }

    public AssuranceSpdx? SpdxFor(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        foreach (AssuranceSpdxOverride entry in SpdxOverrides)
        {
            if (entry.Glob.IsMatch(relativePath))
                return entry.Spdx;
        }

        return Spdx;
    }

    public AssuranceExclusion? ExclusionFor(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        foreach (AssuranceExclusion exclusion in Exclude)
        {
            if (exclusion.Glob.IsMatch(relativePath))
                return exclusion;
        }

        return null;
    }

    public static AssuranceComponentConfig Parse(string json)
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
            throw new AssuranceConfigException($"{FileName} is not valid JSON: {exception.Message}", exception);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw Error("$", "must be an object");

            var config = new AssuranceComponentConfig();
            bool sawProjects = false;

            foreach (JsonProperty property in root.EnumerateObject())
            {
                string path = "$." + property.Name;
                JsonElement value = property.Value;

                switch (property.Name)
                {
                    case "$comment":
                        break;

                    case "schema":
                        if (value.ValueKind != JsonValueKind.Number ||
                            !value.TryGetInt32(out int schema) ||
                            schema != CurrentSchema)
                        {
                            throw Error(path, $"must be {CurrentSchema}; this build reads no other schema");
                        }
                        break;

                    case "component":
                        config = config with { Component = SingleLine(value, path) };
                        break;

                    case "mode":
                        config = config with
                        {
                            Mode = RequiredString(value, path) switch
                            {
                                "owned" => AssuranceMode.Owned,
                                "external" => AssuranceMode.External,
                                _ => throw Error(path, "must be \"owned\" or \"external\""),
                            },
                        };
                        break;

                    case "projects":
                        sawProjects = true;
                        config = config with { Projects = ProjectList(value, path) };
                        break;

                    case "exclude":
                        config = config with { Exclude = Exclusions(value, path) };
                        break;

                    case "excludeBuildOutputAtAnyDepth":
                        config = config with { ExcludeBuildOutputAtAnyDepth = RequiredBool(value, path) };
                        break;

                    case "exemptionPredicate":
                        config = config with
                        {
                            ExemptionPredicate = RequiredString(value, path) switch
                            {
                                "strict" => AssuranceExemptionPredicate.Strict,
                                "owning-component" => AssuranceExemptionPredicate.OwningComponent,
                                _ => throw Error(path, "must be \"strict\" or \"owning-component\""),
                            },
                        };
                        break;

                    case "namedValues":
                        config = config with
                        {
                            NamedValues = RequiredString(value, path) switch
                            {
                                "reviewed" => AssuranceNamedValues.Reviewed,
                                "watched" => AssuranceNamedValues.Watched,
                                _ => throw Error(path, "must be \"reviewed\" or \"watched\""),
                            },
                        };
                        break;

                    case "criteriaBelowHigh":
                        config = config with
                        {
                            CriteriaBelowHigh = RequiredString(value, path) switch
                            {
                                "permitted" => AssuranceCriteriaBelowHigh.Permitted,
                                "refused" => AssuranceCriteriaBelowHigh.Refused,
                                _ => throw Error(path, "must be \"permitted\" or \"refused\""),
                            },
                        };
                        break;

                    case "spdx":
                        config = config with { Spdx = SpdxOf(value, path, allowGlob: false).Spdx };
                        break;

                    case "spdxOverrides":
                        config = config with { SpdxOverrides = Overrides(value, path) };
                        break;

                    case "artefacts":
                        config = config with { Artefacts = ArtefactPaths(value, path) };
                        break;

                    case "preprocessorSymbols":
                        config = config with { PreprocessorSymbols = Symbols(value, path) };
                        break;

                    case "forbidDirectives":
                        config = config with { ForbidDirectives = RequiredBool(value, path) };
                        break;

                    case "forgeryVocabulary":
                        config = config with
                        {
                            ForgeryVocabulary = RequiredString(value, path) switch
                            {
                                "narrow" => AssuranceForgeryVocabulary.Narrow,
                                "strict" => AssuranceForgeryVocabulary.Strict,
                                _ => throw Error(path, "must be \"narrow\" or \"strict\""),
                            },
                        };
                        break;

                    case "closedToEscapeHatch":
                        config = config with { ClosedToEscapeHatch = StringList(value, path) };
                        break;

                    case "adrDirectory":
                        config = config with { AdrDirectory = RelativePath(value, path) };
                        break;

                    case "regenerateCommand":
                        config = config with { RegenerateCommand = SingleLine(value, path) };
                        break;

                    case "manifestComment":
                        config = config with { ManifestComment = CommentLines(value, path) };
                        break;

                    default:
                        throw Error(path, "is not a property this schema defines");
                }
            }

            if (!sawProjects || config.Projects.Count == 0)
                throw Error("$.projects", "must list at least one product project");

            return config;
        }
    }

    private static IReadOnlyList<string> ProjectList(JsonElement value, string path)
    {
        IReadOnlyList<string> projects = StringList(value, path);
        for (int index = 0; index < projects.Count; index++)
        {
            string project = CheckRelative(projects[index], $"{path}[{index}]");
            if (!project.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                throw Error($"{path}[{index}]", $"'{project}' is not a .csproj file");
        }

        if (projects.Distinct(StringComparer.OrdinalIgnoreCase).Count() != projects.Count)
            throw Error(path, "names a project twice");

        return projects;
    }

    private static IReadOnlyList<AssuranceExclusion> Exclusions(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Array)
            throw Error(path, "must be an array");

        var exclusions = new List<AssuranceExclusion>();
        int index = 0;
        foreach (JsonElement entry in value.EnumerateArray())
        {
            string at = $"{path}[{index++}]";
            string pattern;
            string reason = $"excluded by {FileName}";

            if (entry.ValueKind == JsonValueKind.String)
            {
                pattern = entry.GetString()!;
            }
            else if (entry.ValueKind == JsonValueKind.Object)
            {
                string? glob = null;
                foreach (JsonProperty property in entry.EnumerateObject())
                {
                    switch (property.Name)
                    {
                        case "glob":
                            glob = RequiredString(property.Value, $"{at}.glob");
                            break;
                        case "reason":
                            reason = SingleLine(property.Value, $"{at}.reason");
                            break;
                        default:
                            throw Error($"{at}.{property.Name}", "is not a property this schema defines");
                    }
                }

                pattern = glob ?? throw Error(at, "has no \"glob\"");
            }
            else
            {
                throw Error(at, "must be a glob string or {\"glob\", \"reason\"}");
            }

            exclusions.Add(new AssuranceExclusion(Glob(pattern, at), reason));
        }

        return exclusions;
    }

    private static IReadOnlyList<AssuranceSpdxOverride> Overrides(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Array)
            throw Error(path, "must be an array");

        var overrides = new List<AssuranceSpdxOverride>();
        int index = 0;
        foreach (JsonElement entry in value.EnumerateArray())
        {
            string at = $"{path}[{index++}]";
            (AssuranceSpdx spdx, AssuranceGlob? glob) = SpdxOf(entry, at, allowGlob: true);
            overrides.Add(new AssuranceSpdxOverride(glob ?? throw Error(at, "has no \"glob\""), spdx));
        }

        return overrides;
    }

    private static (AssuranceSpdx Spdx, AssuranceGlob? Glob) SpdxOf(JsonElement value, string path, bool allowGlob)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw Error(path, "must be an object");

        IReadOnlyList<string>? copyright = null;
        string? license = null;
        AssuranceGlob? glob = null;

        foreach (JsonProperty property in value.EnumerateObject())
        {
            string at = $"{path}.{property.Name}";
            switch (property.Name)
            {
                case "copyright":
                    copyright = StringList(property.Value, at);
                    for (int index = 0; index < copyright.Count; index++)
                    {
                        string line = CheckSingleLine(copyright[index], $"{at}[{index}]");
                        if (line.StartsWith("//", StringComparison.Ordinal) ||
                            line.StartsWith("SPDX-", StringComparison.OrdinalIgnoreCase))
                        {
                            throw Error($"{at}[{index}]", "is the value only, without '// SPDX-FileCopyrightText:'");
                        }
                    }
                    break;

                case "license":
                    license = SingleLine(property.Value, at);
                    break;

                case "glob" when allowGlob:
                    glob = Glob(RequiredString(property.Value, at), at);
                    break;

                default:
                    throw Error(at, "is not a property this schema defines");
            }
        }

        if (copyright is null || copyright.Count == 0)
            throw Error($"{path}.copyright", "must list at least one copyright line");

        if (license is null)
            throw Error($"{path}.license", "is required");

        return (new AssuranceSpdx(copyright, license), glob);
    }

    private static AssuranceArtefactPaths ArtefactPaths(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw Error(path, "must be an object");

        var artefacts = new AssuranceArtefactPaths();
        foreach (JsonProperty property in value.EnumerateObject())
        {
            string at = $"{path}.{property.Name}";
            artefacts = property.Name switch
            {
                "report" => artefacts with { Report = RelativePath(property.Value, at) },
                "humanReview" => artefacts with { HumanReview = RelativePath(property.Value, at) },
                "manifest" => artefacts with { Manifest = RelativePath(property.Value, at) },
                _ => throw Error(at, "is not a property this schema defines"),
            };
        }

        string[] all = [artefacts.Report, artefacts.HumanReview, artefacts.Manifest];
        if (all.Distinct(StringComparer.OrdinalIgnoreCase).Count() != all.Length)
            throw Error(path, "names one file for two artefacts");

        return artefacts;
    }

    private static IReadOnlyList<string> CommentLines(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Array)
            throw Error(path, "must be an array of strings");

        var lines = new List<string>();
        int index = 0;
        foreach (JsonElement entry in value.EnumerateArray())
        {
            string at = $"{path}[{index++}]";
            if (entry.ValueKind != JsonValueKind.String)
                throw Error(at, "must be a string");

            lines.Add(CheckSingleLine(entry.GetString()!, at));
        }

        if (lines.Count == 0 || !lines[0].StartsWith("GENERATED - DO NOT EDIT MANUALLY", StringComparison.Ordinal))
            throw Error(path, "must open with a line starting 'GENERATED - DO NOT EDIT MANUALLY'");

        return lines;
    }

    private static IReadOnlyList<string> Symbols(JsonElement value, string path)
    {
        IReadOnlyList<string> symbols = StringList(value, path);
        for (int index = 0; index < symbols.Count; index++)
        {
            string symbol = symbols[index];
            if (symbol.Length == 0 || !symbol.All(static c => char.IsLetterOrDigit(c) || c == '_'))
                throw Error($"{path}[{index}]", $"'{symbol}' is not a preprocessor symbol");
        }

        return symbols;
    }

    private static AssuranceGlob Glob(string pattern, string path) =>
        AssuranceGlob.TryParse(pattern, out AssuranceGlob? glob, out string? problem)
            ? glob!
            : throw Error(path, problem!);

    private static IReadOnlyList<string> StringList(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Array)
            throw Error(path, "must be an array of strings");

        var list = new List<string>();
        int index = 0;
        foreach (JsonElement entry in value.EnumerateArray())
            list.Add(RequiredString(entry, $"{path}[{index++}]"));

        return list;
    }

    private static string RelativePath(JsonElement value, string path) =>
        CheckRelative(RequiredString(value, path), path);

    private static string CheckRelative(string value, string path)
    {
        if (value.Contains('\\', StringComparison.Ordinal))
            throw Error(path, $"'{value}' uses '\\'; write paths with '/'");

        if (value.StartsWith('/') || (value.Length > 1 && value[1] == ':'))
            throw Error(path, $"'{value}' is rooted; paths are relative to the component root");

        if (value.Split('/').Any(static segment => segment is ".." or "." or ""))
            throw Error(path, $"'{value}' has an empty, '.' or '..' segment");

        if (value.Split('/').Any(static segment => string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase)))
            throw Error(path, $"'{value}' names a path inside a '.git' directory");

        return value;
    }

    private static string SingleLine(JsonElement value, string path) =>
        CheckSingleLine(RequiredString(value, path), path);

    private static string CheckSingleLine(string value, string path)
    {
        if (value.AsSpan().IndexOfAny("\r\n\u0085\u2028\u2029") >= 0)
            throw Error(path, "must be one line");

        return value;
    }

    private static string RequiredString(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.String)
            throw Error(path, "must be a string");

        string text = value.GetString()!.Trim();
        if (text.Length == 0)
            throw Error(path, "must not be empty");

        return text;
    }

    private static bool RequiredBool(JsonElement value, string path) => value.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => throw Error(path, "must be true or false"),
    };

    private static AssuranceConfigException Error(string path, string message) =>
        new($"{FileName}: {path} {message}");
}
