// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using Hairs.Assurance;

namespace Hairs.Tests;

/// <summary>
/// A component's <c>hairs.config.json</c>.
/// </summary>
public sealed class AssuranceConfigTests
{
    [Fact(Timeout = 600000)]
    public void A_Minimal_Config_Takes_The_Defaults()
    {
        AssuranceComponentConfig config = AssuranceComponentConfig.Parse(
            """{ "schema": 1, "projects": [ "src/A/A.csproj" ] }""");

        Assert.Equal(["src/A/A.csproj"], config.Projects);
        Assert.Equal(AssuranceMode.Owned, config.Mode);
        Assert.Equal(AssuranceForgeryVocabulary.Narrow, config.ForgeryVocabulary);
        Assert.False(config.ForbidDirectives);
        Assert.Null(config.PreprocessorSymbols);
        Assert.Null(config.Spdx);
        Assert.Empty(config.Exclude);
        Assert.Empty(config.ClosedToEscapeHatch);
        Assert.Equal("CODE-ASSURANCE.md", config.Artefacts.Report);
        Assert.Equal("HUMAN_REVIEW.md", config.Artefacts.HumanReview);
        Assert.Equal("hairs.manifest.json", config.Artefacts.Manifest);
        Assert.Equal("docs/adr", config.AdrDirectory);
        Assert.True(config.ExcludeBuildOutputAtAnyDepth);
        Assert.Equal(AssuranceExemptionPredicate.Strict, config.ExemptionPredicate);
        Assert.Equal(AssuranceNamedValues.Reviewed, config.NamedValues);
        Assert.Equal(AssuranceCriteriaBelowHigh.Permitted, config.CriteriaBelowHigh);
    }

    [Theory(Timeout = 600000)]
    [InlineData("reviewed", AssuranceNamedValues.Reviewed)]
    [InlineData("watched", AssuranceNamedValues.Watched)]
    public void Named_Values_Are_Chosen_Explicitly(string value, AssuranceNamedValues expected)
    {
        AssuranceComponentConfig config = AssuranceComponentConfig.Parse(
            $$"""{ "projects": [ "a/a.csproj" ], "namedValues": "{{value}}" }""");

        Assert.Equal(expected, config.NamedValues);
    }

    [Theory(Timeout = 600000)]
    [InlineData("permitted", AssuranceCriteriaBelowHigh.Permitted)]
    [InlineData("refused", AssuranceCriteriaBelowHigh.Refused)]
    public void Criteria_Below_High_Are_Refused_Explicitly(string value, AssuranceCriteriaBelowHigh expected)
    {
        AssuranceComponentConfig config = AssuranceComponentConfig.Parse(
            $$"""{ "projects": [ "a/a.csproj" ], "criteriaBelowHigh": "{{value}}" }""");

        Assert.Equal(expected, config.CriteriaBelowHigh);
    }

    [Fact(Timeout = 600000)]
    public void The_Owning_Components_Rules_Are_Chosen_Explicitly()
    {
        AssuranceComponentConfig config = AssuranceComponentConfig.Parse(
            """{ "projects": [ "a/a.csproj" ], "excludeBuildOutputAtAnyDepth": false, "exemptionPredicate": "owning-component" }""");

        Assert.False(config.ExcludeBuildOutputAtAnyDepth);
        Assert.Equal(AssuranceExemptionPredicate.OwningComponent, config.ExemptionPredicate);
    }

