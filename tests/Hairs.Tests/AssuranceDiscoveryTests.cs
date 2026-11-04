// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Hairs.Assurance;
using Hairs.Cli.Assurance;
using Xunit;

namespace Hairs.Tests;

/// <summary>
/// Which files a component's assurance covers, and what <c>assurance list</c>
/// reports about them.
/// </summary>
public sealed class AssuranceDiscoveryTests
{
    private const string Relevant = "namespace N;\npublic sealed class C\n{\n    public void Run() { System.Console.WriteLine(); }\n}\n";

    [Fact(Timeout = 600000)]
    public void Build_Output_Nested_Checkouts_And_Exclusions_Are_Not_Covered()
    {
        using var component = new TemporaryComponent();
        component.Project("src/A/A.csproj", assemblyName: "Custom.A");
        component.Write("src/A/X.cs", Relevant);
        component.Write("src/A/Sub/Y.cs", Relevant);
        component.Write("src/A/bin/Release/Z.cs", Relevant);
        component.Write("src/A/Deep/obj/Debug/W.AssemblyAttributes.cs", Relevant);
        component.Write("src/A/Nested/.git", "gitdir: ../../.git/modules/Nested\n");
        component.Write("src/A/Nested/N.cs", Relevant);
        component.Folder("src/A/Stale/.git");
        component.Write("src/A/Stale/S.cs", Relevant);
        component.Write("src/A/Gen/X.g.cs", Relevant);
        component.Project("src/B/B.csproj");
        component.Write("src/B/Q.cs", Relevant);

        AssuranceComponentConfig config = AssuranceComponentConfig.Parse(
            """{ "projects": [ "src/A/A.csproj" ], "exclude": [ { "glob": "**/*.g.cs", "reason": "regenerated" } ] }""");

        ComponentSourceSet set = ComponentSources.Discover(component.Root, config);

        Assert.Equal(["src/A/Sub/Y.cs", "src/A/X.cs"], set.Files.Select(file => file.RelativePath));
        Assert.All(set.Files, file => Assert.Equal("Custom.A", file.Project.AssemblyName));

        Assert.Equal(
            [
                ("src/A/Deep/obj/Debug/W.AssemblyAttributes.cs", "inside the build output directory 'src/A/Deep/obj/'"),
                ("src/A/Gen/X.g.cs", "regenerated"),
                ("src/A/Nested/", "a nested checkout: it holds a .git entry"),
                ("src/A/Stale/", "a nested checkout: it holds a .git entry"),
            ],
            set.Excluded.Select(static file => (file.RelativePath, file.Reason.Split(", which", 2)[0].Split(", so", 2)[0])));
    }

    [Fact(Timeout = 600000)]
    public void Build_Output_Below_The_Project_Root_Is_Covered_Under_The_Owning_Components_Rule()
    {
        using var component = new TemporaryComponent();
        component.Project("src/A/A.csproj");
        component.Write("src/A/obj/Debug/A.AssemblyInfo.cs", Relevant);
        component.Write("src/A/Internal/obj/Gate.cs", Relevant);

        ComponentSourceSet set = ComponentSources.Discover(
            component.Root,
            AssuranceComponentConfig.Parse("""{ "projects": [ "src/A/A.csproj" ], "excludeBuildOutputAtAnyDepth": false }"""));

        Assert.Equal(["src/A/Internal/obj/Gate.cs"], set.Files.Select(file => file.RelativePath));
        Assert.Empty(set.Excluded);
    }

    [Fact(Timeout = 600000)]
    public void Links_Are_Not_Followed_And_Are_Named()
    {
        using var component = new TemporaryComponent();
        component.Project("src/A/A.csproj");
        component.Write("src/A/X.cs", Relevant);
        component.Write("elsewhere/Outside.cs", Relevant);
        Assert.True(component.TryLinkDirectory("src/A/Linked", component.PathOf("elsewhere")), "no link could be made");

        ComponentSourceSet set = ComponentSources.Discover(
            component.Root, AssuranceComponentConfig.Parse("""{ "projects": [ "src/A/A.csproj" ] }"""));

        Assert.Equal(["src/A/X.cs"], set.Files.Select(file => file.RelativePath));
        ComponentExcludedFile linked = Assert.Single(set.Excluded);
        Assert.Equal("src/A/Linked/", linked.RelativePath);
        Assert.Equal("a junction or symbolic link, which the tool does not follow", linked.Reason);
    }

