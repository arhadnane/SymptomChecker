# Implementation Plan: Guided Diagnosis Assistant

**Branch**: `001-guided-diagnosis-ux` | **Date**: 2026-05-23 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/001-guided-diagnosis-ux/spec.md`

## Summary

Add a dual-mode (Patient guided / Professional advanced) shell on top of the existing WinForms Symptom Checker, with a refactored results panel that surfaces the localized `Treatments / Medications / CareAdvice` fields directly as cards, a reinforced non-dismissable disclaimer, and a stronger red-flag triage banner with a localized "When to call emergency services" dialog. The Professional mode reorganizes the current 1500+ line `MainForm` into collapsible sections without removing functionality. The Patient mode introduces a 4-step wizard that hides every technical control (model, threshold, top-K, weights, NB temperature, IA modules). All new strings ship in EN/FR/AR, all changes preserve the educational boundary mandated by the constitution, and `data/conditions.json` / schemas are left untouched.

## Technical Context

**Language/Version**: C# 12 on .NET 8 (WinForms desktop)
**Primary Dependencies**: System.Windows.Forms, NJsonSchema (already present, schema validation only); no new third-party packages
**Storage**: Local JSON under `data/` (`conditions.json`, `categories.json`, `synonyms.json`, `translations.json`, `settings.json`). Only `translations.json` and `settings.json` are mutated by this feature (both additive).
**Testing**: xUnit via `dotnet test` in `tests/SymptomChecker.Tests/`
**Target Platform**: Windows 10+ (x64), DPI 100-200 %, screen ≥ 800×600
**Project Type**: desktop-app (single executable, WinForms)
**Performance Goals**: Startup < 2 s (unchanged), Patient wizard step transition < 200 ms, single result card render < 50 ms, results list ≤ 100 entries rendered in < 300 ms
**Constraints**: Educational-only wording mandatory, offline-capable core flow, no PHI, graceful degradation if `translations.json` lacks new keys (English fallback)
**Scale/Scope**: ~98 conditions in dataset, single user, local files only; UI changes touch ~2000 lines across `UI/` and `Services/`

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Educational safety preserved: every new screen carries the non-dismissable disclaimer; results cards include the "Important — always consult a professional" inset (FR-005, FR-007). The term "diagnosis" is never used without "educational orientation" qualifier; red-flag banner says "consult immediately", never gives a clinical verdict (FR-010, FR-011).
- [x] Local-first / privacy preserved: no new outbound calls, no new persisted data beyond three additive `AppSettings` fields (`UiMode`, `CollapsedSections`, `PatientWizardLastStep`); Ollama and Wikidata stay optional and are hidden (not removed) in Patient mode.
- [x] Data governance captured: `data/conditions.json`, `data/categories.json`, `data/synonyms.json`, `schemas/*.schema.json` unchanged. `data/translations.json` receives additive keys with EN/FR/AR values and an English fallback path documented in `TranslationService.T(...)`. `AppSettings` change is additive (nullable members), so older `settings.json` files load without error.
- [x] Validation plan defined: new xUnit suites under `tests/SymptomChecker.Tests/` will cover `TriageService.DetectRedFlags`, `ConfidenceBadgeService.FromScore`, `ConditionResultViewModel.LocalizedSelection`, and `AppSettings` round-trip for the three new fields. Each merge runs `dotnet build --nologo` and `dotnet test`.
- [x] UX impact captured: new translation keys listed in `spec.md` Localization section, RTL handled in card and wizard containers (`RightToLeft` + `RightToLeftLayout` propagated), tab order defined per wizard step, contrast tested in both themes (AA target), Patient touch targets ≥ 32 px, no regression in current keyboard shortcuts (Alt+F, Alt+C, plus new Ctrl+M for mode toggle and Esc for back).

## Project Structure

### Documentation (this feature)

```text
specs/001-guided-diagnosis-ux/
├── plan.md                       # This file
├── spec.md                       # Feature spec
├── research.md                   # Phase 0 notes (existing-code audit, RTL recipe)
├── data-model.md                 # New view-models, settings keys, translation keys
├── quickstart.md                 # How to try the new UI locally
├── contracts/
│   ├── ui-mode-toggle.md         # Behavior contract: switching modes
│   ├── results-card-rendering.md # Behavior contract: localized cards + fallbacks
│   └── red-flags-triage.md       # Behavior contract: detection inputs/outputs
├── checklists/
│   └── requirements.md           # Spec quality checklist (already created)
└── tasks.md                      # Created by /speckit.tasks
```

### Source Code (repository root)

```text
Program.cs
Models/
├── Condition.cs                  # unchanged
├── ConditionMatch.cs             # unchanged
├── Symptom.cs                    # unchanged
└── UiMode.cs                     # NEW enum (Patient | Professional)
Services/
├── SymptomCheckerService.cs      # unchanged (logic intact)
├── CategoriesService.cs          # unchanged
├── TranslationService.cs         # unchanged API; new keys consumed
├── SettingsService.cs            # +3 nullable properties (UiMode, CollapsedSections, PatientWizardLastStep)
├── TriageService.cs              # extended: DetectRedFlags(vitals, symptoms, perc) returns IReadOnlyList<RedFlag>
├── ConfidenceBadgeService.cs     # NEW: score → Low/Moderate/High (per model)
└── (PatientFlowService deferred) # flow currently hosted inside PatientShell/MainForm state sync
UI/
├── MainForm.cs                   # shell host + layout creation
├── MainForm.Mode.cs              # mode restore/toggle + shell switching
├── MainForm.Patient.cs           # patient shell synchronization to existing state
├── MainForm.Professional.cs      # professional shell synchronization + persisted collapse state
├── MainForm.* (other partials)   # existing behavior preserved under shell hosting
├── PatientShell.cs               # NEW: 4-step wizard implemented as a single host control with internal step panels
├── ProfessionalShell.cs          # NEW: hosts existing controls grouped in CollapsibleSection panels
├── Controls/
│   ├── ConditionResultCard.cs    # NEW: localized card render (treatments / meds / advice / disclaimer)
│   ├── CollapsibleSection.cs     # NEW: header + toggle + persisted state
│   ├── RedFlagBanner.cs          # NEW: non-dismissable rouge bar
│   └── ModeSelector.cs           # NEW: first-run mode chooser
data/
├── translations.json             # additive keys (EN/FR/AR)
└── settings.json                 # additive fields auto-saved
schemas/                          # unchanged
docs/
└── uml-diagrams.md               # add wizard sequence + ProfessionalShell component diagram
tests/
└── SymptomChecker.Tests/
    ├── RedFlagDetectionTests.cs              # extend / align with typed red-flag contract
    ├── ConfidenceBadgeServiceTests.cs        # NEW
    ├── PatientTranslationAuditTests.cs       # NEW (patient-facing vocabulary guardrail)
    ├── TranslationServiceTests.cs            # extended with wizard/pro section keys
    └── SettingsServiceTests.cs               # extend (new fields round-trip)
```

**Structure Decision**: We keep the existing `Models/`, `Services/`, `UI/`, `data/`, `schemas/`, `tests/` layout (mandated by `.specify/memory/constitution.md` Operational Constraints) and add new files only. The current `MainForm` partials remain functional code — `ProfessionalShell` reorganises them into named collapsible sections while preserving existing controls. The Patient flow currently lives in a single `PatientShell` host control with four internal steps; it can still be split later if the wizard grows.

## Phase 0 — Research

Captured in `research.md`. Highlights:

1. **Re-using existing logic without duplication**: the existing `SymptomCheckerService`, `CategoriesService`, `TriageService`, `TranslationService`, `SettingsService` are sufficient; the new shells call them. No matching-logic rewrite.
2. **Choosing a card UI in WinForms**: a `FlowLayoutPanel` of owner-drawn `ConditionResultCard` user controls beats the current `ListBox` for legibility (the existing `_resultsList` becomes the Pro fallback view; cards become the default in both modes).
3. **RTL & DPI recipe**: setting `RightToLeft = Yes` and `RightToLeftLayout = true` on every new container, scaling via `AutoScaleMode.Dpi` already used by `MainForm`, and using `ScaleX/Y` helpers already present.
4. **Confidence badge buckets**: derived from `match.Score` per model — Jaccard/Cosine: Low < 0.20, Moderate < 0.50, High ≥ 0.50 ; Naive Bayes (post-temperature): Low < 0.15, Moderate < 0.35, High ≥ 0.35. Bands are configurable later but hardcoded with constants now to keep scope bounded.
5. **Collapsible section state**: serialize as `Dictionary<string,bool>` in `AppSettings.CollapsedSections`; default to `false` (expanded) for missing keys.

## Phase 1 — Design Artifacts

Captured separately:

- `data-model.md` — enum, view-models, translation key list, new settings fields, JSON shape of `AppSettings.CollapsedSections`.
- `contracts/ui-mode-toggle.md` — preconditions, postconditions, and persisted state for switching modes.
- `contracts/results-card-rendering.md` — input (`ConditionMatch + Condition + Language`), output (card sections + disclaimer + fallback messages).
- `contracts/red-flags-triage.md` — input thresholds, ordered detection rules, output `RedFlag` list, presentation rules.
- `quickstart.md` — how a contributor runs the new UI in 30 seconds.

## Phase 2 — Tasks

Generated by `/speckit.tasks` into `tasks.md`. Each story (US1, US2, US3, US4, US5) gets its own task block so it can be implemented and shipped independently.

## Validation Strategy

- `dotnet build --nologo` — required gate.
- `dotnet test --nologo` — required gate; new tests must pass.
- Automated startup smoke completed on 2026-05-23: launching `bin/Debug/net8.0-windows/SymptomChecker.exe` created a fresh startup log file under `bin/Debug/net8.0-windows/logs`. This confirms process boot but does not replace the pending interactive smoke checklist.
- Contrast spot-check completed on 2026-05-23: measured ratios for the shipped Patient shell, mode selector, collapsible headers, red-flag banner, and result-card surfaces all meet WCAG AA after tightening the light-mode `ConditionResultCard` blue/amber/neutral palette (`Treatment` 5.60, `Advice` 5.38, `Badge Moderate` 5.52, `Badge Low` 5.05).
- Manual smoke: launch app → mode chooser → Patient wizard → results cards visible → red flag banner triggered by SpO₂ = 88 + Shortness of Breath → switch to Pro → all existing controls reachable.
- Localization smoke: switch language to `fr` then `ar`, verify wizard, cards, disclaimer, red flag banner translate, and that `ar` flips layout.
- Dataset audit: validate that every rendered result card shows educational care content or the documented fallback message, never an empty medication section.
- Patient-surface audit: verify that Patient mode strings do not expose technical model/tuning terms in EN/FR/AR.
- Performance smoke: time wizard transitions and result card list rendering with a stopwatch (must meet the goals in Technical Context).

## Complexity Tracking

> No constitution violations. This feature is purely additive on top of existing services; no exception entries required.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| (none)    | (n/a)      | (n/a)                                |
