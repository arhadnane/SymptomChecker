# Phase 0 Research — Guided Diagnosis Assistant

## 1. Existing code audit (read-only)

- `UI/MainForm.cs` (≈1594 lines) currently builds the full toolbar inside a single `FlowLayoutPanel` (`topControls`). All technical controls (model selector, threshold, min match, top-K, category weights, NB temperature, language, dark mode, Help, Logs, Sync, Missing Translations) live here. That density is the root UX issue and the target of Story US2.
- Existing partials (`MainForm.Results.cs`, `MainForm.Dialogs.cs`, `MainForm.Ollama.cs`, `MainForm.ImageAnalysis.cs`, `MainForm.BloodAnalysis.cs`, `MainForm.Theme.cs`, `MainForm.Session.cs`, `MainForm.DecisionRules.cs`, `MainForm.Export.cs`) keep behaviour intact; we re-host them in `ProfessionalShell` sections via `CollapsibleSection`.
- `Services/SymptomCheckerService.cs` already exposes `TryGetCondition`, score computation, model dispatch. No change.
- `Services/TriageService.cs` is the right home for `DetectRedFlags(vitals, symptoms, percPositive)`. Currently triage logic is duplicated inline in `MainForm.DecisionRules.cs`; we extract it to the service.
- `Services/SettingsService.cs` already persists ≈30 fields in `data/settings.json`. Adding 3 nullable members is safe and tested by the existing round-trip pattern.

## 2. Result card rendering options

| Option | Pros | Cons | Decision |
|--------|------|------|----------|
| Keep `ListBox` + owner draw | No new control, low risk | Hard to host multi-section text + buttons inside a row | Reject — does not satisfy FR-007 |
| Replace by `FlowLayoutPanel` of `UserControl` cards | Multi-section layout, accessibility per control, easy RTL, per-card tab order | More controls on screen | **Adopt** — used in both Patient and Pro modes |
| `DataGridView` | Built-in scroll/sort | Heavy, hard to style, poor for multi-line + sections | Reject |

## 3. RTL and DPI recipe

- Containers (`PatientShell`, `ProfessionalShell`, `ConditionResultCard`, `RedFlagBanner`, `ModeSelector`) call `this.RightToLeft = lang == "ar" ? RightToLeft.Yes : RightToLeft.No;` plus `this.RightToLeftLayout = (lang == "ar");`. Children inherit by default.
- DPI: rely on `AutoScaleMode.Dpi` already set on `MainForm`. New controls expose dimensions through the existing `ScaleX/Y` helpers (`MainForm.cs`) — extract them to `UI/Controls/UiScale.cs` to share with the new shells without circular deps.
- Touch targets ≥ 32 px: enforce `MinimumSize = new Size(_, ScaleY(32))` on Patient buttons and option cards.

## 4. Confidence badge buckets

Decisions per model, based on observed score ranges in `tests/SymptomChecker.Tests/`:

| Model | Low | Moderate | High |
|-------|-----|----------|------|
| Jaccard | `score < 0.20` | `0.20 ≤ score < 0.50` | `score ≥ 0.50` |
| Cosine | `score < 0.20` | `0.20 ≤ score < 0.50` | `score ≥ 0.50` |
| NaiveBayes (post temp) | `score < 0.15` | `0.15 ≤ score < 0.35` | `score ≥ 0.35` |

Localized as `Confidence_Low / Moderate / High` keys (EN: "Low confidence", FR: "Confiance faible", AR: "ثقة منخفضة"). All boundaries are hard-coded constants in `ConfidenceBadgeService`; no settings exposure in this iteration.

## 5. Collapsible section state

- Serialized as `Dictionary<string, bool>` in `AppSettings.CollapsedSections`.
- Default state for missing keys: `false` (section is expanded).
- Section keys (stable, used in settings + telemetry-free logs):
  - `pro.symptoms` (left panel)
  - `pro.vitals`
  - `pro.rules` (Centor/PERC group)
  - `pro.model` (model selector + tuning)
  - `pro.results`
  - `pro.ai` (Ollama/Image/Blood tabs)

## 6. Patient flow design constants

- 4 steps (FR-003). Step number persisted as `AppSettings.PatientWizardLastStep` (1..4, 0 = not started). On launch with Patient mode, app jumps to last step if results were not yet shown; once Step 4 is reached, persisted as 0 so next launch starts fresh.
- Default common symptoms per category are derived at runtime from `CategoriesService` + `SynonymService`: take the first 8 symptoms whose category keyword matches and surface them as large checkboxes; "Show all symptoms in this area" expands the list.
- Validation rule: Step 2 requires ≥ 1 symptom selected before "Next" is enabled.

## 7. Test data scenarios

- Flu cluster: `Fever + Cough + Fatigue` → expect "Flu" in top 3, confidence Moderate or High, card shows medication list with Acetaminophen.
- Migraine: `Headache + Nausea + Sensitivity to Light` → expect "Migraine" top, medication shows NSAIDs.
- Red flag SpO₂: vitals `SpO₂=88, RR=22 + Shortness of Breath` → red flag banner, results not blocked.
- Empty input: no symptoms in Step 2 → "Next" disabled, helper text shown.
- Localization: switch to `fr`/`ar`, replay scenarios, assert all UI strings localized and disclaimer present.

## 8. Risks

| Risk | Mitigation |
|------|------------|
| Existing `MainForm` rewrite breaks current users | Keep `MainForm` as host; mount `PatientShell` or `ProfessionalShell` as a child. Original behaviour preserved when `UiMode = Professional`. |
| Translation coverage incomplete | `TranslationService.SaveMissingReport` already logs misses; build pipeline can later assert zero missing keys for new prefixes. |
| Layout regressions on small screens | New shells use `TableLayoutPanel + ScrollableControl`. Add manual smoke at 800×600 to acceptance. |
| Performance regression on cards | Cap rendered cards to TopK (already a setting); virtualize via paging if list > 50 (post-iteration). |
