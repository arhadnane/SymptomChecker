# Decision Rules Specification

> **DISCLAIMER:** All decision rules in this application are simplified educational approximations. They do not replace clinical tools, validated scoring systems, or professional medical judgment. Real-world implementations of these scoring systems have additional nuances, context, and clinical prerequisites not captured here.

---

## 1. Centor Score

### 1.1 Purpose (Educational)

The Centor score estimates the likelihood of Group A Streptococcal (GAS) pharyngitis in patients with sore throat. It was developed to guide the need for rapid testing or empiric antibiotic therapy.

### 1.2 Components

| # | Criterion | Points | How Inferred in App |
|---|---|---|---|
| 1 | Fever (temperature ≥ 38.0 °C) | +1 | `TempC ≥ 38.0` OR "Fever" symptom is checked |
| 2 | Tonsillar exudates or swelling | +1 | "Sore Throat" symptom is checked (proxy) OR "Tonsillar Exudates"/"Tonsillar Swelling" if present |
| 3 | Tender anterior cervical lymphadenopathy | +1 | "Swollen Lymph Nodes" symptom is checked |
| 4 | Absence of cough | +1 | "Cough" symptom is NOT checked |

**Score range:** 0–4

### 1.3 Implementation

```csharp
bool hasFever = (TempC >= 38.0) || checkedSymptoms.Contains("Fever");
bool tonsils  = checkedSymptoms.Contains("Sore Throat") 
             || checkedSymptoms.Contains("Tonsillar Exudates")
             || checkedSymptoms.Contains("Tonsillar Swelling");
bool nodes    = checkedSymptoms.Contains("Swollen Lymph Nodes");
bool noCough  = !checkedSymptoms.Contains("Cough");

int centor = (hasFever ? 1 : 0) + (tonsils ? 1 : 0) + (nodes ? 1 : 0) + (noCough ? 1 : 0);
```

### 1.4 Educational Limitations

- Tonsillar exudates are proxied by "Sore Throat" — a significant simplification
- Physical exam findings cannot be reliably inferred from checkbox selections
- The real Centor score requires in-person clinical assessment

---

## 2. McIsaac Score (Modified Centor)

### 2.1 Purpose (Educational)

The McIsaac score adds an age adjustment to the Centor score to account for the differing prevalence of GAS pharyngitis across age groups.

### 2.2 Age Adjustment

| Age Group | Adjustment |
|---|---|
| < 15 years | +1 |
| 15–44 years | 0 |
| ≥ 45 years | −1 |

### 2.3 Computation

```
McIsaac = Centor + AgeAdjustment
Clamped to range [0, 5]
```

### 2.4 Advice Bands (Educational)

| McIsaac Score | Advice (Localized) |
|---|---|
| 0–1 | Low risk: likely viral. No antibiotics. Consider symptomatic care. |
| 2 | Intermediate risk: consider rapid strep test (RADT). |
| 3 | Higher risk: RADT and/or consider empiric antibiotics as per local guidance. |
| 4–5 | High risk: consider testing and/or empiric antibiotics per guidelines. |

### 2.5 UI Behavior

- Centor component checkboxes are **read-only** (disabled) — they reflect inferred state, not user input
- Centor and McIsaac scores are displayed as localized labels
- Advice text updates automatically when symptoms, vitals, or age change

---

## 3. PERC Rule (Pulmonary Embolism Rule-out Criteria)

### 3.1 Purpose (Educational)

The PERC rule is designed to identify patients at very low risk for pulmonary embolism (PE) who do not require further D-dimer testing. All 8 criteria must be met for PERC-negative status.

### 3.2 Criteria

| # | Criterion | Input Source | Pass Condition |
|---|---|---|---|
| 1 | Age < 50 | `_numAge.Value` | `age < 50` |
| 2 | Heart rate < 100 | `_numHR.Value` | `HR < 100` |
| 3 | SpO₂ ≥ 95% | `_numSpO2.Value` | `SpO2 >= 95` |
| 4 | No hemoptysis | `_percHemoptysis.Checked` | `!checked` |
| 5 | No estrogen use | `_percEstrogen.Checked` | `!checked` |
| 6 | No prior DVT/PE | `_percPriorDvtPe.Checked` | `!checked` |
| 7 | No unilateral leg swelling | `_percUnilateralLeg.Checked` | `!checked` |
| 8 | No recent surgery/trauma | `_percRecentSurgery.Checked` | `!checked` |

### 3.3 Result

```
PERC negative = ALL 8 criteria pass
```

| Result | Display Text (Localized) |
|---|---|
| PERC negative | "PERC negative — PE unlikely if pretest probability is low." |
| PERC positive | "PERC positive — cannot rule out PE; consider further testing if suspicion persists." |

### 3.4 PERC Integration with Triage

When PERC is positive **and** the user has "Chest Pain" or "Shortness of Breath" checked, the triage banner adds `RF_PERC_Positive` (severity priority 1).

### 3.5 Educational Limitations

- PERC is only valid when clinical pretest probability is already low (< 15%)
- The app does not assess pretest probability
- Real PERC requires clinical context not available from checkboxes alone

---

## 4. Triage Red-Flag Rules

### 4.1 Purpose (Educational)

