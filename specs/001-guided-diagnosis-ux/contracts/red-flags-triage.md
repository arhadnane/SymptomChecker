# Contract — Red Flags Triage

## API

```csharp
public static class TriageService
{
  public static IReadOnlyList<RedFlag> DetectRedFlags(
        VitalsSnapshot vitals,
        IReadOnlyCollection<string> selectedSymptoms,
    bool percPositive);
}

public readonly record struct VitalsSnapshot(
    double? TempC, int? HeartRate, int? RespRate,
    int? SystolicBP, int? DiastolicBP, int? SpO2);
```

## Rules (evaluated in this order)

| Code | Trigger | Severity | Localized message key |
| ---- | ------- | -------- | --------------------- |
| `RF_Hypoxia` | `SpO₂ < 92` (when value provided) | 4 | `RedFlag_Spo2_Low` |
| `RF_Hypotension` | `SBP < 90` | 4 | `RedFlag_Sbp_Low` |
| `RF_SevereHypertension` | `SBP ≥ 180` or `DBP ≥ 120` | 3 | `RedFlag_Sbp_High` |
| `RF_Tachycardia` | `HR ≥ 120` | 2 | `RedFlag_Hr_High` |
| `RF_Tachypnea` | `RR ≥ 30` | 3 | `RedFlag_Rr_High` |
| `RF_HighFever` | `Temp ≥ 40 °C` | 3 | `RedFlag_Temp_High` |
| `RF_PERC_Positive` | `percPositive` AND ("Chest Pain" OR "Shortness of Breath" in `selectedSymptoms`) | 4 | `RedFlag_Perc_Chest` |

- A `null` vital value MUST skip the corresponding rule (no false trigger).
- The returned list is ordered by descending severity, then by code (deterministic).
- If no rule fires, an empty list is returned and `RedFlagBanner` stays hidden.

## Presentation contract (UI side)

- When at least one flag fires:
  - `RedFlagBanner` becomes visible at the top of the results area in both shells.
  - Banner background uses a fixed red (`#B00020` light / `#CF6679` dark) with white text (AA contrast).
  - Header text uses `RedFlag_Banner_Title` and localizes each `MessageKey` through `TranslationService`.
  - A button `RedFlag_WhenToCall_Button` opens a localized informational dialog `RedFlag_WhenToCall_Body`.
  - The banner is **not dismissable** until inputs change.
- The banner never replaces the result list; it stacks above it.

## Tests required

- 8 unit tests covering each rule independently (positive and "not triggered" paths).
- 1 integration test: combined inputs (SpO₂ low + tachypnea + chest pain + PERC positive) — list contains all relevant codes, ordered by severity.
- UI localization is validated at the banner/control layer because the service returns stable codes plus message keys, not localized text.
