# HAIRS Concepts and Trust Model

HAIRS operates on a foundational invariant:

> **Automation may assess, measure, calculate fingerprints, and invalidate stale records; automation may NEVER invent or forge a human decision.**

---

## Why HAIRS?

Modern software development increasingly relies on automation, AI-assisted coding, and contributions from distributed teams.

At the same time, incidents such as the xz Utils backdoor (2024), the event-stream compromise (2018), and the SolarWinds supply-chain attack (2020) have demonstrated how difficult it can be to maintain trust across source code, dependencies, build processes, and software releases.

These incidents were not caused by AI-generated code. However, they highlight broader challenges involving code provenance, review accountability, and release integrity.

HAIRS (Human–AI Review System) is designed to address these challenges through traceable, source-bound human reviews, risk classification, and automated release validation.

Its goal is not to guarantee that software is free of vulnerabilities, but to make review coverage measurable, changes traceable, and release decisions accountable.

> **AI can accelerate development. Human accountability must remain verifiable.**

---

## The Three Layers of Evidence

1. **Component Attestation (`HUMAN_REVIEW.md`):** High-level attestation by a release engineer or team lead stating the scope and conditions for a release.
2. **File Review (`.hairs-review/<path>.review.json`):** Content-addressed decision over an entire source file based on normalized full-text SHA-256 (`sha256-nlf:`).
3. **Declaration Code Assurance (In-source `// Hairs-*`):** Fine-grained unit-level assessment and human review attached directly to code symbols (classes, methods, properties).

---

## Supply Chain Integrity Review

Software supply-chain integrity extends verification beyond application source code into the automation, build recipes, and dependencies that produce software releases.

HAIRS categorizes and audits four distinct supply chain asset classes:

| Asset Type | Target Assets | Vulnerability Vector (Historical Precedent) |
|---|---|---|
| **`BuildPipeline`** | `.github/workflows/*.yml`, `pack.ps1`, `build.sh`, `Dockerfile` | Malicious workflow actions, build-script injections (*SolarWinds, xz Utils*) |
| **`PackageManifest`** | `Directory.Packages.props`, `Directory.Build.props`, `*.csproj`, `packages.lock.json` | Dependency confusion, malicious version bumps, compromised maintainership (*event-stream*) |
| **`Configuration`** | `hairs.config.json`, `.gitattributes`, `nuget.config` | Silently disabling review gates, weakening verification rules |
| **`ThirdPartyCode`** | Vendored code (`vendor/*`, `third_party/*`), `Origin=ThirdParty` | Backdoored external modules, unvetted imported libraries |

### Content-Addressed Supply Chain Tracking

All supply chain assets are tracked with the same content-addressed normalized SHA-256 (`sha256-nlf:`) mechanism as source code:
- Stored as human review records under `.hairs-review/<path>.review.json`.
- Any edit to a CI/CD workflow, build script, package version, or tool configuration immediately alters the content hash, transitioning the asset from `Current` to `Stale`.
- Under the `hairs supply-chain --fail-on-stale` gate, unreviewed or modified supply chain files immediately block the release pipeline.

---

## Content Identity and Fingerprinting

### File Content Hash (`sha256-nlf:`)

File reviews compute SHA-256 over normalized content:
- Strips any leading Byte Order Mark (BOM).
- Normalizes all line endings (`\r\n`, `\r`) to standard Unix line feeds (`\n`).
- Retains all other whitespace, comments, and character tokens verbatim.

If a file is edited and later reverted to its exact previous normalized content, HAIRS recognizes that the review is current again without human intervention.

### Unit Structural Fingerprints

Code declarations use 6-character uppercase hexadecimal fingerprints computed by Roslyn AST parsing:
- Hashes meaningful language tokens, identifiers, operators, types, and bodies.
- Token-based: Re-formatting whitespace or adding benign line wraps does not alter the fingerprint.
- Directives (`#if`, `#else`, `#endif`) and conditional blocks are tracked.
- Acts as a tamper and staleness detector: any logic change in the declaration produces a different fingerprint, immediately invalidating any prior human signature on that unit.

---

## Assurance State Machine

Each code unit exists in exactly one state, evaluated in strict guard order:

| State | Condition | Blocks Release? |
|---|---|---|
| `EXEMPT` | Unit is exempt by scanner predicate or explicit declaration | No |
| `NEW` | Declaration carries no assurance annotation block | **Yes** |
| `STALE` | Human line explicitly records `STALE` | **Yes** |
| `AI_ASSESSED` | Machine block is present, but fingerprint is unsealed (`TBF`) | **Yes** |
| `HUMAN_PENDING` | Machine fingerprint is sealed, but human line is `PENDING` | **Yes** |
| `HUMAN_APPROVED_PENDING_FINGERPRINT` | Human alias signed, but not yet sealed by generator | **Yes** |
| `VERIFIED` | Sealed human fingerprint exactly matches current AST fingerprint | **No** |
| `STALE` | Sealed human fingerprint differs from current AST fingerprint | **Yes** |

---

## In-Source Annotation Syntax

```csharp
// Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=2; Fingerprint=4A3BFD
// Hairs-Falsified-If: an unauthorized user can read protected memory
// Hairs-Human:        EB; Fingerprint=4A3BFD
```

### Rubric Fields

- **Origin:** Provenance of the code (`AI`, `Original`, `Specification`, `Derived`, `Ported`, `ThirdParty`).
- **IP:** Intellectual property risk assessment (`None`, `Low`, `Medium`, `High`, `Unknown`).
- **Security:** Security impact of defects (`None`, `Low`, `Medium`, `High`, `Critical`).
- **Resources:** CPU and memory cost rating under adversary load (integer `0` to `10`).
- **CS / CL (Optional):** Conceptual Stability (`0` to `10`) and Change Likelihood (`0` to `10`).
- **Spec (Optional):** Architectural Decision Record reference (e.g. `ADR-0004`).
- **Falsified-If:** High and Critical security units **require** an observable falsification criterion sentence explaining under what condition the implementation would be proven flawed.

---

## Review Notes & Relocation

Review notes represent structured questions, concerns, todos, and observations attached to code:
- **Anchoring:** Notes anchor to exact code fragments rather than fragile line numbers.
- **Relocation States:**
  - `Anchored`: Text found at exact recorded offset.
  - `Moved`: Text moved elsewhere in the file uniquely.
  - `Ambiguous`: Text appears multiple times in new positions.
  - `Orphaned`: Text deleted or completely rewritten.
