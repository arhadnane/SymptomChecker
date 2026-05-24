---

description: "Task list for Guided Diagnosis Assistant"
---

# Tasks: Guided Diagnosis Assistant (Patient + Professional Modes)

**Input**: `specs/001-guided-diagnosis-ux/`
**Prerequisites**: spec.md, plan.md, research.md, data-model.md, contracts/

**Tests**: Mandatory for `TriageService`, `ConfidenceBadgeService`, view-model fallbacks, settings round-trip (per constitution Principle IV).

> Re-baselined on 2026-05-23 against the current repository state. The
> following items are already present in code and/or tests: T001, T003, T005,
> T006, T011, T012, T032, T050, T051, T053. Remaining work focuses on shell
> hosting, mode switching, patient flow, disclaimer coverage, missing
> translations, accessibility, and completion audits.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallelizable (different files, no dependencies)
- **[Story]**: which user story (US1..US5) the task supports

## Path Conventions

- App: `Program.cs`, `Models/`, `Services/`, `UI/`
- Local data: `data/`, `schemas/`
- Tests: `tests/SymptomChecker.Tests/`
- Feature artifacts: `specs/001-guided-diagnosis-ux/`

---

## Phase A — Foundation (shared across stories)

- [x] **T001** [P] Add `Models/UiMode.cs` with the enum from `data-model.md`.
- [ ] **T002** [P] Add `UI/Controls/UiScale.cs` with `ScaleX(int)`, `ScaleY(int)`, `MinTouch(int = 32)` helpers (extracted from `MainForm.cs`).
- [x] **T003** Extend `Services/SettingsService.cs` — add nullable `UiMode`, `PatientWizardLastStep`, `CollapsedSections`. Keep round-trip backward compatible.
- [ ] **T004** [P] Add `data/translations.json` keys listed in `data-model.md` (EN/FR/AR). Validate with `schemas/translations.schema.json`.
- [x] **T005** [P] Add `Services/ConfidenceBadgeService.cs` implementing the bucket logic per model.
- [x] **T006** Extend `Services/TriageService.cs` with `DetectRedFlags(VitalsSnapshot, IReadOnlyCollection<string>, bool)` returning ordered `RedFlag` list (move duplicated logic out of `MainForm.DecisionRules.cs`).
- [ ] **T007** [P] Add `Services/PatientFlowService.cs` with `PatientWizardStep` enum, `CanProceed`, `CommonSymptomsForCategory(string, int = 8)`.

## Phase B — UX shells & controls

- [x] **T010** [P] [US1/US2] Add `UI/Controls/CollapsibleSection.cs` (header + chevron + persists state via `SettingsService`).
- [x] **T011** [P] [US3/US4] Add `UI/Controls/RedFlagBanner.cs` — non-dismissable, localized, with "When to call" dialog.
- [x] **T012** [P] [US3] Add `UI/Controls/ConditionResultCard.cs` (header, confidence badge, 3 sections + "Important" red inset, RTL aware).
- [x] **T013** [US1/US2] Add `UI/Controls/ModeSelector.cs` — overlay with two cards (Patient / Professional) shown when `Settings.UiMode == null`.
- [x] **T014** [US1/US2] Refactor `UI/MainForm.cs` into a shell host with a single mounted content region and a persistent global disclaimer/footer that survives shell remounts.
- [x] **T015** [US1/US2] Mount `UI/Controls/ModeSelector.cs` on first launch and after reset-settings, persist the chosen `UiMode`, and restore it on startup.
- [x] **T016** [US1/US2] Preserve symptoms and vitals across mode switches, restore focus to the primary control of the mounted shell, and wire `Ctrl+M`.
- [x] **T017** [US4] Guarantee disclaimer coverage on the mode selector, Patient shell host/footer, Patient steps 1-4, and Professional shell footer.

## Phase C — Patient shell (US1, US4, US5)

- [x] **T020** [US1] Add `UI/PatientShell.cs` host container + step navigation (`Next`, `Back`, `Esc`).
- [x] **T021** [US1] Add `UI/PatientShell.Step1Category.cs` — delivered inside `UI/PatientShell.cs` as large category cards from `CategoriesService.GetAllCategories()`.
- [x] **T022** [US1] Add `UI/PatientShell.Step2Symptoms.cs` — delivered inside `UI/PatientShell.cs` as the category-bound symptom picker.
- [x] **T023** [US1/US4] Add `UI/PatientShell.Step3Vitals.cs` — delivered inside `UI/PatientShell.cs` with optional vitals and plain-language red-flag prompts.
- [x] **T024** [US1/US3/US4] Add `UI/PatientShell.Step4Results.cs` — delivered inside `UI/PatientShell.cs` with `ConditionResultCard` + `RedFlagBanner` + permanent disclaimer.
- [ ] **T025** [US1/US5] Ensure all Patient steps render correctly at 800×600 and 1920×1200, dark and light themes, EN/FR/AR.