    [Fact(Timeout = 600000)]
    public void Every_Field_Is_Read()
    {
        AssuranceComponentConfig config = AssuranceComponentConfig.Parse(
            """
            {
              "schema": 1,
              "component": "Hairs.Core",
              "mode": "external",
              "projects": [ "src/A/A.csproj", "src/B/B.csproj" ],
              "exclude": [ "*.g.cs", { "glob": "src/A/Legacy/**", "reason": "vendored" } ],
              "spdx": { "copyright": [ "2026 HAIRS contributors" ], "license": "Apache-2.0" },
              "spdxOverrides": [
                { "glob": "src/A/Renderer/**",
                  "copyright": [ "2026 HAIRS contributors" ],
                  "license": "Apache-2.0" }
              ],
              "artefacts": { "report": "docs/CODE-ASSURANCE.md", "humanReview": "HUMAN_REVIEW.generated.md", "manifest": "hairs.manifest.json" },
              "preprocessorSymbols": [ "NET", "RELEASE" ],
              "forbidDirectives": true,
              "forgeryVocabulary": "strict",
              "closedToEscapeHatch": [ "Hairs.Core" ],
              "adrDirectory": "docs/decisions",
              "regenerateCommand": "hairs assurance generate --root .",
              "manifestComment": [ "GENERATED - DO NOT EDIT MANUALLY. Regenerate with", "", "Prose." ]
            }
            """);

        Assert.Equal("Hairs.Core", config.Component);
        Assert.Equal(AssuranceMode.External, config.Mode);
        Assert.Equal(2, config.Projects.Count);
        Assert.Equal("excluded by hairs.config.json", config.ExclusionFor("src/A/X.g.cs")!.Reason);
        Assert.Equal("vendored", config.ExclusionFor("src/A/Legacy/Old/Y.cs")!.Reason);
        Assert.Null(config.ExclusionFor("src/A/Y.cs"));
        Assert.Equal(AssuranceForgeryVocabulary.Strict, config.ForgeryVocabulary);
        Assert.True(config.ForbidDirectives);
        Assert.Equal(["NET", "RELEASE"], config.PreprocessorSymbols);
        Assert.Equal(["Hairs.Core"], config.ClosedToEscapeHatch);
        Assert.Equal("docs/decisions", config.AdrDirectory);
        Assert.Equal("docs/CODE-ASSURANCE.md", config.Artefacts.Report);
        Assert.Equal("HUMAN_REVIEW.generated.md", config.Artefacts.HumanReview);
        Assert.Equal("hairs assurance generate --root .", config.RegenerateCommand);
        Assert.Equal(["GENERATED - DO NOT EDIT MANUALLY. Regenerate with", string.Empty, "Prose."], config.ManifestComment);
        Assert.Equal(
            ("hairs assurance generate --root .", "hairs assurance check --root ."),
            AssuranceReportContext.CommandsFor(config));

        AssuranceSpdx derived = config.SpdxFor("src/A/Renderer/Box.cs")!;
        Assert.Single(derived.Copyright);
        Assert.Equal("Apache-2.0", derived.License);
        Assert.Equal("Apache-2.0", config.SpdxFor("src/B/Other.cs")!.License);
    }

