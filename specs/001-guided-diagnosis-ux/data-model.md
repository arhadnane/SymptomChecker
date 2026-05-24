# Data Model — Guided Diagnosis Assistant

## Enums

```csharp
namespace SymptomCheckerApp.Models
{
    public enum UiMode
    {
        Patient = 0,
        Professional = 1
    }
}
```

## UI projection shapes (in-memory only, not persisted)

```csharp
public enum Confidence { Low, Moderate, High }

// Optional projection shape for card rendering. In this iteration it may live
// as an internal mapper in the UI layer rather than as a public shared type.
public sealed record ConditionResultViewModel(
    string DisplayName,
    string CanonicalName,
    Confidence Confidence,
    int MatchCount,
    IReadOnlyList<string> Treatments,
    IReadOnlyList<string> Medications,
    string CareAdvice,
    bool MedicationsMissing,
    bool RtlLayout);

public sealed record RedFlag(
    string Code,            // e.g. "RF_Hypoxia"
    int Severity,           // 1..5
    string MessageKey);     // localized by the UI layer via TranslationService
```

## AppSettings additions (additive, all nullable / defaulted)

```csharp
public UiMode? UiMode { get; set; }                    // null = "ask on next launch"
public int? PatientWizardLastStep { get; set; }        // 0..4
public Dictionary<string, bool>? CollapsedSections { get; set; } // per-section toggle in Pro mode
```

Behavior:

- A missing value is treated as the default (null → ask the user on first run; missing key in `CollapsedSections` → expanded).
- Older `settings.json` files without these fields load without error (JSON deserialization ignores absent keys).
- The mutated artifacts in `data/` for this feature are `translations.json`
    (additive keys only) and `settings.json` (additive UI preferences only).

## Translation keys (additive, all in EN/FR/AR)

Mode chooser:

- `Mode_Title`, `Mode_Subtitle`, `Mode_Patient`, `Mode_Patient_Description`, `Mode_Professional`, `Mode_Professional_Description`, `Mode_Switch_Hint`.

Patient wizard:

- `Patient_Step1_Title`, `Patient_Step1_Subtitle`
- `Patient_Step2_Title`, `Patient_Step2_Subtitle`, `Patient_Step2_ShowAll`, `Patient_Step2_RequireOne`
- `Patient_Step3_Title`, `Patient_Step3_Subtitle`, `Patient_Step3_Optional`
- `Patient_Step4_Title`, `Patient_Step4_NoResults`
- `Patient_Next`, `Patient_Back`, `Patient_StartOver`

Result card:

- `Card_HomeCare`, `Card_OTC`, `Card_WhenToSeek`, `Card_NoMedications`, `Card_Important`, `Card_AlwaysConsult`, `Card_MatchCount`

Confidence badges:

- `Confidence_Low`, `Confidence_Moderate`, `Confidence_High`

Red-flag banner:

- `RedFlag_Banner_Title`, `RedFlag_SeekCareNow`, `RedFlag_WhenToCall_Button`, `RedFlag_WhenToCall_Body`
- Specific triggers: `RedFlag_Spo2_Low`, `RedFlag_Sbp_Low`, `RedFlag_Sbp_High`, `RedFlag_Dbp_High`, `RedFlag_Hr_High`, `RedFlag_Rr_High`, `RedFlag_Temp_High`, `RedFlag_Perc_Chest`

Mode toggle / Pro shell:

- `Pro_Section_Symptoms`, `Pro_Section_Vitals`, `Pro_Section_Rules`, `Pro_Section_Model`, `Pro_Section_Results`, `Pro_Section_Ai`
- `Pro_Section_Expand`, `Pro_Section_Collapse`

Fallback rule: when a key is missing for the active language, fall back to English. When the English key is missing, the raw key is displayed and logged via `TranslationService.SaveMissingReport` (no app crash).

## Schema impact

- `data/conditions.json`, `data/categories.json`, `data/synonyms.json` — unchanged. Existing schemas (`schemas/*.schema.json`) remain valid.
- `data/translations.json` — additive only (entries with shape `{ "key": "...", "en": "...", "fr": "...", "ar": "..." }`); existing `schemas/translations.schema.json` already permits arbitrary keys.
- `data/settings.json` — additive only; no schema file exists today, and the in-memory `AppSettings` class is the de facto contract. Constitution III applies on the *next* structural change after this one.

## Confidence buckets (derived, not persisted)

| Model | Low | Moderate | High |
| ----- | --- | -------- | ---- |
| Jaccard | `< 0.20` | `< 0.50` | `≥ 0.50` |
| Cosine | `< 0.20` | `< 0.50` | `≥ 0.50` |
| NaiveBayes | `< 0.15` | `< 0.35` | `≥ 0.35` |

## Red-flag rules (educational only)

| Code | Trigger | Severity |
| ---- | ------- | -------- |
| `RF_Hypoxia` | `SpO₂ < 92` | 4 |
| `RF_Hypotension` | `SBP < 90` | 4 |
| `RF_SevereHypertension` | `SBP ≥ 180` or `DBP ≥ 120` | 3 |
| `RF_Tachycardia` | `HR ≥ 120` | 2 |
| `RF_Tachypnea` | `RR ≥ 30` | 3 |
| `RF_HighFever` | `Temp ≥ 40 °C` | 3 |
| `RF_PERC_Positive` | PERC positive AND ("Chest Pain" OR "Shortness of Breath" selected) | 4 |

Banner shows the most severe code first; all triggered codes are listed in the banner body.
