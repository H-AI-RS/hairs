# HAIRS — Human AI Review System

**HAIRS** is an autonomous, standalone code review and code assurance engineering framework. It provides a formal system for auditing codebases, recording human and AI attestations, tracking content freshness, and enforcing release readiness through deterministic verification gates.

HAIRS stands entirely on its own as a standalone .NET 10 solution (`Hairs.slnx`) with zero dependencies on external editor hosts or platform frameworks.

---

## Why HAIRS?

Modern software development increasingly relies on automation, AI-assisted coding, and contributions from distributed teams.

At the same time, incidents such as the xz Utils backdoor (2024), the event-stream compromise (2018), and the SolarWinds supply-chain attack (2020) have demonstrated how difficult it can be to maintain trust across source code, dependencies, build processes, and software releases.

These incidents were not caused by AI-generated code. However, they highlight broader challenges involving code provenance, review accountability, and release integrity.

HAIRS (Human–AI Review System) is designed to address these challenges through traceable, source-bound human reviews, risk classification, and automated release validation.

Its goal is not to guarantee that software is free of vulnerabilities, but to make review coverage measurable, changes traceable, and release decisions accountable.

> **AI can accelerate development. Human accountability must remain verifiable.**

---

## Highlights & Capabilities

- **Zero Coupling / Pure Standalone:** Owns its solution (`Hairs.slnx`), shared core library (`Hairs`), CLI tool (`Hairs.Cli`), and unit test suite (`Hairs.Tests`).
- **Dual-Layer Assurance:**
  - **File Review Engine:** Content-addressed file freshness with normalized SHA-256 hashes (`sha256-nlf:`), structured reviewer decisions, and anchored review notes.
  - **Declaration Assurance Gate:** In-source code annotations (`// Hairs-AI:`, `// Hairs-Human:`, `// Hairs-Falsified-If:`) with Roslyn-driven structural fingerprinting, state machines, and release verification gates.
- **Strict Verification Standards:** Built with `LangVersion=latest`, `Nullable=enable`, and `TreatWarningsAsErrors=true`.

---

## Solution Structure

```
hairs/
├── Hairs.slnx                  # Standalone solution
├── Directory.Build.props       # .NET 10, Nullable, TreatWarningsAsErrors
├── Directory.Packages.props    # Central Package Management (CPM)
├── src/
│   ├── Hairs/                  # Core shared library (Hairs.dll)
│   │   ├── Storage/            # IHairsStorage, FileSystemHairsStorage, HairsPath
│   │   ├── Review/             # FileReview, ReviewStore, ReviewCoverage, ReviewContentHash
│   │   ├── SupplyChain/        # SupplyChainAuditor, SupplyChainAsset, SupplyChainAuditReport
│   │   ├── Assurance/          # AssuranceUnit, AssuranceAnnotation, AssuranceGenerator, AssuranceChecks
│   │   └── Language/CSharp/    # CSharpAssuranceScanner, CSharpAssuranceFileScanner (Roslyn)
│   └── Hairs.Cli/              # CLI executable & dotnet tool (hairs)
│       ├── Program.cs          # Entry point (coverage, check, list, supply-chain, assurance)
│       └── Assurance/          # Discovery, insertion, pruning, generation, gate commands
├── tests/
│   └── Hairs.Tests/            # Comprehensive unit test suite (392 tests, xUnit v3)
└── *.md                        # In-depth architectural & workflow documentation
```

---

## Installation & CLI Usage

### Building

Build the entire solution:

```powershell
dotnet build Hairs.slnx -c Release
```

Run all 392 unit tests:

```powershell
dotnet run --project tests/Hairs.Tests -c Release
```

### CLI Commands

The CLI executable is `Hairs.Cli.exe` (tool command name `hairs`).

#### 1. File Review Coverage & Freshness

- **Generate Coverage Report:**
  ```powershell
  hairs coverage --root . --markdown coverage.md --json coverage.json
  ```
- **Check File Freshness against Changed Files:**
  ```powershell
  hairs check --root . --changed changed-files.txt --fail-on-stale
  ```

#### 2. Supply Chain Integrity Review

Audit repository assets critical to supply chain security (CI/CD workflows, build scripts, CPM package definitions, tool configurations, and third-party code):

