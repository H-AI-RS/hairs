// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0

namespace Hairs.SupplyChain;

/// <summary>
/// Categorization of software supply chain assets audited for integrity and review freshness.
/// </summary>
public enum SupplyChainAssetType
{
    /// <summary>CI/CD workflows, build scripts, deployment recipes, packaging scripts.</summary>
    BuildPipeline = 0,

    /// <summary>Central package management, project references, lock files, package feeds.</summary>
    PackageManifest = 1,

    /// <summary>Tool configs, repository health rules, git attributes, security policies.</summary>
    Configuration = 2,

    /// <summary>Source units or vendored files marked with Origin=ThirdParty or Origin=Ported.</summary>
    ThirdPartyCode = 3,
}
