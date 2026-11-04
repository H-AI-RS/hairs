// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Linq;
using Hairs.Assurance;
using Hairs.Language.CSharp;
using Xunit;

namespace Hairs.Tests;

/// <summary>
/// The scanner reads a file under the nearest configuration.
/// </summary>
public sealed class ComponentAssuranceScannerTests : IDisposable
{
    private const string Values =
        "namespace Probe;\n" +
        "\n" +
        "public static class Native\n" +
        "{\n" +
        "    public const int EGL_NONE = 0x3038;\n" +
        "}\n";

    private const string Watched =
        """{ "schema": 1, "projects": [ "src/Probe/Probe.csproj" ], "exemptionPredicate": "owning-component", "namedValues": "watched" }""";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "hairs-component-scanner-" + Guid.NewGuid().ToString("N"));

    public ComponentAssuranceScannerTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
        }
    }

    private void Write(string relativePath, string text)
    {
        string path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static AssuranceScannedUnit Value(IAssuranceUnitScanner scanner, string path) =>
        scanner.Scan(Values, path).Single(static unit => unit.Name == "Probe.Native.EGL_NONE");

    [Fact(Timeout = 600000)]
    public void Each_File_Is_Scanned_As_Its_Nearest_Configuration_Says()
    {
        Write("hairs.config.json", """{ "schema": 1, "projects": [ "Other/Other.csproj" ] }""");
        Write("Native/hairs.config.json", Watched);
        var scanner = new CSharpComponentAssuranceScanner(_root);

        AssuranceScannedUnit native = Value(scanner, "Native/src/Probe/Values.cs");
        Assert.True(native.IsExempt);
        Assert.Equal("NamedValue", native.Exemption);
        Assert.Equal(Path.Combine(_root, "Native", "hairs.config.json"), scanner.ConfigurationFor("Native/src/Probe/Values.cs"));

        AssuranceScannedUnit other = Value(scanner, "Other/Values.cs");
        Assert.False(other.IsExempt);
        Assert.Equal(Path.Combine(_root, "hairs.config.json"), scanner.ConfigurationFor("Other/Values.cs"));

        Assert.Equal(native.Fingerprint, other.Fingerprint);
        Assert.Equal(new CSharpAssuranceScanner().Scan(Values, "x.cs").Select(static unit => unit.Fingerprint),
            scanner.Scan(Values, "Native/x.cs").Select(static unit => unit.Fingerprint));
    }

    [Fact(Timeout = 600000)]
    public void Nothing_Outside_The_Root_Is_Read()
    {
        Write("hairs.config.json", Watched);
        Directory.CreateDirectory(Path.Combine(_root, "src"));
        var scanner = new CSharpComponentAssuranceScanner(Path.Combine(_root, "src"));

        Assert.Null(scanner.ConfigurationFor("Probe/Values.cs"));
        Assert.False(Value(scanner, "Probe/Values.cs").IsExempt);
        Assert.Null(scanner.ConfigurationFor("../Values.cs"));
        Assert.Null(scanner.ConfigurationFor(Path.Combine(_root, "Values.cs")));
        Assert.Null(scanner.ConfigurationFor(string.Empty));
    }

    [Fact(Timeout = 600000)]
    public void An_Unreadable_Configuration_Leaves_The_Default_And_An_Edited_One_Is_Read_Again()
    {
        Write("hairs.config.json", """{ "schema": 1, "projects": [ "a.csproj" ], "namedValues": "maybe" }""");
        var scanner = new CSharpComponentAssuranceScanner(_root);

        Assert.False(Value(scanner, "Values.cs").IsExempt);

        Write("hairs.config.json", Watched);
        File.SetLastWriteTimeUtc(Path.Combine(_root, "hairs.config.json"), DateTime.UtcNow.AddMinutes(1));

        Assert.True(Value(scanner, "Values.cs").IsExempt);
    }
}