    [Fact(Timeout = 600000)]
    public void A_Project_Or_An_Artefact_Behind_A_Link_Is_Refused()
    {
        using var component = new TemporaryComponent();
        using var outside = new TemporaryComponent();
        outside.Project("Ext.csproj");
        outside.Write("Outside.cs", Relevant);
        Assert.True(component.TryLinkDirectory("ext", outside.Root), "no link could be made");

        ComponentSourceException project = Assert.Throws<ComponentSourceException>(() => ComponentSources.Discover(
            component.Root, AssuranceComponentConfig.Parse("""{ "projects": [ "ext/Ext.csproj" ] }""")));
        Assert.Contains("lies inside 'ext', which is a junction or symbolic link", project.Message, StringComparison.Ordinal);

        component.Project("src/A/A.csproj");
        component.Write("hairs.config.json",
            """{ "projects": [ "src/A/A.csproj" ], "artefacts": { "manifest": "ext/hairs.manifest.json" }, "spdx": { "copyright": [ "x" ], "license": "MIT" } }""");

        (int exit, _, string error) = component.Run("generate", "--root", component.Root);
        Assert.Equal(AssuranceCommand.UsageError, exit);
        Assert.Contains("the artefact 'ext/hairs.manifest.json' lies inside 'ext', which is a junction or symbolic link", error, StringComparison.Ordinal);
        Assert.False(File.Exists(outside.PathOf("hairs.manifest.json")));
    }

