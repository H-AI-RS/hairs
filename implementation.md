# HAIRS Implementation and Architecture Map

The HAIRS project is structured as a standalone multi-project solution in `Hairs.slnx`.

All components are written in modern C# (.NET 10, C# 13, `TreatWarningsAsErrors=true`) and strictly isolated under the `Hairs` namespace hierarchy.

---

## Architecture Overview

```
                      +-----------------------------+
                      |         Hairs.Cli           |
                      | (hairs tool / executable)   |
                      +--------------+--------------+
                                     |
                                     v
                      +-----------------------------+
                      |            Hairs            |
                      |  (Core Library: DLL)        |
                      +--------------+--------------+
                      |                             |
                      v                             v
           +--------------------+         +--------------------+
           |   Hairs.Review     |         |   Hairs.Assurance  |
           | File Review Engine |         |  Declaration Gate  |
           +--------------------+         +--------------------+
                      |                             |
                      +--------------+--------------+
                                     |
                                     v
                      +-----------------------------+
                      |    Hairs.Language.CSharp    |
                      |    (Roslyn AST Scanning)    |
                      +-----------------------------+
```

---

## Module and Type Map

### 1. Storage Abstraction (`Hairs.Storage`)

Decouples all review and assurance disk operations from specific editor harnesses.

| Type | Path | Purpose |
|---|---|---|
| `IHairsStorage` | [IHairsStorage.cs](src/Hairs/Storage/IHairsStorage.cs) | Pure storage contract (read/write/delete/exists). |
| `FileSystemHairsStorage` | [FileSystemHairsStorage.cs](src/Hairs/Storage/FileSystemHairsStorage.cs) | Physical file system implementation of storage. |
| `HairsPath` | [HairsPath.cs](src/Hairs/Storage/HairsPath.cs) | Normalized path utility with case-folding support. |
| `StorageResult` | [StorageResult.cs](src/Hairs/Storage/StorageResult.cs) | Safe outcome wrapper for atomic storage operations. |

### 2. File Review Engine (`Hairs`)

Content-addressed verification and note tracking over entire source files.

| Concern | Primary Source | Description |
|---|---|---|
| Review Decisions | [FileReview.cs](src/Hairs/FileReview.cs) | Decisions (`Reviewed`, `Question`, `NeedsChange`, `InReview`, `Unreviewed`). |
| Review Freshness | [ReviewStatus.cs](src/Hairs/ReviewStatus.cs) | Freshness evaluation (`Current`, `Stale`, `NotReviewed`, `Unknown`). |
| Freshness Evaluator | [ReviewStateEvaluator.cs](src/Hairs/ReviewStateEvaluator.cs) | Compares normalized content hash with recorded review hash. |
| Content Hash | [ReviewContentHash.cs](src/Hairs/ReviewContentHash.cs) | Computes `sha256-nlf:` over normalized LF content (stripping BOM). |
| Review Persistence | [ReviewStore.cs](src/Hairs/ReviewStore.cs) | JSON serialization under `.hairs-review/`. |
| Deterministic JSON | [ReviewJson.cs](src/Hairs/ReviewJson.cs) | Canonical JSON writer without key sorting differences. |
| Anchored Notes | [ReviewNote.cs](src/Hairs/ReviewNote.cs) | Structured questions, concerns, todos, and observations. |
| Note Relocation | [NoteAnchoring.cs](src/Hairs/NoteAnchoring.cs) | Tracks notes across code edits (`Anchored`, `Moved`, `Ambiguous`, `Orphaned`). |
| Coverage Computation | [ReviewCoverage.cs](src/Hairs/ReviewCoverage.cs) | Computes reviewed, stale, and unreviewed metrics across a tree. |

### 3. Supply Chain Integrity Engine (`Hairs.SupplyChain`)

Audits repository assets critical to software supply chain security and release integrity.

