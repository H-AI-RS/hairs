// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

using System;

namespace Hairs.Assurance;

public enum AssuranceUnitState
{
    New = 0,
    AiAssessed,
    HumanPending,
    HumanApprovedPendingFingerprint,
    Verified,
    Stale,
    Exempt,
    Unknown,
}

public static class AssuranceStateMachine
{
    public static AssuranceUnitState Resolve(
        AssuranceAnnotation? annotation,
        bool isExemptByPredicate,
        string? currentFingerprint)
    {
        if (isExemptByPredicate || annotation?.ExemptReason is not null)
            return AssuranceUnitState.Exempt;

        if (annotation is null)
            return AssuranceUnitState.New;

        if (annotation.HumanIsStale)
            return AssuranceUnitState.Stale;

        string? recorded = annotation.RecordedFingerprint;
        if (recorded is null ||
            string.Equals(recorded, AssuranceVocabulary.ToBeFilled, StringComparison.Ordinal))
        {
            return AssuranceUnitState.AiAssessed;
        }

        if (annotation.HumanIsPending || annotation.Reviewer is null)
            return AssuranceUnitState.HumanPending;

        string? approved = annotation.HumanFingerprint;
        if (approved is null ||
            string.Equals(approved, AssuranceVocabulary.ToBeFilled, StringComparison.Ordinal))
        {
            return AssuranceUnitState.HumanApprovedPendingFingerprint;
        }

        if (currentFingerprint is null)
            return AssuranceUnitState.Unknown;

        return string.Equals(approved, currentFingerprint, StringComparison.Ordinal)
            ? AssuranceUnitState.Verified
            : AssuranceUnitState.Stale;
    }

    public static bool BlocksRelease(AssuranceUnitState state) =>
        state is not (AssuranceUnitState.Verified or AssuranceUnitState.Exempt);

    public static string Name(AssuranceUnitState state) => state switch
    {
        AssuranceUnitState.New => "NEW",
        AssuranceUnitState.AiAssessed => "AI_ASSESSED",
        AssuranceUnitState.HumanPending => "HUMAN_PENDING",
        AssuranceUnitState.HumanApprovedPendingFingerprint => "HUMAN_APPROVED_PENDING_FINGERPRINT",
        AssuranceUnitState.Verified => "VERIFIED",
        AssuranceUnitState.Stale => "STALE",
        AssuranceUnitState.Exempt => "EXEMPT",
        _ => "fingerprint unknown",
    };

    public static string ToDisplayString(AssuranceUnitState state) => state switch
    {
        AssuranceUnitState.New => "not assessed",
        AssuranceUnitState.AiAssessed => "assessed, fingerprint not yet generated",
        AssuranceUnitState.HumanPending => "needs human review",
        AssuranceUnitState.HumanApprovedPendingFingerprint => "approved, fingerprint to be generated",
        AssuranceUnitState.Verified => "human reviewed",
        AssuranceUnitState.Stale => "reviewed, then modified",
        AssuranceUnitState.Exempt => "exempt",
        _ => "approved, freshness unknown in this build",
    };
}