    [Theory(Timeout = 600000)]
    [InlineData("""{ "projects": [ "a/a.csproj" ], "exlcude": [] }""", "$.exlcude is not a property this schema defines")]
    [InlineData("""{ "schema": 1 }""", "$.projects must list at least one product project")]
    [InlineData("""{ "schema": 2, "projects": [ "a/a.csproj" ] }""", "$.schema must be 1")]
    [InlineData("""{ "projects": [ "a/a.cs" ] }""", "is not a .csproj file")]
    [InlineData("""{ "projects": [ "../other/a.csproj" ] }""", "has an empty, '.' or '..' segment")]
    [InlineData("""{ "projects": [ "C:/x/a.csproj" ] }""", "is rooted")]
    [InlineData("""{ "projects": [ "a\\a.csproj" ] }""", "write paths with '/'")]
    [InlineData("""{ "projects": [ "a/a.csproj" ], "mode": "shared" }""", "$.mode must be \"owned\" or \"external\"")]
    [InlineData("""{ "projects": [ "a/a.csproj" ], "forgeryVocabulary": "wide" }""", "must be \"narrow\" or \"strict\"")]
    [InlineData("""{ "projects": [ "a/a.csproj" ], "spdx": { "license": "MIT" } }""", "$.spdx.copyright must list at least one copyright line")]
    [InlineData("""{ "projects": [ "a/a.csproj" ], "spdx": { "copyright": [ "x" ] } }""", "$.spdx.license is required")]
    [InlineData("""{ "projects": [ "a/a.csproj" ], "spdx": { "copyright": [ "// SPDX-FileCopyrightText: x" ], "license": "MIT" } }""", "is the value only")]
    [InlineData("""{ "projects": [ "a/a.csproj" ], "spdxOverrides": [ { "copyright": [ "x" ], "license": "MIT" } ] }""", "has no \"glob\"")]
    [InlineData("""{ "projects": [ "a/a.csproj" ], "exclude": [ "/abs/**" ] }""", "is rooted")]
    [InlineData("""{ "projects": [ "a/a.csproj" ], "artefacts": { "report": "X.md", "humanReview": "X.md" } }""", "names one file for two artefacts")]
    [InlineData("""{ "projects": [ "a/a.csproj" ], "preprocessorSymbols": [ "NET 10" ] }""", "is not a preprocessor symbol")]
    [InlineData("""{ "projects": [ "a/a.csproj", "a/a.csproj" ] }""", "names a project twice")]
    [InlineData("""{ "projects": [ "a/a.csproj" ], "manifestComment": [ "Hand-written." ] }""", "$.manifestComment must open with a line starting 'GENERATED - DO NOT EDIT MANUALLY'")]
    [InlineData("""{ "projects": [ "a/a.csproj" ], "manifestComment": [ "GENERATED - DO NOT EDIT MANUALLY.", 3 ] }""", "$.manifestComment[1] must be a string")]
    [InlineData("""{ "projects": [ "a/a.csproj" ], "artefacts": { "report": ".git/hooks/pre-commit" } }""", "names a path inside a '.git' directory")]
    [InlineData("""{ "projects": [ "a/.GIT/a.csproj" ] }""", "names a path inside a '.git' directory")]
    [InlineData("""{ "projects": [ "a/a.csproj" ], "component": "Probe\necho owned" }""", "$.component must be one line")]
    [InlineData("""{ "projects": [ "a/a.csproj" ], "exemptionPredicate": "vm" }""", "must be \"strict\" or \"owning-component\"")]
    [InlineData("""{ "projects": [ "a/a.csproj" ], "excludeBuildOutputAtAnyDepth": "yes" }""", "must be true or false")]
    [InlineData("""{ "projects": [ "a/a.csproj" ], "namedValues": "exempt" }""", "$.namedValues must be \"reviewed\" or \"watched\"")]
    [InlineData("""{ "projects": [ "a/a.csproj" ], "namedValues": "Watched" }""", "$.namedValues must be \"reviewed\" or \"watched\"")]
    [InlineData("""{ "projects": [ "a/a.csproj" ], "namedValues": true }""", "$.namedValues must be a string")]
    [InlineData("""{ "projects": [ "a/a.csproj" ], "criteriaBelowHigh": "forbidden" }""", "$.criteriaBelowHigh must be \"permitted\" or \"refused\"")]
    [InlineData("""[ ]""", "$ must be an object")]
    [InlineData("""{ "projects": """, "not valid JSON")]
    public void A_Mistake_Is_An_Error_Naming_Where_It_Is(string json, string expected)
    {
        AssuranceConfigException exception =
            Assert.Throws<AssuranceConfigException>(() => AssuranceComponentConfig.Parse(json));

        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    [Theory(Timeout = 600000)]
    [InlineData("*.g.cs", "src/A/Deep/X.g.cs", true)]
    [InlineData("*.g.cs", "src/A/X.cs", false)]
    [InlineData("**/*.g.cs", "X.g.cs", true)]
    [InlineData("**/*.g.cs", "src/A/X.g.cs", true)]
    [InlineData("src/A/**", "src/A/B/C.cs", true)]
    [InlineData("src/A/**", "src/AB/C.cs", false)]
    [InlineData("src/*/C.cs", "src/A/C.cs", true)]
    [InlineData("src/*/C.cs", "src/A/B/C.cs", false)]
    [InlineData("src/A/?.cs", "src/A/B.cs", true)]
    [InlineData("src/A/**/Gen/*.cs", "src/A/Gen/X.cs", true)]
    [InlineData("src/A/**/Gen/*.cs", "src/A/B/C/Gen/X.cs", true)]
    [InlineData("src/a/*.cs", "src/A/X.cs", false)]
    public void A_Glob_Matches_Root_Relative_Paths(string pattern, string path, bool expected)
    {
        Assert.True(AssuranceGlob.TryParse(pattern, out AssuranceGlob? glob, out _));
        Assert.Equal(expected, glob!.IsMatch(path));
    }
}