| Type | Path | Purpose |
|---|---|---|
| `SupplyChainAssetType` | [SupplyChainAssetType.cs](src/Hairs/SupplyChain/SupplyChainAssetType.cs) | Categorization (`BuildPipeline`, `PackageManifest`, `Configuration`, `ThirdPartyCode`). |
| `SupplyChainAsset` | [SupplyChainAsset.cs](src/Hairs/SupplyChain/SupplyChainAsset.cs) | Immutable record representing a supply chain asset and its review freshness. |
| `SupplyChainAuditReport` | [SupplyChainAuditReport.cs](src/Hairs/SupplyChain/SupplyChainAuditReport.cs) | Audit results with totals, compliance checks, and Markdown/JSON reporting. |
| `SupplyChainAuditor` | [SupplyChainAuditor.cs](src/Hairs/SupplyChain/SupplyChainAuditor.cs) | Discovers and audits workflows, build scripts, CPM manifests, and configs. |

### 4. Declaration Code Assurance Engine (`Hairs.Assurance`)

In-source annotation parsing, state machines, rubric rules, and artifact generation.

| Concern | Primary Source | Description |
|---|---|---|
| Vocabulary & Constants | [AssuranceVocabulary.cs](src/Hairs/Assurance/AssuranceVocabulary.cs) | Allowed origins, risks, markers (`// Hairs-AI:`, `// Hairs-Human:`). |
| In-Source Annotation | [AssuranceAnnotation.cs](src/Hairs/Assurance/AssuranceAnnotation.cs) | Strict parser and formatter for machine and human annotation blocks. |
| Human Decision Line | [AssuranceHumanLine.cs](src/Hairs/Assurance/AssuranceHumanLine.cs) | Parses and validates human reviewer signatures and timestamps. |
| Assurance State Machine | [AssuranceUnitState.cs](src/Hairs/Assurance/AssuranceUnitState.cs) | Transition logic between `NEW`, `AI_ASSESSED`, `HUMAN_PENDING`, `VERIFIED`, `STALE`. |
| Header Banners | [AssuranceBanner.cs](src/Hairs/Assurance/AssuranceBanner.cs) | Formats and validates the 20-dash generated header banner. |
| Header Manipulation | [AssuranceHeader.cs](src/Hairs/Assurance/AssuranceHeader.cs) | Safely detects, strips, and writes generated top-of-file headers. |
| Rubric Rules & Validation | [AssuranceRules.cs](src/Hairs/Assurance/AssuranceRules.cs) | Enforces criterion obligations for High/Critical risk units. |
| Component Configuration | [AssuranceComponentConfig.cs](src/Hairs/Assurance/AssuranceComponentConfig.cs) | Parses and validates `hairs.config.json` schema. |
| Artifact Generation | [AssuranceGenerator.cs](src/Hairs/Assurance/AssuranceGenerator.cs) | Generates `CODE-ASSURANCE.md`, `HUMAN_REVIEW.md`, and `hairs.manifest.json`. |
| Consistency Checks (J1–J11) | [AssuranceChecks.cs](src/Hairs/Assurance/AssuranceChecks.cs) | Rule validator verifying annotations, headers, manifests, and release gates. |
| Assessment Insertion | [AssuranceInsertion.cs](src/Hairs/Assurance/AssuranceInsertion.cs) | Idempotent insertion of machine blocks above AST declarations. |
| Rubric Pruning | [AssurancePruning.cs](src/Hairs/Assurance/AssurancePruning.cs) | Removes obsolete annotations and below-High criteria safely. |

### 5. Roslyn C# Language Scanner (`Hairs.Language.CSharp`)

High-performance semantic and AST parsing powered by `Microsoft.CodeAnalysis.CSharp`.

| Concern | Primary Source | Description |
|---|---|---|
| AST Scanner | [CSharpAssuranceScanner.cs](src/Hairs/Language/CSharp/CSharpAssuranceScanner.cs) | Extracts C# types, methods, properties, and computes 6-char hex fingerprints. |
| File Scanner | [CSharpAssuranceFileScanner.cs](src/Hairs/Language/CSharp/CSharpAssuranceFileScanner.cs) | Maps tokens, leading trivia, doc comments, and attributes per file. |
| Component Scanner | [CSharpComponentAssuranceScanner.cs](src/Hairs/Language/CSharp/CSharpComponentAssuranceScanner.cs) | Resolves the nearest `hairs.config.json` for per-project scanning rules. |

