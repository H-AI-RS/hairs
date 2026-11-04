// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using Hairs.Assurance;
using Hairs.Language.CSharp;

namespace Hairs.Tests;

/// <summary>
/// The assurance scanner tests.
/// </summary>
public sealed class AssuranceScannerTests
{
    [Fact(Timeout = 600000)]
    public void The_Scanner_Reproduces_The_Fingerprints_In_The_Source()
    {
        IReadOnlyList<AssuranceScannedUnit> units = new CSharpAssuranceScanner()
            .Scan(AssuranceFixture.Descriptor, "ArtifactDescriptor.cs");

        Assert.Equal("B53BCB", Fingerprint(units, "Hairs.Core.ArtifactDescriptor"));
        Assert.Equal("4A3BFD", Fingerprint(units, "Hairs.Core.ArtifactDescriptor.IsWellFormed"));
        Assert.Equal(
            "7A6D21",
            Fingerprint(units, "Hairs.Core.ArtifactDescriptor.Equals(ArtifactDescriptor)"));
        Assert.Equal(
            "74B2DC",
            Fingerprint(
                units,
                "Hairs.Core.ArtifactDescriptor.operator !=(ArtifactDescriptor, ArtifactDescriptor)"));
    }

    [Fact(Timeout = 600000)]
    public void The_Scanner_Reproduces_The_Fingerprints_The_Manifest_Records()
    {
        IReadOnlyList<AssuranceScannedUnit> units = new CSharpAssuranceScanner()
            .Scan(AssuranceFixture.Descriptor, "ArtifactDescriptor.cs");

        Assert.Equal(
            "052A4A",
            Fingerprint(
                units,
                "Hairs.Core.ArtifactDescriptor.ArtifactDescriptor(ProfileId, uint, " +
                "FeatureManifestId, LimitVector, CallerIdentity)"));
        Assert.Equal("19C70B", Fingerprint(units, "Hairs.Core.ArtifactDescriptor.ProfileId"));
        Assert.Equal("551767", Fingerprint(units, "Hairs.Core.ArtifactDescriptor.FormatVersion"));
        Assert.Equal("798D3A", Fingerprint(units, "Hairs.Core.ArtifactDescriptor.FeatureManifestId"));
        Assert.Equal("E84955", Fingerprint(units, "Hairs.Core.ArtifactDescriptor.RequestedLimits"));
        Assert.Equal("AC3713", Fingerprint(units, "Hairs.Core.ArtifactDescriptor.CallerIdentity"));
        Assert.Equal("241477", Fingerprint(units, "Hairs.Core.ArtifactDescriptor.Equals(object?)"));
        Assert.Equal("464772", Fingerprint(units, "Hairs.Core.ArtifactDescriptor.GetHashCode()"));
        Assert.Equal(
            "E67251",
            Fingerprint(
                units,
                "Hairs.Core.ArtifactDescriptor.operator ==(ArtifactDescriptor, ArtifactDescriptor)"));
    }

    [Fact(Timeout = 600000)]
    public void The_File_Fingerprint_Is_The_One_The_Manifest_Records()
    {
        Assert.Equal(
            "427D7F",
            CSharpAssuranceScanner.FingerprintOfFile(AssuranceFixture.Descriptor, "ArtifactDescriptor.cs"));
        Assert.Equal(
            "427D7F",
            new CSharpAssuranceFileScanner().ScanFile(AssuranceFixture.Descriptor, "ArtifactDescriptor.cs").FileFingerprint);
    }

    [Fact(Timeout = 600000)]
    public void Rewriting_Every_Comment_Moves_No_Fingerprint()
    {
        var scanner = new CSharpAssuranceScanner();

        string reviewed = AssuranceFixture.Descriptor
            .Replace("// Hairs-Human:        PENDING", "// Hairs-Human:        EB", StringComparison.Ordinal)
            .Replace("/// <inheritdoc/>", "/// <summary>Something else entirely.</summary>", StringComparison.Ordinal)
            .Replace("// Human-reviewed:   0/4", "// Human-reviewed:   4/4", StringComparison.Ordinal);

        IReadOnlyList<AssuranceScannedUnit> before = scanner.Scan(AssuranceFixture.Descriptor, "x.cs");
        IReadOnlyList<AssuranceScannedUnit> after = scanner.Scan(reviewed, "x.cs");

        Assert.Equal(
            before.Select(unit => (unit.Name, unit.Fingerprint)),
            after.Select(unit => (unit.Name, unit.Fingerprint)));
    }

    [Fact(Timeout = 600000)]
    public void Line_Endings_Move_No_Fingerprint()
    {
        var scanner = new CSharpAssuranceScanner();
        string lf = AssuranceFixture.Descriptor.Replace("\r\n", "\n", StringComparison.Ordinal);
        string crlf = lf.Replace("\n", "\r\n", StringComparison.Ordinal);

        Assert.Equal(
            scanner.Scan(lf, "x.cs").Select(unit => unit.Fingerprint),
            scanner.Scan(crlf, "x.cs").Select(unit => unit.Fingerprint));
    }

