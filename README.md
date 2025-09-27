# Symptom Checker (Educational)

A Windows Forms desktop app in C# where you select symptoms from a predefined list and the app suggests possible conditions from a JSON dataset. No free text input; it’s list-based for simplicity and learning.

- Input: Checkboxes only (no free text)
- Data: `data/conditions.json`
- Output: Suggested condition names with scores and a clear disclaimer

## Requirements

- .NET 8 SDK (Windows)

## Run

1. Open this folder in VS Code.
2. Build and run.

Optional commands (PowerShell):

```powershell
# Restore and build
dotnet restore; dotnet build

# Run
dotnet run
```

## Features at a glance

- Model weighting & calibration
- Vitals inputs: Temp (°C), HR, RR, BP (SBP/DBP), SpO₂, Weight (kg)
- Decision rules (educational):
  - Centor/McIsaac scores with age adjustment and brief advice
  - PERC rule: pass/fail with component checklist
- Triage v2: red‑flag banner now considers symptoms + vitals thresholds + PERC context
- Internationalization and UX: EN/FR/AR with RTL support, dark mode, and persisted settings

## How to use

1. Use the filter box to quickly find symptoms. Click “Select Visible” to check all currently filtered items, or “Clear Visible” to uncheck them. Your previous checks are preserved when you change the filter text.

1. Pick a detection model from the dropdown:

- Jaccard: set overlap (good baseline)
- Cosine (binary): vector similarity on presence/absence
- Naive Bayes: probabilistic model with smoothing; scores are normalized

1. Adjust parameters as needed:

- Threshold (%): minimum score to keep a result
- Min Match: require at least N overlapping symptoms
- Top‑K: show only the top K matches (optional)

1. Click “Check” to compute matches. Results show “Condition — Score: X.XX — Matches: N”. The top score is highlighted. Only real matches are shown.

1. Double‑click a result to open a details dialog with the full symptom list.

Tip for labs: Use the Category selector and choose “Laboratory Findings” to quickly pick lab items like High WBC, Elevated ALT/AST, High HbA1c, etc.

Tip: Start with Jaccard and a low threshold (e.g., 0–30%) for exploratory use.

### Using vitals and decision rules (educational)

- Enter vitals in the Vitals row:
  - TempC (°C), Heart Rate (bpm), Resp Rate (/min), BP (SBP/DBP), SpO₂ (%), Weight (kg)
  - Values persist between runs. Units are metric.
- Centor/McIsaac group:
  - Centor components are inferred from your current selection and vitals:
    - Fever: Temp ≥ 38°C or “Fever” symptom
    - Tonsillar exudates/swelling: proxied by “Sore Throat” (educational simplification)
    - Tender anterior cervical nodes: “Swollen Lymph Nodes” symptom
    - Absence of cough: no “Cough” symptom selected
  - McIsaac age adjustment: +1 if < 15 years; −1 if ≥ 45 years
  - The UI shows localized Centor and McIsaac scores plus brief advice per score band
- PERC group (PE Rule‑out Criteria):
  - Evaluates 8 items: Age < 50, HR < 100, SpO₂ ≥ 95, and absence of hemoptysis, estrogen use, prior DVT/PE, unilateral leg swelling, recent surgery/trauma
  - If all are met → PERC negative (with low pretest probability, PE is unlikely). Otherwise → PERC positive
  - The result text is localized

### Triage banner (v2)

The banner lists possible red flags based on symptoms and now also on vitals and PERC context. Thresholds used (educational only):

- SpO₂ < 92% → Hypoxia
- SBP < 90 → Hypotension
- SBP ≥ 180 or DBP ≥ 120 → Severely high blood pressure
- HR ≥ 120 → Tachycardia
- RR ≥ 30 → Tachypnea
- Temp ≥ 40°C → Very high fever
- PERC positive with chest pain or shortness of breath → cannot rule out PE

Notes:

- These signals are for learning only and do not replace clinical judgment.
- The banner text is localized; layout adapts to RTL languages.

### Exporting results

You can export the current results via the results list context menu:

- Export Results (CSV): Writes a UTF‑8 CSV with columns: Condition, Score, Matches, Category
- Export Results (Markdown): Writes a Markdown table with the same columns

Details:

- Column headers are localized (EN/FR/AR). Category is determined by best symptom overlap with your dataset categories.
- RTL languages are supported in the UI; the file content remains left‑to‑right for portability.

## Detection models

- Jaccard: |A ∩ B| / |A ∪ B| on sets of symptoms (selected vs condition)
- Cosine (binary): dot(A,B) / (||A||·||B||), where vectors are 1 if symptom present
- Naive Bayes (Bernoulli + Laplace smoothing): per‑symptom presence odds; normalized across conditions

Notes:

- The app returns no results if no symptoms are selected (prevents false highlighting).
- Scores are sorted descending; ties break by higher match count.

