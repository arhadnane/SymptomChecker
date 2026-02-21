# Functional Requirements

## 1. User Flows

### 1.1 Primary Flow — Symptom Check

```
1. Application starts → loads conditions.json, categories.json, synonyms.json, translations.json, settings.json
2. User optionally selects a language (EN/FR/AR) and theme (light/dark)
3. User filters symptoms using the filter text box and/or category selector
4. User checks one or more symptoms from the CheckedListBox
5. User selects a detection model (Jaccard / Cosine / Naive Bayes)
6. User adjusts parameters: Threshold (%), Min Match, Top-K
7. User clicks "Check"
8. Application computes matches and displays scored results grouped by category
9. Top-scoring result is highlighted; triage banner shows if red flags are detected
10. User double-clicks a result to see condition details (symptoms, treatments, medications, care advice)
```

### 1.2 Wikidata Sync Flow

```
1. User clicks "Sync"
2. Application fires a SPARQL query to Wikidata (no API key)
3. Fetched conditions are merged into the local dataset (additive merge)
4. Vocabulary is rebuilt; UI refreshes
5. User is notified of merge count or failure
```

### 1.3 Export Flow

```
1. User right-clicks the results list
2. User selects Export (CSV / Markdown / HTML)
3. SaveFileDialog opens; user chooses path
4. Application writes localized export file with condition, score, matches, category columns
5. Export folder is remembered for next use
```

### 1.4 Session Management

```
1. User clicks "Save" → saves selected symptoms + model + parameters to a JSON file
2. User clicks "Load" → restores a previously saved session
3. User clicks "Reset Settings" → reverts to defaults
4. User can save/load settings profiles (separate from sessions)
```

---

## 2. Feature List

### 2.1 Existing Features (Implemented)

| ID | Feature | Status |
|---|---|---|
| F-01 | Checkbox-based symptom selection (no free text) | Implemented |
| F-02 | Three detection models: Jaccard, Cosine (binary), Naive Bayes | Implemented |
| F-03 | Configurable threshold (%), min match count, top-K | Implemented |
| F-04 | Category-based symptom filtering and grouping | Implemented |
| F-05 | Synonym-aware filtering (canonical + aliases) | Implemented |
| F-06 | Localization: EN, FR, AR with RTL support | Implemented |
| F-07 | Dark mode toggle with persisted preference | Implemented |
| F-08 | Vitals input: Temp (°C), HR, RR, BP (SBP/DBP), SpO₂, Weight | Implemented |
| F-09 | Centor/McIsaac score computation with age adjustment | Implemented |
| F-10 | PERC rule evaluation (8 criteria) | Implemented |
| F-11 | Triage v2 banner: symptoms + vitals + PERC context | Implemented |
| F-12 | Wikidata SPARQL sync (no API key, limit 200) | Implemented |
| F-13 | Condition details dialog (symptoms, treatments, medications, care advice) | Implemented |
| F-14 | Export results: CSV, Markdown, HTML | Implemented |
| F-15 | Session save/load (JSON) | Implemented |
| F-16 | Settings profiles save/load | Implemented |
| F-17 | Settings persistence (data/settings.json) | Implemented |
| F-18 | Schema validation on startup (NJsonSchema) | Implemented |
| F-19 | Missing translations report dialog + export | Implemented |
| F-20 | Category weighting (multiply score by category factor) | Implemented |
| F-21 | Naive Bayes temperature scaling | Implemented |
| F-22 | Owner-drawn results list with group headers | Implemented |
| F-23 | Copy/Print details from context menu | Implemented |
| F-24 | Collapsible left panel | Implemented |
| F-25 | Keyboard shortcuts (Alt+F → filter, Alt+C → check) | Implemented |
| F-26 | Logging service with rotation and pruning | Implemented |
| F-27 | Help/About dialog | Implemented |
| F-28 | Score explainability in details dialog | Implemented |
| F-29 | Localized treatment/medication/care-advice fields per condition | Implemented |

### 2.2 Proposed Improvements