- **Audit Supply Chain Assets:**
  ```powershell
  hairs supply-chain --root . --markdown supply-chain.md --json supply-chain.json
  ```
- **Enforce Supply Chain Review Gate in CI (Fail on stale or unreviewed assets):**
  ```powershell
  hairs supply-chain --root . --fail-on-stale --minimum 100
  ```

#### 3. Code Assurance Gate

- **Inspect Current Status:**
  ```powershell
  hairs assurance status --root .
  ```
- **Discover Unannotated Units:**
  ```powershell
  hairs assurance list --root . --json units.json
  ```
- **Insert Machine Assessments:**
  ```powershell
  hairs assurance insert --root . --assessments assessments.json
  ```
- **Prune Obsolete Blocks / Criteria:**
  ```powershell
  hairs assurance prune --root .
  ```
- **Generate Headers, Manifest & Report:**
  ```powershell
  hairs assurance generate --root .
  ```
- **Run Verification Consistency Checks:**
  ```powershell
  hairs assurance check --root .
  ```
- **Enforce Release Gate (All relevant units verified):**
  ```powershell
  hairs assurance check --root . --release
  ```

---

## NuGet Packaging & CI/CD Workflow

HAIRS publishes two packages:
- **`Hairs`**: Core shared library for code review, file hashing, and declaration assurance.
- **`Hairs.Cli`**: Standalone .NET CLI tool (`hairs`), packable as a global/local dotnet tool.

### Packaging & Release

The GitHub Actions workflow [`.github/workflows/hairs-nuget.yml`](.github/workflows/hairs-nuget.yml) automates building, testing, packing, and publishing.

- **Manual Trigger (`workflow_dispatch`):**
  Run the workflow manually via GitHub Actions UI / CLI. The `preview_number` defaults to `auto`, which queries NuGet.org and auto-increments to the next preview number (e.g. from `preview.1` to `preview.2`). You can also specify an exact preview number, provide a `version_override`, or check `dry_run`.
- **Local Packaging:**
  Run [`pack.ps1`](pack.ps1) locally:
  ```powershell
  ./pack.ps1 -Preview 1
  ```

---

## Configuration (`hairs.config.json`)

Each component or project opts in by placing a `hairs.config.json` in its directory:

```json
{
  "schema": 1,
  "component": "MyComponent",
  "projects": [
    "src/MyComponent/MyComponent.csproj"
  ],
  "exclude": [
    { "glob": "**/*.g.cs", "reason": "generated code" }
  ],
  "artefacts": {
    "report": "CODE-ASSURANCE.md",
    "humanReview": "HUMAN_REVIEW.md",
    "manifest": "hairs.manifest.json"
  },
  "spdx": {
    "copyright": [ "2026 Contributor Name" ],
    "license": "Apache-2.0"
  }
}
```

---

## In-Source Assurance Format

Above relevant C# declarations (classes, structs, methods, properties), HAIRS records:

```csharp
// Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=2; Fingerprint=4A3BFD
// Hairs-Falsified-If: an unauthorized user can read protected memory
// Hairs-Human:        EB; Fingerprint=4A3BFD
public void Execute(Request request)
{
    // ...
}
```

- `Origin`: `AI`, `Original`, `Specification`, `Derived`, `Ported`, `ThirdParty`.
- `IP`: `None`, `Low`, `Medium`, `High`, `Unknown`.
- `Security`: `None`, `Low`, `Medium`, `High`, `Critical` (`High` and `Critical` require `// Hairs-Falsified-If:`).
- `Resources`: Resource impact rating 0–10.
- `Fingerprint`: Deterministic 6-character hex hash computed by Roslyn AST parsing.
- `// Hairs-Human:`: The human reviewer's signature, bound to the structural fingerprint upon review.

---

## Detailed Documentation

- [Concepts and Trust Model](concepts.md): Three review layers, evidence types, state machines, and scoring rubric.
- [Workflow and CI](workflow.md): Step-by-step assessment, human signing, and CI release gates.
- [Implementation Map](implementation.md): Complete mapping of types, modules, and architecture in `Hairs`.
- [Findings & Architectural Decisions](findings.md): Resolved differences, clean boundaries, and safety guarantees.