### 6. Standalone CLI Executable (`Hairs.Cli`)

The command-line interface packed as `hairs`.

| Command / Component | Source | Purpose |
|---|---|---|
| Entry Point & Router | [Program.cs](src/Hairs.Cli/Program.cs) | Top-level dispatcher for `coverage`, `check`, `list`, `supply-chain`, `assurance`. |
| Supply Chain Audit | [Program.cs](src/Hairs.Cli/Program.cs) | Implements `hairs supply-chain` audit and CI gate. |
| Source Inventory | [SourceInventory.cs](src/Hairs.Cli/SourceInventory.cs) | Discovers project sources and collapses nested checkouts. |
| Coverage Reporting | [ReviewReport.cs](src/Hairs.Cli/ReviewReport.cs) | Renders file review coverage in Markdown and JSON. |
| Project Discovery | [ComponentSources.cs](src/Hairs.Cli/Assurance/ComponentSources.cs) | Evaluates MSBuild project files and compiles item sets without MSBuild exe. |
| Corpus Loader | [ComponentCorpus.cs](src/Hairs.Cli/Assurance/ComponentCorpus.cs) | Loads and scans all covered files in a component. |
| Safe File I/O | [AssuranceSourceText.cs](src/Hairs.Cli/Assurance/AssuranceSourceText.cs) | Strict UTF-8 and BOM reader/writer with change verification. |
| Gate Commands | [AssuranceGateCommands.cs](src/Hairs.Cli/Assurance/AssuranceGateCommands.cs) | Implements `assurance generate`, `assurance check`, and `assurance status`. |
| Prune Command | [AssurancePruneCommand.cs](src/Hairs.Cli/Assurance/AssurancePruneCommand.cs) | Implements `assurance prune`. |
| CLI Router | [AssuranceCommand.cs](src/Hairs.Cli/Assurance/AssuranceCommand.cs) | Parses command-line options and routes assurance commands. |

---

## Test Suite (`tests/Hairs.Tests/`)

The test suite contains 392 tests running on xUnit v3 In-Process Runner:

- `ReviewRecordTests`: Decision recording, timestamps, content hash verification.
- `ReviewStoreTests`: Serialization round-tripping, file deletion.
- `ReviewCoverageTests`: Coverage arithmetic and bucket calculations.
- `SupplyChainTests`: Asset classification, audit reports, staleness detection, and Markdown/JSON serialization.
- `NoteAnchoringTests`: Relocation of notes across multiline code modifications.
- `AssuranceScannerTests`: Roslyn AST unit extraction and fingerprint reproducibility.
- `AssuranceFileScannerTests`: Line-level mapping of trivia and declarations.
- `AssuranceScannerComponentTests`: Directives, raw strings, CRLF literals, top-level statements.
- `ComponentAssuranceScannerTests`: Per-component configuration isolation.
- `AssuranceRulesTests`: Falsification criteria, vocabulary validation, and risk inheritance.
- `AssuranceConfigTests`: Schema parsing and validation of `hairs.config.json`.
- `AssuranceGeneratorTests`: Deterministic generation of headers, markdown reports, and manifests.
- `AssuranceCheckTests`: Complete verification of check rules J1 through J11.
- `AssuranceDocumentTests`: Editor document model, human approval recording, and line preservation.
- `AssuranceInsertTests`: Idempotent insertion of assessments and CRLF/BOM preservation.
- `AssurancePruneCommandTests`: Safe pruning of obsolete rubric entries.
- `AssuranceDiscoveryTests`: Project evaluation, linked file exclusions, and file discovery.
- `AssuranceGenerateCommandTests`: CLI-level testing of `generate`, `check`, `status`, and GitHub actions formatting.
