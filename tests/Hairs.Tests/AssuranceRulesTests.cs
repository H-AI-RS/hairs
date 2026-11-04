// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;
using Hairs.Assurance;

namespace Hairs.Tests;

/// <summary>
/// Strict block parse and vocabulary checks.
/// </summary>
public sealed class AssuranceRulesTests
{
    private static AssuranceAnnotation? Parse(string text, out string? problem)
    {
        AssuranceAnnotation.TryParseStrict(new AssuranceLines(text), 0, out AssuranceAnnotation? annotation, out problem);
        return annotation;
    }

    [Fact(Timeout = 600000)]
    public void Keys_And_Values_Are_Trimmed_Around_The_Equals_Sign()
    {
        AssuranceAnnotation annotation = Parse(
            "// Hairs-AI: Origin = AI ;IP=Low; Security=Low; Resources=3; Fingerprint=TBF\n// Hairs-Human: PENDING",
            out _)!;

        Assert.Equal("AI", annotation.Field("Origin"));
        Assert.Equal("Low", annotation.Field("IP"));
        Assert.Empty(AssuranceRules.VocabularyProblems(annotation));
    }

    [Theory(Timeout = 600000)]
    [InlineData("// Hairs-AI: Origin=AI; Low\n// Hairs-Human: PENDING", "line 1 has a field with no '=': 'Low'")]
    [InlineData("// Hairs-AI: ;\n// Hairs-Human: PENDING", "line 1 carries no fields")]
    [InlineData("// Hairs-AI: Origin=AI", "line 1 has no '// Hairs-Human:' line under it")]
    [InlineData("// Hairs-AI: Origin=AI\n\n// Hairs-Human: PENDING", "line 1 is not immediately followed by a '// Hairs-Human:' line")]
    [InlineData(
        "// Hairs-AI: Origin=AI\n// Hairs-Falsified-If: a\n// Hairs-Falsified-If: b\n// Hairs-Human: PENDING",
        "line 3 carries a second '// Hairs-Falsified-If:' line, and a falsification criterion is one line")]
    public void A_Block_Refused_Is_Refused_With_Its_Reason(string text, string expected)
    {
        Assert.Null(Parse(text, out string? problem));
        Assert.Equal(expected, problem);
    }

    [Fact(Timeout = 600000)]
    public void An_Empty_Criterion_Line_Is_A_Line_That_Says_Nothing()
    {
        AssuranceAnnotation annotation = Parse(
            "// Hairs-AI: Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=TBF\n" +
            "// Hairs-Falsified-If:\n" +
            "// Hairs-Human: PENDING",
            out _)!;

        Assert.True(annotation.HasCriterionLine);
        Assert.False(annotation.HasCriterion);
        Assert.Equal(["// Hairs-Falsified-If: carries no criterion"], AssuranceRules.VocabularyProblems(annotation));
    }

    [Fact(Timeout = 600000)]
    public void Every_Field_Is_Held_To_Its_Vocabulary()
    {
        AssuranceAnnotation annotation = Parse(
            "// Hairs-AI: Origin=Human; IP=Some; Security=Severe; Resources=11; CS=12; CL=-1; Fingerprint=abc123; Spec=; Extra=1\n" +
            "// Hairs-Human: PENDING",
            out _)!;

        Assert.Equal(
            [
                "Origin=Human is outside its vocabulary (Original, AI, Specification, Derived, Ported, ThirdParty)",
                "IP=Some is outside its vocabulary (None, Low, Medium, High, Unknown)",
                "Security=Severe is outside its vocabulary (None, Low, Medium, High, Critical)",
                "Resources=11 is not an integer 0 to 10",
                "CS=12 is not an integer 0 to 10",
                "CL=-1 is not an integer 0 to 10",
                "Fingerprint=abc123 is neither TBF nor six uppercase hex characters",
                "Spec= is empty",
                "Extra=1 is not a field this system defines",
            ],
            AssuranceRules.VocabularyProblems(annotation));
    }

    [Fact(Timeout = 600000)]
    public void A_Missing_Required_Field_Is_Named()
    {
        AssuranceAnnotation annotation = Parse("// Hairs-AI: Origin=AI\n// Hairs-Human: PENDING", out _)!;

        Assert.Equal(
            ["no IP field", "no Security field", "no Resources field", "no Fingerprint field"],
            AssuranceRules.VocabularyProblems(annotation));
    }