The triage system flags symptom combinations and vital-sign thresholds that could indicate urgent conditions in a real clinical setting. This is for educational awareness only.

### 4.2 Symptom-Based Red Flags

| Key | Trigger | Severity |
|---|---|---|
| `RF_ChestPain_SOB` | "Chest Pain" AND "Shortness of Breath" | 1 (highest) |
| `RF_Fainting_ChestPain` | "Fainting" AND "Chest Pain" | 1 |
| `RF_Confusion` | "Confusion" | 1 |
| `RF_Fever_NeckPain_Light` | "Fever" AND ("Neck Pain" OR "Sensitivity to Light") | 2 |
| `RF_SevereCough_SOB` | "Severe Cough" AND "Shortness of Breath" | 2 |
| `RF_BloodInStool` | "Blood in Stool" | 2 |
| `RF_BloodInUrine` | "Blood in Urine" | 2 |
| `RF_TesticularPain` | "Testicular Pain" | 3 |

### 4.3 Vitals-Based Red Flags (Triage v2)

| Key | Trigger | Severity |
|---|---|---|
| `RF_Hypoxia` | SpO₂ < 92% | 1 |
| `RF_Hypotension` | SBP < 90 mmHg | 1 |
| `RF_PERC_Positive` | PERC positive + ("Chest Pain" OR "Shortness of Breath") | 1 |
| `RF_SevereHypertension` | SBP ≥ 180 OR DBP ≥ 120 | 2 |
| `RF_Tachycardia` | HR ≥ 120 bpm | 2 |
| `RF_Tachypnea` | RR ≥ 30 /min | 2 |
| `RF_HighFever` | Temp ≥ 40.0 °C | 2 |

### 4.4 Severity Priority System

```
Priority 1 = Most critical (red — immediate concern)
Priority 2 = High concern (orange — urgent review)
Priority 3 = Moderate concern (yellow — prompt attention)
Priority 99 = Unknown / unclassified (default)
```

Red flags are sorted by severity (ascending priority number), then alphabetically for stability.

### 4.5 Triage Banner Format

**LTR languages (EN, FR):**
```
Possible red flags:
 • [Flag 1 localized text]
 • [Flag 2 localized text]
If these apply, consider seeking urgent medical attention. This tool is educational, not medical advice.
```

**RTL language (AR):**
```
[Header]:
[Flag 1 localized text]  •
[Flag 2 localized text]  •
[Disclaimer]
```

### 4.6 Evaluation Flow

```
TriageService.EvaluateV2()
  1. Call Evaluate() for symptom-only flags
  2. Check each vital threshold → add keys
  3. Check PERC+chest/SOB context → add RF_PERC_Positive
  4. Deduplicate (HashSet)
  5. Sort by severity priority, then alphabetically
  6. Return list of message keys
```

---

## 5. Explicit Educational Disclaimers

The following disclaimers must be displayed in conjunction with decision rules:

| Context | Disclaimer |
|---|---|
| Main form footer | "⚠️ Educational only. Not medical advice." |
| Triage banner footer | "If these apply, consider seeking urgent medical attention. This tool is educational, not medical advice." |
| Details dialog | Treatments, medications, and care advice are labeled "educational" |
| Export files | Timestamp + "Educational only. Not medical advice." |
| PERC result | Explicitly states "PE unlikely if pretest probability is low" (negative) or "cannot rule out PE" (positive) |
| Centor/McIsaac | Advice bands use hedging language ("consider", "per guidelines") |

---

## 6. Rule Extensibility Strategy

### 6.1 Current Architecture

Rules are hardcoded in `TriageService.cs` (static class) and `MainForm.cs` (Centor/McIsaac/PERC logic).

### 6.2 Recommended Extension Approach

**Option A: JSON-Driven Rules (Preferred for simple thresholds)**

Define a `rules.json` file:

```json
{
  "version": "1.0",
  "symptomRules": [
    {
      "key": "RF_ChestPain_SOB",
      "requires": ["Chest Pain", "Shortness of Breath"],
      "requireAll": true,
      "severity": 1
    }
  ],
  "vitalRules": [
    {
      "key": "RF_Hypoxia",
      "vital": "SpO2",
      "operator": "<",
      "value": 92,
      "severity": 1
    }
  ]
}
```

A `RulesEngine` service loads and evaluates these rules without code changes.

**Option B: Plugin-Based Rules (For complex scoring systems)**

```csharp
public interface IDecisionRule
{
    string Id { get; }
    string DisplayNameKey { get; }
    DecisionRuleResult Evaluate(DecisionRuleContext context);
}

public record DecisionRuleContext(
    HashSet<string> SelectedSymptoms,
    VitalsSnapshot? Vitals,
    int? AgeYears,
    IDictionary<string, bool>? Flags
);
```

Each rule (Centor, PERC, custom) implements `IDecisionRule`. The UI discovers rules via registration and renders results dynamically.

### 6.3 Adding a New Rule (Current Process)

1. Add evaluation logic to `TriageService.EvaluateV2()` or a new method
2. Add severity entry to `SeverityPriority` dictionary
3. Add localized message key to `translations.json` (ui section)
4. Wire UI display in `UpdateTriageBanner()` or new panel
5. Add tests in `TriageServiceTests.cs`
