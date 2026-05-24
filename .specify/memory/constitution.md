<!--
Sync Impact Report
Version change: template -> 1.0.0
Modified principles:
- Template Principle 1 -> I. Educational Safety First
- Template Principle 2 -> II. Local-First and Privacy-Minimal Operation
- Template Principle 3 -> III. Schema-Governed Data Evolution
- Template Principle 4 -> IV. Test-Backed Clinical Logic Changes
- Template Principle 5 -> V. Accessible and Localized Desktop Experience
Added sections:
- Operational Constraints
- Development Workflow & Quality Gates
Removed sections:
- None
Templates requiring updates:
- ✅ .specify/templates/plan-template.md
- ✅ .specify/templates/spec-template.md
- ✅ .specify/templates/tasks-template.md
- ✅ README.md (reviewed, no change required)
- ✅ .specify/templates/commands/*.md not present in this repository
Follow-up TODOs:
- None
-->

# Symptom Checker Constitution

## Core Principles

### I. Educational Safety First

The product MUST remain an educational symptom-matching tool, never a clinical
diagnostic or treatment system. User-facing copy, exports, AI-assisted responses,
and feature names MUST preserve an educational disclaimer, avoid claims of
certainty, and avoid presenting matches as diagnoses. Any change that affects
condition ranking, medication guidance, urgency language, or AI-generated
medical content MUST document the safety impact and show how disclaimer coverage
is preserved in supported languages. Rationale: the project's scope,
compliance position, and user trust all depend on a hard boundary between
education and clinical advice.

### II. Local-First and Privacy-Minimal Operation

Core symptom checking MUST remain fully usable from local files without network
access. The application MUST NOT collect, persist, or transmit patient
identifiers or other PHI, and any outbound integration MUST be optional,
user-initiated, and limited to non-sensitive payloads. Features that depend on
Wikidata, local Ollama, or future services MUST fail gracefully without
breaking the primary workflow. Rationale: the current desktop architecture is
intentionally offline-capable and privacy-minimal.

### III. Schema-Governed Data Evolution

Changes to conditions, categories, translations, synonyms, settings, or export
structures MUST preserve deterministic loading and documented fallback behavior.
Any structural change to a JSON-backed artifact MUST ship with a schema update
or a written compatibility exception, plus migration or fallback notes when the
shape changes. Additive merges are the default; breaking data changes require
an explicit plan and versioned rollout. Rationale: the application depends on
small local datasets, and silent format drift would damage matching accuracy,
localization, and user confidence.

### IV. Test-Backed Clinical Logic Changes

Any change to matching algorithms, triage rules, decision support logic, AI
response parsing, scoring calibration, or safety messaging MUST include focused
automated regression coverage in the xUnit test suite before merge. Bug fixes
in those areas MUST reproduce the defect with a failing or characterizing test
when feasible, and plans MUST identify the narrowest validation command that
proves the change. Rationale: clinical-adjacent logic regresses subtly and must
be defended with executable checks rather than manual inspection.

### V. Accessible and Localized Desktop Experience

User-visible changes MUST preserve keyboard accessibility, clear WinForms
desktop behavior, and the English/French/Arabic localization model, including
RTL layout where applicable. New controls, dialogs, banners, exports, and AI
surfaces MUST define translation impacts, maintain a usable English fallback,
and keep accessibility metadata for primary interactions. Performance-sensitive
flows MUST continue to respect the documented expectations for startup,
filtering, and result computation. Rationale: the app's value depends on a
consistent local desktop experience across supported languages.

## Operational Constraints

- The baseline stack is C# on .NET 8 WinForms with local JSON data under
  `data/`, schemas under `schemas/`, business logic in `Services/`, UI code in
  `UI/`, and automated tests in `tests/SymptomChecker.Tests/`.
- New third-party dependencies MUST be justified against the existing .NET
  platform and current packages, and MUST not displace simple built-in
  solutions without a measurable benefit.
- Core workflows MUST degrade gracefully when optional files are missing,
  except for required datasets whose absence already blocks startup by design.
- When a file currently lacks a schema, the next structural change to that file
  MUST add one or explicitly document why the exception remains temporary.

## Development Workflow & Quality Gates

- Non-trivial features and behavioral changes MUST record, in specification and
  plan artifacts, their impact on educational safety, local data or schemas,
  localization/accessibility, and automated validation.
- Implementation plans MUST pass a constitution check covering: educational
  wording, local-first/privacy boundaries, schema and migration impact,
  required xUnit coverage, and localization/accessibility implications.
- Task lists MUST include the concrete validation work needed for affected
  services, parsers, translations, disclaimers, schemas, and user-facing flows;
  tasks cannot treat these as optional polish when the change touches them.
- Before merge, contributors MUST run the narrowest relevant automated tests
  and `dotnet build --nologo`. When JSON structures or translations change,
  contributors MUST also validate the affected load/fallback path.
- Any exception to these gates MUST be documented in the plan's complexity or
  risk notes and explicitly reviewed before implementation proceeds.

## Governance

This constitution overrides ad hoc workflow preferences for this repository.
Amendments MUST be made in the same change set as any required template or
guidance updates, and each amendment MUST include a Sync Impact Report at the
top of this file.

Versioning policy for this constitution follows semantic versioning:

- MAJOR for removing a principle or redefining governance in a
  backward-incompatible way.
- MINOR for adding a principle, a mandatory section, or materially expanding
  governance.
- PATCH for clarifications, wording improvements, or non-semantic refinements.

Compliance review is required at specification, planning, implementation, and
review time. Plans and tasks that cannot satisfy the active principles MUST
document the exception and the simpler compliant alternative that was rejected.

**Version**: 1.0.0 | **Ratified**: 2026-05-22 | **Last Amended**: 2026-05-22