    [Fact(Timeout = 600000)]
    public void An_Exemption_Is_Its_Own_Field_And_Carries_A_Reason()
    {
        Assert.Equal(
            ["EXEMPT carries no reason"],
            AssuranceRules.VocabularyProblems(Parse("// Hairs-AI: EXEMPT=\n// Hairs-Human: PENDING", out _)!));
        Assert.Equal(
            ["EXEMPT is stated beside other fields; an exemption is not an assessment"],
            AssuranceRules.VocabularyProblems(Parse("// Hairs-AI: EXEMPT=x; IP=Low\n// Hairs-Human: PENDING", out _)!));
    }

    [Theory(Timeout = 600000)]
    [InlineData("the count == 0 after a reset, or x != y, or a <= b")]
    [InlineData("a negative value reaches the running total")]
    public void Prose_Is_A_Criterion(string criterion) =>
        Assert.Empty(AssuranceRules.CriterionProblems(criterion));

    [Fact(Timeout = 600000)]
    public void A_Field_On_A_Criterion_Is_Data_And_Refused()
    {
        Assert.Equal(
            ["// Hairs-Falsified-If: states the field Security=Low, and a falsification criterion is prose, not data"],
            AssuranceRules.CriterionProblems("it is really Security=Low"));
        Assert.Equal(
            ["// Hairs-Falsified-If: states the field CS=10, and a falsification criterion is prose, not data"],
            AssuranceRules.CriterionProblems("it is really CS=10"));
        Assert.Equal(
            ["// Hairs-Falsified-If: states the field CL=0, and a falsification criterion is prose, not data"],
            AssuranceRules.CriterionProblems("it is really CL=0"));
    }

    [Fact(Timeout = 600000)]
    public void A_Criterion_Never_Claims_A_Review()
    {
        Assert.Equal(
            [
                "// Hairs-Falsified-If: claims a review by saying 'signed off', and a falsification criterion states what would make the unit wrong, never that anyone read it",
            ],
            AssuranceRules.CriterionProblems("Unverified input is returned; this was Signed  Off"));

        Assert.Equal(
            ["approves", "lgtm", "signed-off", "verified"],
            AssuranceRules.ReviewClaimsIn("never: EB approves this unit, LGTM and signed-off; it is verified"));
    }

    [Theory(Timeout = 600000)]
    [InlineData("an unverified receiver is accepted as a class instance")]
    [InlineData("a cookie with SameSite=None is sent on a cross-site request")]
    [InlineData("a certificate that fails chain verification is accepted")]
    public void Prose_About_Verification_And_Protocol_Fields_Is_A_Criterion(string criterion) =>
        Assert.Empty(AssuranceRules.CriterionProblems(criterion));

    [Fact(Timeout = 600000)]
    public void An_Exemption_Reason_Or_A_Spec_Never_Claims_A_Review()
    {
        AssuranceAnnotation exempt = Parse(
            "// Hairs-AI: EXEMPT=signed-off by EB after human review, LGTM\n// Hairs-Human: PENDING", out _)!;
        Assert.Equal(
            ["EXEMPT=signed-off by EB after human review, LGTM claims a review by saying 'signed-off', and an exemption states why a unit needs no review, never that it had one",
             "EXEMPT=signed-off by EB after human review, LGTM claims a review by saying 'human review', and an exemption states why a unit needs no review, never that it had one",
             "EXEMPT=signed-off by EB after human review, LGTM claims a review by saying 'lgtm', and an exemption states why a unit needs no review, never that it had one"],
            AssuranceRules.VocabularyProblems(exempt));

        AssuranceAnnotation spec = Parse(
            "// Hairs-AI: Origin=AI; Spec=reviewed and approved by EB; IP=Low; Security=Low; Resources=0; Fingerprint=TBF\n// Hairs-Human: PENDING",
            out _)!;
        Assert.Equal(
            ["Spec=reviewed and approved by EB claims a review by saying 'approved', and a Spec cites what the unit implements, never who read it"],
            AssuranceRules.VocabularyProblems(spec));
    }

    [Theory(Timeout = 600000)]
    [InlineData("0", true)]
    [InlineData("10", true)]
    [InlineData("+5", true)]
    [InlineData("11", false)]
    [InlineData("-1", false)]
    [InlineData("five", false)]
    public void Score_Is_An_Integer_From_Zero_To_Ten(string value, bool valid)
    {
        Assert.Equal(valid, AssuranceRules.TryParseScore(value, out _));
        Assert.Equal(valid, AssuranceRules.TryParseResources(value, out _));
    }

    [Fact(Timeout = 600000)]
    public void A_Stale_Line_Keeps_The_Previous_Reviewer()
    {
        AssuranceAnnotation annotation = Parse(
            "// Hairs-AI: Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=06FA02\n" +
            "// Hairs-Human: STALE; Previous=EB@112233",
            out _)!;

        Assert.Equal(("EB", "112233"), annotation.Previous);
    }
}
