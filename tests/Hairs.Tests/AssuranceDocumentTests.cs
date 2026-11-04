// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using Hairs.Assurance;
using Xunit;

namespace Hairs.Tests;

/// <summary>
/// The assurance model with no language service composed.
/// </summary>
public sealed class AssuranceDocumentTests
{
    private static readonly string Source = """
// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0
//
// HAIRS Code Assurance
// --------------------
// Relevant units:   2
// Annotated:        2/2
// Exempt:           1
// Human-reviewed:   0/2
// IP risk:          Low
// Security risk:    High
// Criteria:         1/1
// Resource impact:  2/10 max
// Unverified:       2
//
// GENERATED - DO NOT EDIT MANUALLY

namespace Sample;

/// <summary>A thing.</summary>
// Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=AAAAAA
// Hairs-Human:        PENDING
public sealed class Thing
{
    /// <summary>Does the work.</summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=2; Fingerprint=BBBBBB
    // Hairs-Falsified-If: the caller can observe a partially applied change
    // Hairs-Human:        PENDING
    public int Work(int value)
    {
        return value + 1;
    }
}
""".Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact(Timeout = 600000)]
    public void The_Units_Are_The_Annotated_Declarations()
    {
        AssuranceDocument document = AssuranceDocument.Read(Source);

        Assert.Equal(2, document.Units.Count);
        Assert.False(document.HasUnitScanner);
        Assert.True(document.HasBanner);
        Assert.Contains("class Thing", document.Units[0].DisplayName, StringComparison.Ordinal);
        Assert.Contains("Work", document.Units[1].DisplayName, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void An_Annotation_Reports_What_It_States()
    {
        AssuranceAnnotation annotation = AssuranceDocument.Read(Source).Units[1].Annotation!;

        Assert.Equal("AI", annotation.Field("Origin"));
        Assert.Equal("High", annotation.Field("Security"));
        Assert.Equal("2", annotation.Field("Resources"));
        Assert.Equal("BBBBBB", annotation.RecordedFingerprint);
        Assert.Equal("the caller can observe a partially applied change", annotation.Criterion);
        Assert.True(annotation.HumanIsPending);
        Assert.Null(annotation.Reviewer);
    }

    [Fact(Timeout = 600000)]
    public void The_Caret_Finds_The_Unit_It_Is_In()
    {
        AssuranceDocument document = AssuranceDocument.Read(Source);
        AssuranceUnit method = document.Units[1];

        Assert.Same(method, document.UnitAt(method.DeclarationLine));
        Assert.Same(method, document.UnitAt(method.Annotation!.HumanLine));
        Assert.Same(method, document.UnitAt(method.DeclarationLine + 2));
        Assert.Null(document.UnitAt(0));
    }

    [Fact(Timeout = 600000)]
    public void Approving_Writes_The_Reviewer_And_No_Fingerprint()
    {
        AssuranceDocument document = AssuranceDocument.Read(Source);
        AssuranceEditResult result = document.Approve(document.Units[1], "EB");

        Assert.True(result.Succeeded);
        Assert.Contains("    // Hairs-Human:        EB\n", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("EB; Fingerprint", result.Text, StringComparison.Ordinal);

        // The other unit is untouched, and so is every other line in the file.
        Assert.Contains("// Hairs-Human:        PENDING", result.Text, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void Approving_Changes_One_Line_And_No_Other()
    {
        AssuranceDocument document = AssuranceDocument.Read(Source);
        string after = document.Approve(document.Units[1], "EB").Text;

        string[] before = Source.Split('\n');
        string[] now = after.Split('\n');

        Assert.Equal(before.Length, now.Length);
        Assert.Single(before.Where((line, index) => !string.Equals(line, now[index], StringComparison.Ordinal)));
    }

    [Fact(Timeout = 600000)]
    public void A_Rewrite_Keeps_The_Line_Endings_It_Found()
    {
        string crlf = Source.Replace("\n", "\r\n", StringComparison.Ordinal);
        AssuranceDocument document = AssuranceDocument.Read(crlf);

        string after = document.Approve(document.Units[1], "EB").Text;

        Assert.Contains("    // Hairs-Human:        EB\r\n", after, StringComparison.Ordinal);

        Assert.Equal(crlf.Length, after.Length + "PENDING".Length - "EB".Length);
        Assert.DoesNotContain('\n', after.Replace("\r\n", string.Empty, StringComparison.Ordinal));
    }

    [Fact(Timeout = 600000)]
    public void Withdrawing_Restores_Pending()
    {
        AssuranceDocument approved = AssuranceDocument.Read(
            AssuranceDocument.Read(Source).Approve(AssuranceDocument.Read(Source).Units[1], "EB").Text);

        AssuranceEditResult result = approved.Withdraw(approved.Units[1]);

        Assert.True(result.Succeeded);
        Assert.Equal(2, Occurrences(result.Text, "// Hairs-Human:        PENDING"));
    }

    [Fact(Timeout = 600000)]
    public void An_Approval_Without_A_Name_Is_Refused()
    {
        AssuranceDocument document = AssuranceDocument.Read(Source);

        AssuranceEditResult result = document.Approve(document.Units[1], "   ");

        Assert.Equal(AssuranceEditOutcome.NoReviewer, result.Outcome);
        Assert.Equal(Source, result.Text);
        Assert.Contains("not evidence", result.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Enrico; Bartky")]
    [InlineData("enrico@example.com")]
    [InlineData("name=value")]
    [InlineData("PENDING")]
    [InlineData("STALE")]
    [InlineData("Pending")]
    [InlineData("NOT REVIEWED")]
    [InlineData("TODO")]
    [InlineData("EB​")]
    public void A_Name_The_Format_Cannot_Carry_Is_Refused(string reviewer)
    {
        AssuranceDocument document = AssuranceDocument.Read(Source);

        AssuranceEditResult result = document.Approve(document.Units[1], reviewer);

        Assert.Equal(AssuranceEditOutcome.NoReviewer, result.Outcome);
        Assert.Equal(Source, result.Text);
    }

    [Fact(Timeout = 600000)]
    public void An_Ordinary_Name_With_A_Space_Is_Written()
    {
        AssuranceDocument document = AssuranceDocument.Read(Source);

        AssuranceEditResult result = document.Approve(document.Units[1], "Enrico Bartky");

        Assert.True(result.Succeeded);
        Assert.Contains("// Hairs-Human:        Enrico Bartky", result.Text, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void Recording_The_Same_Decision_Twice_Is_Refused()
    {
        AssuranceDocument document = AssuranceDocument.Read(
            AssuranceDocument.Read(Source).Approve(AssuranceDocument.Read(Source).Units[1], "EB").Text);

        AssuranceEditResult result = document.Approve(document.Units[1], "EB");

        Assert.Equal(AssuranceEditOutcome.NothingToDo, result.Outcome);
    }

    [Fact(Timeout = 600000)]
    public void A_Reviewers_Own_Assessment_Survives_Their_Re_Signature()
    {
        string assessed = Source.Replace(
            "    // Hairs-Human:        PENDING",
            "    // Hairs-Human:        EB; Security=Critical; Fingerprint=BBBBBB",
            StringComparison.Ordinal);

        AssuranceDocument document = AssuranceDocument.Read(assessed);
        AssuranceUnit unit = document.Units[1];
        AssuranceAnnotation annotation = unit.Annotation!;

        Assert.Equal("EB", annotation.Reviewer);
        Assert.Equal("BBBBBB", annotation.HumanFingerprint);
        Assert.Equal(["Security=Critical"], annotation.HumanAssessment);

        Assert.Equal("EB; Security=Critical; Fingerprint=BBBBBB", AssuranceDocument.ApprovalBody(unit, "EB"));
        Assert.Equal("RV", AssuranceDocument.ApprovalBody(unit, "RV"));
    }

    [Fact(Timeout = 600000)]
    public void Signing_A_Unit_That_Already_Records_This_Reviewer_Changes_Nothing()
    {
        string assessed = Source.Replace(
            "    // Hairs-Human:        PENDING",
            "    // Hairs-Human:        EB; Fingerprint=BBBBBB",
            StringComparison.Ordinal);

        AssuranceDocument document = AssuranceDocument.Read(assessed);

        AssuranceEditResult result = document.Approve(document.Units[1], "EB");

        Assert.Equal(AssuranceEditOutcome.NothingToDo, result.Outcome);
        Assert.Equal(assessed, result.Text);
    }

    [Fact(Timeout = 600000)]
    public void Without_A_Scanner_The_Header_Is_Left_Alone()
    {
        AssuranceDocument document = AssuranceDocument.Read(Source);

        Assert.Null(document.Summary);
        Assert.False(document.BannerIsReproducible);

        AssuranceEditResult result = document.Approve(document.Units[1], "EB");

        Assert.True(result.Succeeded);
        Assert.False(result.HeaderUpdated);
        Assert.Contains("// Human-reviewed:   0/2", result.Text, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void A_File_With_No_Annotations_Reports_Nothing()
    {
        AssuranceDocument document = AssuranceDocument.Read("namespace Sample;\n\npublic sealed class Plain\n{\n}\n");

        Assert.Empty(document.Units);
        Assert.False(document.HasBanner);
        Assert.False(document.IsAnnotated);
        Assert.Null(document.UnitAt(2));
    }

    [Fact(Timeout = 600000)]
    public void A_Block_Without_A_Human_Line_Is_Not_An_Annotation()
    {
        AssuranceDocument document = AssuranceDocument.Read(
            "// Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=AAAAAA\n" +
            "public sealed class Thing\n{\n}\n");

        Assert.Empty(document.Units);
    }

    [Theory]
    [InlineData("    /// <summary>Doc.</summary>")]
    [InlineData("    [System.Obsolete]")]
    [InlineData("    /* a block comment */")]
    [InlineData("    #region Work")]
    [InlineData("")]
    public void The_Block_Survives_What_Can_Sit_Between_It_And_The_Declaration(string between)
    {
        const string Anchor = "    // Hairs-Human:        PENDING\n    public int Work(int value)";
        string source = Source.Replace(
            Anchor,
            "    // Hairs-Human:        PENDING\n" + between + "\n    public int Work(int value)",
            StringComparison.Ordinal);

        Assert.NotEqual(Source, source);

        AssuranceDocument document = AssuranceDocument.Read(source);

        Assert.Equal(2, document.Units.Count);
        Assert.NotNull(document.Units[1].Annotation);
    }

    private static int Occurrences(string text, string value)
    {
        int count = 0;
        int index = text.IndexOf(value, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);
        }

        return count;
    }
}
