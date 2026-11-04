# HAIRS Workflow and CI Integration

This guide describes how to use HAIRS for day-to-day code review, declaration assurance, and CI/CD automated gates.

---

## 1. File Review Workflow

The file review engine tracks whether an entire file was reviewed by a human at a specific content hash.

### Recording a Review

1. Open and inspect the file.
2. Record the review using the `Hairs` API or CLI, storing the decision under `.hairs-review/<path>.review.json`.
3. Commit the review JSON along with the code changes.
4. When file content changes, HAIRS marks the review as `Stale`. Reverting to the exact previous content restores freshness automatically.

### Running File Review Audits in CI

From the repository root:

```powershell
# Compute repository coverage report (Markdown and JSON)
hairs coverage --root . --markdown coverage.md --json coverage.json

# Fail if any changed files have stale reviews
hairs check --root . --changed changed-files.txt --fail-on-stale
```

Options:
- `--root <path>`: The base directory of the repository or component.
- `--changed <path>`: List of changed files to verify (e.g. from `git diff --name-only`).
- `--fail-on-stale`: Exits with code 1 if any checked file has a stale or question review.
- `--minimum <0-100>`: Require minimum percentage of reviewed files.

---

## 2. Declaration Code Assurance Workflow

Declaration assurance provides unit-level confidence tracking directly inside source code.

### Step 1: Configuration Opt-In

Create a `hairs.config.json` at the component or repository root:

```json
{
  "schema": 1,
  "component": "CoreEngine",
  "projects": [ "src/CoreEngine/CoreEngine.csproj" ],
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

### Step 2: List Unannotated Units

Discover all C# units that lack assurance blocks:

```powershell
hairs assurance list --root . --json unannotated.json
```

### Step 3: Insert AI Assessments

Insert initial assessments using an assessments JSON file:

```powershell
hairs assurance insert --root . --assessments unannotated.json
```

This inserts blocks in the source with `// Hairs-Human: PENDING`:

```csharp
/// <summary>Calculates tax rate.</summary>
// Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
// Hairs-Human:        PENDING
public decimal CalculateTax(decimal amount) => amount * 0.2m;
```

### Step 4: Generate Artifacts & Fingerprints

Fill in the structural fingerprints and generate the summary reports:

```powershell
hairs assurance generate --root .
```

This updates:
1. File headers (with summary counts and SPDX banner).
2. `CODE-ASSURANCE.md` (detailed markdown assurance report).
3. `HUMAN_REVIEW.md` (human review status record).
4. `hairs.manifest.json` (machine-readable unit inventory with fingerprints).

### Step 5: Human Review & Signature

A human reviewer inspects the unit and signs the human line:

```csharp
// Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=8A3C19
// Hairs-Human:        EB
public decimal CalculateTax(decimal amount) => amount * 0.2m;
```

When `hairs assurance generate` runs next, it binds the signature to the current fingerprint:

```csharp
// Hairs-Human:        EB; Fingerprint=8A3C19
```

If the method body is later modified, the fingerprint changes, and HAIRS automatically flags the unit as `STALE`.

### Step 6: Pruning Obsolete Annotations

If the rubric or configuration changes (e.g., constants become exempt as named values):

```powershell
hairs assurance prune --root .
```

`prune` removes obsolete blocks and below-High criteria whose human line is `PENDING`, leaving human-signed lines untouched.

---

## 3. CI/CD Release Gates

HAIRS integrates into continuous integration via deterministic exit codes and GitHub Actions workflow commands.

### Consistency Check Gate

Verifies that all annotations parse cleanly, fingerprints match actual ASTs, and generated files are up to date:

```powershell
hairs assurance check --root .
```

- Returns `0` if all files and manifests are clean and consistent.
- Returns `1` if violations are found (e.g. unannotated code units, modified generated files, stale fingerprints).
- Emits GitHub Actions workflow commands (e.g. `::error file=...::`).

### Strict Release Gate

Verifies that **every** relevant code unit has been reviewed and signed by a human:

```powershell
hairs assurance check --root . --release
```

- Enforces rule `J11`: Any unit with state other than `VERIFIED` or `EXEMPT` blocks the release.

---

## 4. Rule Index Summary

| Rule | Area | Description |
|---|---|---|
| **J1** | Coverage | Relevant units without assurance blocks. |
| **J2** | Grammar | Syntax errors in annotation blocks or unsupported claim prose. |
| **J3** | Fingerprints | Units with placeholder `TBF` or mismatched AST fingerprints. |
| **J4** | Human Decision | Automation attempting to forge human aliases or approvals. |
| **J5** | Headers & Banners | Stale or modified generated file headers or markdown reports. |
| **J6** | Directives | Forbidden directives (e.g. preprocessor directives in covered files). |
| **J7** | Manifest | Discrepancies between `hairs.manifest.json` and actual disk files. |
| **J9** | Claims | Unsupported review or approval claims in criterion prose. |
| **J10** | Criteria | Missing `// Hairs-Falsified-If:` on High/Critical security units. |
| **J11** | Release | Units with pending or stale human reviews under `--release`. |

---

## 5. NuGet Package Publishing (GitHub Actions)

The workflow [`.github/workflows/hairs-nuget.yml`](../.github/workflows/hairs-nuget.yml) automates packaging and releasing `Hairs` and `Hairs.Cli`.

### Tag Scheme: `hairs-0.1.0-preview.x`

When pushing a tag such as `hairs-0.1.0-preview.1`:
1. The workflow triggers automatically.
2. It extracts the package version `0.1.0-preview.1`.
3. It builds and executes the complete unit test suite (376 tests).
4. It packs `Hairs` and `Hairs.Cli` packages.
5. It archives the `.nupkg` artifacts and publishes them to NuGet.org (using `NUGET_API_KEY`) and GitHub Packages (using `GITHUB_TOKEN`).

### Manual Dispatch (`workflow_dispatch`)

In the GitHub Actions tab, select **HAIRS NuGet Packages** and provide:
- `preview_number`: e.g. `2` (creates version `0.1.0-preview.2`)
- `version_override`: optional custom SemVer string
- `dry_run`: `true` to test building and packing without uploading to the feed.