## Phase D — Professional shell (US2, US5)

- [x] **T030** [US2] Add `UI/ProfessionalShell.cs` hosting existing partials inside `CollapsibleSection` containers (Symptoms, Vitals, Rules, Model, Results, AI).
- [ ] **T031** [US2] Re-parent the current `topControls` `FlowLayoutPanel` into 4 logical groups (Model, Tuning, Languages/Theme, Actions) instead of a single dense bar.
- [x] **T032** [US2/US3] Replace the legacy `_resultsList` rendering with the new `ConditionResultCard` flow; keep `_resultsList` as a hidden fallback used only by export (existing CSV/MD/HTML pipeline unchanged).
- [x] **T033** [US2/US5] Persist `CollapsedSections` per section key (`pro.symptoms`, `pro.vitals`, `pro.rules`, `pro.model`, `pro.results`, `pro.ai`).
- [x] **T034** [US2] Ctrl+M shortcut wired in `MainForm.cs` to toggle `UiMode` and reload the shell.

## Phase E — Disclaimer & red-flag enforcement (US4)

- [ ] **T040** [US4] Update all export templates (`MainForm.Export.cs`) so the disclaimer is the first line/header of CSV / Markdown / HTML output (FR-012).
- [ ] **T041** [US4] Ensure every dialog opened from results (details, help, missing translations) shows the disclaimer in a visible footer.
- [x] **T042** [US4] Wire `RedFlagBanner` into both shells using `TriageService.DetectRedFlags`.

## Phase F — Tests (mandatory per constitution)

- [x] **T050** [P] `tests/SymptomChecker.Tests/ConfidenceBadgeServiceTests.cs` — 9 tests covering boundaries for all three models.
- [x] **T051** [P] `tests/SymptomChecker.Tests/RedFlagDetectionTests.cs` — per-rule unit coverage plus combined-ordering integration coverage for typed red flags.
- [ ] **T052** [P] `tests/SymptomChecker.Tests/ConditionResultCardTests.cs` — localized fallback (EN missing FR present, EN missing FR missing, AR present, empty medications → "no OTC listed" message).
- [x] **T053** [P] `tests/SymptomChecker.Tests/SettingsServiceTests.cs` — round-trip of new fields; loads old `settings.json` without crash.
- [ ] **T054** [P] `tests/SymptomChecker.Tests/PatientFlowServiceTests.cs` — `CanProceed` validation, `CommonSymptomsForCategory` caps at 8, deterministic order.

## Phase G — Validation & docs

- [x] **T060** Run `dotnet build --nologo`; must succeed.
- [x] **T061** Run `dotnet test --nologo`; must succeed.
- [ ] **T062** Manual smoke per `quickstart.md` (mode chooser, wizard, red flag, switch to Pro, FR + AR).
- [ ] **T063** [P] Update `README.md` "Features at a glance" with the new modes.
- [x] **T064** [P] Update `docs/uml-diagrams.md` with the new wizard sequence + `ProfessionalShell` component diagram.
- [ ] **T065** [P] Audit `data/conditions.json` card rendering so every condition shows educational care content or the documented fallback message (SC-002).
- [x] **T066** [P] Review/grep Patient-mode strings in EN/FR/AR to ensure forbidden technical terms are absent from the wizard surfaces (SC-005).
- [x] **T067** [P] Verify AA contrast for cards, banner, and disclaimer in light and dark themes; record the result in feature docs (SC-007).

> Validation note (2026-05-23): an automated startup smoke was executed by launching `SymptomChecker.exe`, confirming a fresh startup log file under `bin/Debug/net8.0-windows/logs`. Full interactive smoke across mode switching and FR/AR remains pending in T062.
>
> Contrast note (2026-05-23): measured WCAG AA spot-checks now pass for the shipped Patient shell, mode selector, collapsible headers, red-flag banner, and result-card surfaces. The light-mode `ConditionResultCard` palette was tightened so the treatment/advice headings and `Moderate/Low` confidence badges clear the 4.5:1 threshold.

---

## Dependencies summary

- Phase A blocks Phases B–F.
- T010..T013 (controls) can be developed in parallel after Phase A.
- Patient shell (Phase C) only requires Phase A + T011 + T012 + T013.
- Professional shell (Phase D) only requires Phase A + T010 + T012.
- Tests (Phase F) can be written in parallel as soon as the corresponding service / view-model lands.
- Validation (Phase G) runs at the end of each story slice.

## Independent shipping units

| MVP slice | User Story | Tasks |
| --------- | ---------- | ----- |
| MVP 1 — Result cards + disclaimer | US3, US4 | T001, T003, T004, T005, T006, T011, T012, T032, T040, T041, T042, T050, T051, T052, T053, T060, T061 |
| MVP 2 — Patient wizard | US1 | T007, T013, T020–T025, T054 |
| MVP 3 — Pro reorganisation | US2, US5 | T010, T030, T031, T033, T034 |