    [Fact(Timeout = 600000)]
    public void The_Predicate_Agrees_With_The_Generated_Header()
    {
        IReadOnlyList<AssuranceScannedUnit> units = new CSharpAssuranceScanner()
            .Scan(AssuranceFixture.Descriptor, "ArtifactDescriptor.cs");

        Assert.Equal(13, units.Count);
        Assert.Equal(4, units.Count(unit => !unit.IsExempt));
        Assert.Equal(9, units.Count(unit => unit.IsExempt));

        Assert.Equal(
            [
                "Hairs.Core.ArtifactDescriptor",
                "Hairs.Core.ArtifactDescriptor.Equals(ArtifactDescriptor)",
                "Hairs.Core.ArtifactDescriptor.IsWellFormed",
                "Hairs.Core.ArtifactDescriptor.operator !=(ArtifactDescriptor, ArtifactDescriptor)",
            ],
            units.Where(unit => !unit.IsExempt).Select(unit => unit.Name).OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact(Timeout = 600000)]
    public void An_Exemption_Reports_The_Case_That_Covered_It()
    {
        IReadOnlyList<AssuranceScannedUnit> units = new CSharpAssuranceScanner()
            .Scan(AssuranceFixture.Descriptor, "ArtifactDescriptor.cs");

        Assert.Equal(
            "TrivialPropertyOrAccessor",
            Exemption(units, "Hairs.Core.ArtifactDescriptor.ProfileId"));
        Assert.Equal(
            "ParameterAssigningConstructor",
            Exemption(
                units,
                "Hairs.Core.ArtifactDescriptor.ArtifactDescriptor(ProfileId, uint, " +
                "FeatureManifestId, LimitVector, CallerIdentity)"));
        Assert.Equal(
            "DelegatingOverrideOrOperator",
            Exemption(units, "Hairs.Core.ArtifactDescriptor.Equals(object?)"));
        Assert.Equal(
            "DelegatingOverrideOrOperator",
            Exemption(
                units,
                "Hairs.Core.ArtifactDescriptor.operator ==(ArtifactDescriptor, ArtifactDescriptor)"));
    }

    [Fact(Timeout = 600000)]
    public void Negation_Is_Not_Delegation()
    {
        IReadOnlyList<AssuranceScannedUnit> units = new CSharpAssuranceScanner()
            .Scan(AssuranceFixture.Descriptor, "ArtifactDescriptor.cs");

        Assert.True(Unit(units, "Hairs.Core.ArtifactDescriptor.operator ==(ArtifactDescriptor, ArtifactDescriptor)").IsExempt);
        Assert.False(Unit(units, "Hairs.Core.ArtifactDescriptor.operator !=(ArtifactDescriptor, ArtifactDescriptor)").IsExempt);
    }

    [Fact(Timeout = 600000)]
    public void A_Type_Is_Fingerprinted_By_Its_Header_Alone()
    {
        var scanner = new CSharpAssuranceScanner();
        string edited = AssuranceFixture.Descriptor.Replace("FormatVersion >= 1", "FormatVersion >= 2", StringComparison.Ordinal);

        IReadOnlyList<AssuranceScannedUnit> after = scanner.Scan(edited, "x.cs");

        Assert.Equal("B53BCB", Fingerprint(after, "Hairs.Core.ArtifactDescriptor"));
        Assert.NotEqual("4A3BFD", Fingerprint(after, "Hairs.Core.ArtifactDescriptor.IsWellFormed"));
    }

    [Fact(Timeout = 600000)]
    public void A_Unit_Reports_The_Lines_It_Occupies()
    {
        IReadOnlyList<AssuranceScannedUnit> units = new CSharpAssuranceScanner()
            .Scan(AssuranceFixture.Descriptor, "ArtifactDescriptor.cs");

        AssuranceScannedUnit type = Unit(units, "Hairs.Core.ArtifactDescriptor");
        AssuranceScannedUnit member = Unit(units, "Hairs.Core.ArtifactDescriptor.IsWellFormed");

        Assert.True(type.DeclarationLine < member.DeclarationLine);
        Assert.True(type.EndLine > member.EndLine);
        Assert.True(member.EndLine >= member.DeclarationLine);
    }

    private static AssuranceScannedUnit Unit(IReadOnlyList<AssuranceScannedUnit> units, string name) =>
        units.Single(unit => unit.Name == name);

    private static string Fingerprint(IReadOnlyList<AssuranceScannedUnit> units, string name) =>
        Unit(units, name).Fingerprint;

    private static string Exemption(IReadOnlyList<AssuranceScannedUnit> units, string name) =>
        Unit(units, name).Exemption;
}