| ID | Improvement | Rationale |
|---|---|---|
| P-01 | Extract UI construction into a dedicated layout builder or UserControls | MainForm.cs is 2548 lines; splitting improves maintainability |
| P-02 | Introduce an interface `IMatchingModel` to decouple algorithms | Current switch-case in `GetMatches` violates OCP; new models require editing service |
| P-03 | Add unit tests for Cosine model and edge cases | Only Jaccard and NaiveBayes have partial test coverage |
| P-04 | Validate vitals ranges with user feedback (e.g., out-of-range warning) | Currently silently capped by NumericUpDown min/max |
| P-05 | Debounce filter text changes | Each keystroke triggers full list rebuild; noticeable with large datasets |
| P-06 | Add a "Select All / Deselect All" global button | Users currently rely on category-level or visible-level toggles |
| P-07 | Display matched-symptom chips/tags inline in results | Improves explainability beyond the details dialog |
| P-08 | Add condition-detail hyperlinks to trusted educational sources (e.g., Wikipedia) | Enriches learning without introducing clinical tools |
| P-09 | Support additional languages via translation file extension | Architecture supports it; only EN/FR/AR have data |
| P-10 | Add schema version field to all JSON data files | Enables safe migration when formats change |
| P-11 | Introduce `IDataProvider` abstraction over file-system JSON access | Enables testing with in-memory data and future data sources |

---

## 3. Validation Rules

### 3.1 Input Validation

| Input | Rule | Enforcement |
|---|---|---|
| Symptom selection | At least 1 symptom must be checked to enable "Check" | `_checkButton.Enabled` bound to `_checkedSymptoms.Count > 0` |
| Threshold | Integer 0–100 (interpreted as 0.00–1.00 score) | `NumericUpDown.Minimum=0, Maximum=100` |
| Min Match | Integer 0–10 | `NumericUpDown.Minimum=0, Maximum=10` |
| Top-K | Integer 0–1000 (0 = unlimited) | `NumericUpDown.Minimum=0, Maximum=1000` |
| Temperature (°C) | 30.0–45.0, step 0.1 | `NumericUpDown` bounds |
| Heart Rate (bpm) | 20–240 | `NumericUpDown` bounds |
| Respiratory Rate (/min) | 4–80 | `NumericUpDown` bounds |
| SBP (mmHg) | 50–260 | `NumericUpDown` bounds |
| DBP (mmHg) | 30–160 | `NumericUpDown` bounds |
| SpO₂ (%) | 50–100 | `NumericUpDown` bounds |
| Weight (kg) | 2.0–350.0 | `NumericUpDown` bounds |
| Age (years) | 0–120 | `NumericUpDown` bounds |
| NB Temperature | 0.10–5.00 (entered as 10–500, divided by 100) | `NumericUpDown` bounds |
| Category weight | 10–500 (0.1x–5.0x) | `NumericUpDown` bounds |

### 3.2 Data Validation

| Data File | Validation |
|---|---|
| `conditions.json` | Validated against `conditions.schema.json` — requires `conditions` array, each with non-empty `name` and `symptoms` (min 1, unique) |
| `categories.json` | Validated against `categories.schema.json` — requires `categories` array, each with non-empty `name` |
| `translations.json` | Validated against `translations.schema.json` — requires `languages` array (min 1, unique) |
| `synonyms.json` | No schema currently defined (improvement candidate) |
| `settings.json` | No schema — deserialization failure resets to defaults |

### 3.3 Merge Validation (Wikidata Sync)

- Condition names are trimmed. Empty/whitespace names are skipped.
- Symptom labels are normalized to Title Case.
- Duplicate symptoms within a condition are deduplicated (case-insensitive).
- Existing conditions are preserved; new symptoms are appended.
- Merge count of 0 results in a "No changes detected" message.

---

## 4. Error Handling Behavior

| Scenario | Behavior |
|---|---|
| `conditions.json` not found at startup | `FileNotFoundException` → MessageBox with error, app cannot proceed |
| `categories.json` / `synonyms.json` not found | Respective service is `null`; features degrade gracefully (no categories, no synonym matching) |
| `translations.json` not found | TranslationService loads empty DB; all keys fall back to canonical English strings |
| `settings.json` not found or corrupt | Settings reset to defaults; no user-visible error |
| Schema validation fails on startup | Non-blocking warning dialog listing validation errors |
| Wikidata sync network failure | Sync button re-enabled; MessageBox with error message; local data unchanged |
| Wikidata rate limit / timeout | Same as network failure |
| Unhandled UI thread exception | `Application.ThreadException` handler shows MessageBox and logs to file |
| Unhandled domain exception | `AppDomain.UnhandledException` handler shows MessageBox and logs to file |
| Export file write failure | MessageBox with exception message |
| Print failure | Falls back to direct `pd.Print()`; if both fail, silently swallowed |