    [Fact(Timeout = 600000)]
    public void Files_A_Project_Compiles_In_From_Elsewhere_Are_Covered_Or_Named()
    {
        using var component = new TemporaryComponent();
        component.Write("src/Prod/Prod.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\">\n" +
            "  <ItemGroup>\n" +
            "    <Compile Include=\"..\\Shared\\Linked.cs\" Link=\"Linked.cs\" />\n" +
            "    <Compile Include=\"..\\..\\..\\Outside.cs\" />\n" +
            "    <Compile Include=\"$(SharedRoot)\\Other.cs\" />\n" +
            "    <Compile Include=\"..\\Glob\\**\\*.cs\" />\n" +
            "  </ItemGroup>\n" +
            "</Project>\n");
        component.Write("src/Directory.Build.props",
            "<Project><ItemGroup><Compile Include=\"$(MSBuildThisFileDirectory)SharedAssemblyInfo.cs\" /></ItemGroup></Project>\n");
        component.Write("src/Prod/Widget.cs", Relevant);
        component.Write("src/Shared/Linked.cs", Relevant);
        component.Write("src/SharedAssemblyInfo.cs", "[assembly: System.CLSCompliant(false)]\n");
        component.Write("src/Glob/Deep/G.cs", Relevant);
        component.Write("src/Glob/G.txt", "not code\n");
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(component.Root)!, "Outside.cs"), Relevant);

        try
        {
            ComponentSourceSet set = ComponentSources.Discover(
                component.Root, AssuranceComponentConfig.Parse("""{ "projects": [ "src/Prod/Prod.csproj" ] }"""));

            Assert.Equal(
                ["src/Glob/Deep/G.cs", "src/Prod/Widget.cs", "src/Shared/Linked.cs", "src/SharedAssemblyInfo.cs"],
                set.Files.Select(file => file.RelativePath));
            Assert.All(set.Files, file => Assert.Equal("Prod", file.Project.AssemblyName));

            Assert.Equal(
                [
                    ("$(SharedRoot)\\Other.cs", "a <Compile Include> in src/Prod/Prod.csproj that this tool cannot evaluate"),
                    ("../Outside.cs", "compiled into Prod by <Compile Include=\"..\\..\\..\\Outside.cs\"> in src/Prod/Prod.csproj, and outside the component root"),
                ],
                set.Excluded.Select(static file => (file.RelativePath, file.Reason.Split(", so", 2)[0].Split(", where", 2)[0])));
        }
        finally
        {
            File.Delete(Path.Combine(Path.GetDirectoryName(component.Root)!, "Outside.cs"));
        }
    }

    [Fact(Timeout = 600000)]
    public void Assembly_Names_Are_Read_Exactly_Or_Refused()
    {
        using var component = new TemporaryComponent();
        component.Write("src/Product/Product.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup Condition=\"'$(Configuration)' == 'Release'\">" +
            "<AssemblyName>Prod</AssemblyName></PropertyGroup></Project>\n");
        component.Project("src/B/B.csproj", assemblyName: "Sample.B");

        ComponentSourceException conditional = Assert.Throws<ComponentSourceException>(() => ComponentSources.Discover(
            component.Root, AssuranceComponentConfig.Parse("""{ "projects": [ "src/Product/Product.csproj" ] }""")));
        Assert.Contains("sets AssemblyName to 'Prod' under a condition", conditional.Message, StringComparison.Ordinal);

        ComponentSourceException closed = Assert.Throws<ComponentSourceException>(() => ComponentSources.Discover(
            component.Root,
            AssuranceComponentConfig.Parse("""{ "projects": [ "src/B/B.csproj" ], "closedToEscapeHatch": [ "B" ] }""")));
        Assert.Equal(
            "hairs.config.json: $.closedToEscapeHatch names 'B', which no configured project builds; they build Sample.B",
            closed.Message);
    }

    [Fact(Timeout = 600000)]
    public void A_Nested_Project_Owns_Its_Own_Files()
    {
        using var component = new TemporaryComponent();
        component.Project("src/A/A.csproj");
        component.Project("src/A/Inner/Inner.csproj");
        component.Write("src/A/X.cs", Relevant);
        component.Write("src/A/Inner/I.cs", Relevant);

        ComponentSourceSet set = ComponentSources.Discover(
            component.Root,
            AssuranceComponentConfig.Parse("""{ "projects": [ "src/A/A.csproj", "src/A/Inner/Inner.csproj" ] }"""));

        Assert.Equal(
            [("src/A/Inner/I.cs", "Inner"), ("src/A/X.cs", "A")],
            set.Files.Select(file => (file.RelativePath, file.Project.AssemblyName)));
    }

    [Fact(Timeout = 600000)]
    public void A_Project_Inside_A_Nested_Checkout_Is_Refused()
    {
        using var component = new TemporaryComponent();
        component.Folder("Other.Repo/.git");
        component.Project("Other.Repo/src/G/G.csproj");

        ComponentSourceException exception = Assert.Throws<ComponentSourceException>(() =>
            ComponentSources.Discover(
                component.Root,
                AssuranceComponentConfig.Parse("""{ "projects": [ "Other.Repo/src/G/G.csproj" ] }""")));

        Assert.Contains("'Other.Repo', which holds a .git entry", exception.Message, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void A_Missing_Project_Is_Refused()
    {
        using var component = new TemporaryComponent();

        Assert.Throws<ComponentSourceException>(() => ComponentSources.Discover(
            component.Root, AssuranceComponentConfig.Parse("""{ "projects": [ "src/A/A.csproj" ] }""")));
    }

    [Fact(Timeout = 600000)]
    public void Without_A_Config_Test_Projects_Are_Not_Guessed_As_Products()
    {
        using var component = new TemporaryComponent();
        component.Project("src/A/A.csproj");
        component.Project("src/A.Tests/A.Tests.csproj");
        component.Project("src/Checks/Checks.csproj", isTest: true);
        component.Project("tests/Harness/Harness.csproj");

        ComponentSourceSet set = ComponentSources.Discover(component.Root, config: null);

        Assert.Equal(["src/A/A.csproj"], set.Projects.Select(project => project.RelativePath));
        Assert.Contains(set.Notes, note => note.Contains("guessed", StringComparison.Ordinal));
    }

    [Fact(Timeout = 600000)]
    public void A_Files_List_Restricts_The_Run()
    {
        using var component = new TemporaryComponent();
        component.Project("src/A/A.csproj");
        component.Write("src/A/X.cs", Relevant);
        component.Write("src/A/Y.cs", Relevant);

        ComponentSourceSet set = ComponentSources.Discover(
            component.Root, AssuranceComponentConfig.Parse("""{ "projects": [ "src/A/A.csproj" ] }"""));
        ComponentSourceSet restricted = ComponentSources.Restrict(
            set,
            component.Root,
            ["# changed files", "src\\A\\Y.cs", ".//src//A/./X.cs", Path.Combine(component.Root, "src", "A", "Y.cs"), "./README.md", "src/A/Typo.cs", ""],
            out IReadOnlyList<ComponentUnknownPath> unknown);

        Assert.Equal(["src/A/X.cs", "src/A/Y.cs"], restricted.Files.Select(file => file.RelativePath));
        Assert.Equal(
            [
                new ComponentUnknownPath("README.md", "does not exist under the component root"),
                new ComponentUnknownPath("src/A/Typo.cs", "does not exist under the component root"),
            ],
            unknown);
    }

    [Fact(Timeout = 600000)]
    public void A_Path_In_Another_Case_Names_The_File_Only_Where_The_File_System_Folds_Case()
    {
        using var component = new TemporaryComponent();
        component.Project("src/A/A.csproj");
        component.Write("src/A/Widget.cs", Relevant);

        ComponentSourceSet set = ComponentSources.Discover(
            component.Root, AssuranceComponentConfig.Parse("""{ "projects": [ "src/A/A.csproj" ] }"""));
        ComponentSourceFile? found = ComponentSources.Find(set, component.Root, "SRC/a/widget.CS", out ComponentUnknownPath? problem);

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("src/A/Widget.cs", found?.RelativePath);
        }
        else
        {
            Assert.Null(found);
            Assert.Contains("is not a covered file; the covered file 'src/A/Widget.cs' differs from it in case only", problem!.Reason, StringComparison.Ordinal);
        }
    }

    [Fact(Timeout = 600000)]
    public void A_Files_List_That_Names_Nothing_Covered_Says_So_And_Fails_When_Strict()
    {
        using var component = new TemporaryComponent();
        component.Project("src/A/A.csproj");
        component.Write("hairs.config.json", """{ "projects": [ "src/A/A.csproj" ], "exclude": [ { "glob": "**/*.g.cs", "reason": "regenerated" } ] }""");
        component.Write("src/A/X.cs", Relevant);
        component.Write("src/A/X.g.cs", Relevant);
        component.Write("list.txt", "src/A/Xx.cs\nsrc/A/X.g.cs\n");

        (int exit, string output, _) = component.Run("list", "--root", component.Root, "--files", component.PathOf("list.txt"), "--json", "-");
        Assert.Equal(0, exit);

        using JsonDocument report = JsonDocument.Parse(output);
        Assert.Equal(2, report.RootElement.GetProperty("totals").GetProperty("unknown").GetInt32());
        Assert.Equal(
            [("src/A/X.g.cs", "is not covered: regenerated"), ("src/A/Xx.cs", "does not exist under the component root")],
            report.RootElement.GetProperty("unknownFiles").EnumerateArray()
                .Select(static file => (file.GetProperty("file").GetString(), file.GetProperty("reason").GetString())));
        Assert.Equal(
            ["src/A/X.g.cs"],
            report.RootElement.GetProperty("excluded").EnumerateArray().Select(static file => file.GetProperty("file").GetString()));

        (int strict, _, _) = component.Run("list", "--root", component.Root, "--files", component.PathOf("list.txt"), "--strict");
        Assert.Equal(AssuranceCommand.Refused, strict);

        component.WriteBytes("src/A/X.cs", [0x6E, 0x61, 0x6D, 0xE9, 0x0A]);
        (int unreadable, string text, _) = component.Run("list", "--root", component.Root);
        Assert.Equal(AssuranceCommand.Refused, unreadable);
        Assert.Contains("src/A/X.cs  unreadable: the file is not valid UTF-8", text, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void The_List_Reports_Unannotated_Relevant_Units_With_Their_Placement()
    {
        using var component = new TemporaryComponent();
        component.Project("src/A/A.csproj");
        component.Write("hairs.config.json", """{ "component": "Probe", "projects": [ "src/A/A.csproj" ] }""");
        component.Write("src/A/X.cs",
            "namespace N;\n" +
            "// Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF\n" +
            "// Hairs-Human:        PENDING\n" +
            "public sealed class C\n" +
            "{\n" +
            "    /// <summary>Runs.</summary>\n" +
            "    [System.Obsolete]\n" +
            "    public void Run()\n" +
            "    {\n" +
            "        System.Console.WriteLine();\n" +
            "    }\n" +
            "\n" +
            "    public int Auto { get; set; }\n" +
            "    public int A; public void B() { System.Console.WriteLine(); }\n" +
            "}\n");

        (int exit, string output, _) = component.Run("list", "--root", component.Root, "--json", "-");

        Assert.Equal(0, exit);
        using JsonDocument report = JsonDocument.Parse(output);
        JsonElement root = report.RootElement;
        Assert.Equal("Probe", root.GetProperty("component").GetString());

        JsonElement units = root.GetProperty("units");
        Assert.Equal(2, units.GetArrayLength());

        JsonElement run = units[0];
        Assert.Equal("src/A/X.cs", run.GetProperty("file").GetString());
        Assert.Equal("N.C.Run()", run.GetProperty("unit").GetString());
        Assert.Equal("Run()", run.GetProperty("displayName").GetString());
        Assert.Equal("method", run.GetProperty("kind").GetString());
        Assert.Equal(7, run.GetProperty("line").GetInt32());
        Assert.Equal(5, run.GetProperty("column").GetInt32());
        Assert.Equal("    ", run.GetProperty("indent").GetString());
        Assert.Equal(7, run.GetProperty("extent").GetProperty("startLine").GetInt32());
        Assert.Equal(11, run.GetProperty("extent").GetProperty("endLine").GetInt32());
        Assert.Equal(6, run.GetProperty("fingerprint").GetString()!.Length);
        Assert.True(run.GetProperty("insertable").GetBoolean());
        Assert.Equal("none", run.GetProperty("reason").GetString());
        Assert.Equal("NEW", run.GetProperty("state").GetString());

        JsonElement second = units[1];
        Assert.Equal("N.C.B()", second.GetProperty("unit").GetString());
        Assert.False(second.GetProperty("insertable").GetBoolean());
        Assert.Equal("not-own-line", second.GetProperty("reason").GetString());

        JsonElement file = root.GetProperty("files")[0];
        Assert.Equal(3, file.GetProperty("relevant").GetInt32());
        Assert.Equal(1, file.GetProperty("annotated").GetInt32());
        Assert.Equal(2, file.GetProperty("unannotated").GetInt32());
        Assert.Equal(2, file.GetProperty("exempt").GetInt32());
        Assert.Equal(1, file.GetProperty("insertable").GetInt32());
        Assert.Equal(2, file.GetProperty("listed").GetInt32());

        (_, string all, _) = component.Run("list", "--root", component.Root, "--json", "-", "--all-units");
        using JsonDocument everything = JsonDocument.Parse(all);
        Assert.Equal(5, everything.RootElement.GetProperty("units").GetArrayLength());
        Assert.Contains(
            everything.RootElement.GetProperty("units").EnumerateArray(),
            unit => unit.GetProperty("unit").GetString() == "N.C" &&
                unit.GetProperty("reason").GetString() == "annotated" &&
                unit.GetProperty("state").GetString() == "AI_ASSESSED");
    }

    [Fact(Timeout = 600000)]
    public void The_Text_List_Names_Each_Unit_And_Why_It_Cannot_Take_A_Block()
    {
        using var component = new TemporaryComponent();
        component.Project("src/A/A.csproj");
        component.Write("hairs.config.json", """{ "component": "Probe", "projects": [ "src/A/A.csproj" ] }""");
        component.Write("src/A/X.cs",
            "namespace N;\n" +
            "public sealed class C\n" +
            "{\n" +
            "    // Hairs-Human:        PENDING\n" +
            "    public void Run() { System.Console.WriteLine(); }\n" +
            "}\n");

        (int exit, string output, _) = component.Run("list", "--root", component.Root);

        Assert.Equal(0, exit);
        Assert.StartsWith(
            "Probe: 1 files, 2 units, 2 relevant, 0 annotated, 2 unannotated, 1 insertable",
            output,
            StringComparison.Ordinal);
        Assert.Contains("src/A/X.cs  relevant 2  annotated 0  unannotated 2  insertable 1", output, StringComparison.Ordinal);
        Assert.Contains(
            "N.C.Run()  [NEW] half-block: line 4 carries an assurance line with no '// Hairs-AI:' line above it",
            output,
            StringComparison.Ordinal);

        component.Write("src/A/Y.cs", "namespace N;\npublic sealed class D\n{\n    public int Count;\n}\n");
        (_, string all, _) = component.Run("list", "--root", component.Root, "--all-units");
        Assert.Contains("N.D.Count  [EXEMPT] exempt: FieldDeclaringStorage", all, StringComparison.Ordinal);
        Assert.DoesNotContain("exempt: exempt:", all, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void An_Unknown_Option_Is_A_Usage_Error()
    {
        using var component = new TemporaryComponent();

        (int exit, _, string error) = component.Run("list", "--root", component.Root, "--everything");

        Assert.Equal(2, exit);
        Assert.Contains("'--everything' is not an option of 'assurance list'", error, StringComparison.Ordinal);
    }
}
