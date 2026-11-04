// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

namespace Hairs.Tests;

/// <summary>
/// A real annotated source file, kept verbatim so that several tests can assert
/// against the same evidence.
/// </summary>
internal static class AssuranceFixture
{
    internal const string Descriptor = """
// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0
//
// HAIRS Code Assurance
// --------------------
// Relevant units:   4
// Annotated:        4/4
// Exempt:           9
// Human-reviewed:   0/4
// IP risk:          Low
// Security risk:    Low
// Criteria:         0/0
// Resource impact:  3/10 max
// Unverified:       4
//
// GENERATED - DO NOT EDIT MANUALLY

namespace Hairs.Core;

/// <summary>
/// The caller-supplied description of an artifact.
/// </summary>
// Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=B53BCB
// Hairs-Human:        PENDING
public readonly struct ArtifactDescriptor : System.IEquatable<ArtifactDescriptor>
{
    /// <summary>Creates an artifact descriptor.</summary>
    public ArtifactDescriptor(
        ProfileId profileId,
        uint formatVersion,
        FeatureManifestId featureManifestId,
        LimitVector requestedLimits,
        CallerIdentity callerIdentity)
    {
        ProfileId = profileId;
        FormatVersion = formatVersion;
        FeatureManifestId = featureManifestId;
        RequestedLimits = requestedLimits;
        CallerIdentity = callerIdentity;
    }

    /// <summary>The profile whose verifier owns these bytes.</summary>
    public ProfileId ProfileId { get; }

    /// <summary>Exactly one profile-format version. Not a range.</summary>
    public uint FormatVersion { get; }

    /// <summary>Exactly one feature manifest. Not a set.</summary>
    public FeatureManifestId FeatureManifestId { get; }

    /// <summary>Limits the artifact requests.</summary>
    public LimitVector RequestedLimits { get; }

    /// <summary>The caller's own identity for these bytes.</summary>
    public CallerIdentity CallerIdentity { get; }

    /// <summary>Whether the descriptor is structurally usable.</summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=4A3BFD
    // Hairs-Human:        PENDING
    public bool IsWellFormed =>
        !ProfileId.IsEmpty &&
        FormatVersion >= 1 &&
        !FeatureManifestId.IsEmpty &&
        FeatureManifestId.StartsWithProfileNamespace(ProfileId);

    /// <inheritdoc/>
    // Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=3; Fingerprint=7A6D21
    // Hairs-Human:        PENDING
    public bool Equals(ArtifactDescriptor other) =>
        ProfileId.Equals(other.ProfileId) &&
        FormatVersion == other.FormatVersion &&
        FeatureManifestId.Equals(other.FeatureManifestId) &&
        RequestedLimits.Equals(other.RequestedLimits) &&
        CallerIdentity.Equals(other.CallerIdentity);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ArtifactDescriptor other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        System.HashCode.Combine(ProfileId, FormatVersion, FeatureManifestId, RequestedLimits, CallerIdentity);

    /// <summary>Value equality.</summary>
    public static bool operator ==(ArtifactDescriptor left, ArtifactDescriptor right) => left.Equals(right);

    /// <summary>Value inequality.</summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=74B2DC
    // Hairs-Human:        PENDING
    public static bool operator !=(ArtifactDescriptor left, ArtifactDescriptor right) => !left.Equals(right);
}
""";
}
