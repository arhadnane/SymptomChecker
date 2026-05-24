# Quickstart — Guided Diagnosis Assistant

This guide takes a contributor from a fresh clone to seeing the new dual-mode UI in under a minute.

## Prerequisites

- .NET 8 SDK on Windows.
- VS Code or Visual Studio.

## Run

```powershell
dotnet restore
dotnet build --nologo
dotnet run --project SymptomChecker.csproj
```

## First-run path

1. The new **Mode Selector** opens with two large cards: "Patient (guided)" and "Professional (advanced)". The educational disclaimer is visible.
2. Pick **Patient**.
3. Step 1 — choose a body area / category (e.g. "Respiratory").
4. Step 2 — tick the most common symptoms (e.g. Cough, Sore Throat, Fever). "Next" is disabled until at least one is selected.
5. Step 3 — optional vitals: leave defaults or enter SpO₂ = 88, HR = 122 to trigger a red flag.
6. Step 4 — see **result cards** with Home care / OTC examples / When to seek care, plus the red "Important" inset.

## Switch to Professional

- Press `Ctrl+M` or click "Switch mode".
- All existing controls (model selector, threshold, top-K, weights, NB temp, Centor/McIsaac, PERC, Ollama / Image / Blood tabs) are grouped in collapsible sections.
- Collapse "AI modules" — relaunch the app — the section stays collapsed.

## Localization smoke

- Top right: switch language to `Français` then `العربية`.
- Verify all new strings (mode chooser, wizard, cards, red flag banner) translate.
- In Arabic, layout flips to RTL.

## Run the tests

```powershell
dotnet test --nologo
```

New suites:

- `TriageServiceTests` (red flags)
- `ConfidenceBadgeServiceTests`
- `PatientFlowServiceTests`
- `ConditionResultViewModelTests`
- `SettingsServiceTests` (new fields round-trip)