## Sync with Wikidata (no key)

- Click “Sync” to fetch disease–symptom pairs from Wikidata’s SPARQL endpoint and merge them into the local dataset.
- The app normalizes labels (Title Case) and saves to `data/conditions.json` so you can use the data offline later.
- If Wikidata is slow or rate‑limited, try again later. Your existing local JSON remains usable.

## Project structure

```text
SymptomChecker/
├── data/
│   └── conditions.json            # Local dataset (also updated after Sync)
│   └── categories.json            # Symptom category groups (editable)
│   └── translations.json          # UI/messages + symptom/condition/category labels (i18n)
│   └── synonyms.json              # Aliases (e.g., HbA1c → High HbA1c, Rhinorrhea → Runny Nose)
├── Models/
│   ├── Symptom.cs
│   ├── Condition.cs
│   └── ConditionMatch.cs
├── Services/
│   ├── SymptomCheckerService.cs   # Matching logic + merge/save utilities
│   └── CategoriesService.cs       # Loads categories and maps to symptoms
│   └── WikidataImporter.cs        # No‑key sync via SPARQL
├── UI/
│   └── MainForm.cs                # UI wiring, filtering, highlight, details dialog
├── Program.cs
└── SymptomChecker.csproj
```

## Diagrams

For a comprehensive set of UML-style diagrams (Use Case, Class, Sequence, Activity, State, Component, Deployment, Package, and more), see docs/uml-diagrams.md.

### Architecture overview

```mermaid
graph TD
  U[User] --> M[UI/MainForm.cs]

  subgraph UI
    M --> RL[Results List]
    M --> DD[Details Dialog]
  end

  subgraph Services
    M --> SvcSC[SymptomCheckerService]
    M --> SvcCat[CategoriesService]
    M --> SvcSyn[SynonymService]
    M --> SvcTr[TranslationService]
    M --> SvcSet[SettingsService]
    M --> SvcWiki[WikidataImporter]
  end

  subgraph Data Files
    SvcSC -->|load/save| cond[data/conditions.json]
    SvcCat --> cats[data/categories.json]
    SvcSyn --> syn[data/synonyms.json]
    SvcTr --> tr[data/translations.json]
    SvcSet --> set[data/settings.json]
  end

  SvcWiki -->|fetch & merge| SvcSC
  M -->|runs checks| SvcSC
  M -->|filter/category/synonyms| SvcCat
  M -->|filter/search| SvcSyn
  M -->|labels & messages| SvcTr
  M -->|persist language| SvcSet
```

### Localization data flow (details dialog)

```mermaid
sequenceDiagram
  participant User
  participant UI as MainForm (UI)
  participant TR as TranslationService
  participant DB as data/conditions.json

  User->>UI: Double‑click a result
  UI->>TR: T(), TDetails() for labels
  UI->>DB: Read condition by canonical name
  alt Language = fr/ar
    UI->>DB: Prefer Treatments_Fr/Ar, Medications_Fr/Ar, CareAdvice_Fr/Ar
  else Language = en or missing localized fields
    UI->>DB: Fallback to Treatments, Medications, CareAdvice
  end
  UI-->>User: Show localized details (with fallback)
```

Note: GitHub and VS Code Preview support Mermaid. If needed, enable Mermaid preview extensions in your editor.

## Data

- `data/conditions.json` contains a curated set of ~60+ conditions with standardized symptom names.
- `data/categories.json` lists broad symptom categories. Each category can define:
  - `keywords`: substrings to match symptoms (case-insensitive). Example: "Cough", "Wheez", "Nasal".
  - `symptoms`: optional explicit list of symptom names. If present, it overrides keyword matching.
- `data/synonyms.json` provides alias mappings used by the filter and category keyword matching.
- Sync will merge new conditions and symptoms; existing items are preserved where possible.
- You can edit the JSON manually if you wish; the app rebuilds its symptom vocabulary at startup and after Sync.

## Troubleshooting

- No results after clicking “Check”:
  - Ensure you’ve selected at least one symptom.
  - Lower the threshold, reduce Min Match, or increase Top‑K.
- Unexpected matches:
  - Try Jaccard or Cosine first; increase Min Match to 2.
  - Naive Bayes may surface conditions with partial evidence due to smoothing.
- Filter not finding a symptom:
  - Check for synonymous terms (e.g., “Runny Nose” vs “Rhinorrhea”). The dataset uses common lay terms.

## Disclaimer

This tool is for educational purposes only and is not a substitute for professional medical advice, diagnosis, or treatment.

## Ideas to extend

- Synonym mapping (e.g., “Rhinorrhea” → “Runny Nose”) and symptom categories (respiratory, GI, neuro)
- Export results to a file and basic analytics (symptom frequency)
- Visualization of symptom–condition graph
