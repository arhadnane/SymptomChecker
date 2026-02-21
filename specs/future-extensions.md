# Future Extensions Specification

> **DISCLAIMER:** All extensions described here must preserve the educational-only nature of the application. No extension should cause the application to be classified as a medical device or clinical decision-support system.

---

## 1. Extension Principles

| Principle | Rationale |
|---|---|
| Backward compatibility | New features must not break existing session files, data files, or settings |
| Additive-only data changes | Never remove or rename existing fields; add new optional fields |
| Educational framing preserved | Every new feature must include appropriate disclaimers |
| Offline-first | Core functionality must work without network; online features are optional enrichment |
| Modular architecture | New capabilities added via interfaces and services, not monolithic code changes |

---

## 2. Architecture Extensions

### 2.1 Matching Model Plugin System

**Priority:** High  
**Rationale:** The current `switch` on `DetectionModel` in `SymptomCheckerService.GetMatches()` is a closed design. Adding a new model requires modifying the service.

**Proposed Design:**

```csharp
public interface IMatchingModel
{
    string Id { get; }           // e.g., "jaccard", "cosine", "naive-bayes"
    string DisplayNameKey { get; } // translation key for UI
    List<ConditionMatch> Evaluate(
        HashSet<string> selectedSymptoms,
        List<Condition> conditions,
        Dictionary<string, HashSet<string>> conditionSets,
        HashSet<string> vocabulary,
        MatchingOptions options
    );
}

public record MatchingOptions(
    double NaiveBayesTemperature = 1.0,
    double MinimumScore = 0.0
);
```

**Registration:**
```csharp
var models = new Dictionary<string, IMatchingModel>
{
    ["jaccard"] = new JaccardModel(),
    ["cosine"]  = new CosineModel(),
    ["naive-bayes"] = new NaiveBayesModel()
};
```

**Benefits:**
- Open/closed principle — add models without modifying existing code
- Each model is independently testable
- UI model selector populated from registry

**Candidate New Models:**
| Model | Description |
|---|---|
| TF-IDF | Weight symptoms by inverse frequency across conditions |
| Weighted Jaccard | Category weights applied to symptom overlap |
| Ensemble | Average/vote across multiple models |

### 2.2 Decision Rule Plugin System

**Priority:** High  
**Rationale:** Centor/McIsaac and PERC logic is currently embedded in `MainForm.cs`. New scoring systems (e.g., Wells Score, CURB-65) require UI and logic changes.

**Proposed Design:**

```csharp
public interface IDecisionRule
{
    string Id { get; }
    string DisplayNameKey { get; }
    IReadOnlyList<RuleCriterion> Criteria { get; }
    DecisionRuleResult Evaluate(DecisionRuleContext context);
}

public record RuleCriterion(
    string Key,
    string LabelKey,
    CriterionType Type,       // Boolean, Numeric, Enum
    object? DefaultValue
);

public record DecisionRuleResult(
    int Score,
    int MaxScore,
    string AdviceKey,
    RiskLevel Level
);

public enum RiskLevel { Low, Intermediate, High, Critical }
```

**Benefits:**
- Rules can be added without UI code changes
- Criteria drive dynamic UI generation (checkboxes for Boolean, NumericUpDown for Numeric)
- Scoring logic is testable in isolation

### 2.3 Data Provider Abstraction

**Priority:** Medium  
**Rationale:** Currently data is loaded from local JSON files only. Future providers could include SQLite, REST APIs, or bundled binary formats.

```csharp
public interface IDataProvider
{
    Task<List<Condition>> LoadConditionsAsync();
    Task<List<SymptomCategory>> LoadCategoriesAsync();
    Task<SymptomSynonyms> LoadSynonymsAsync();
    Task SaveConditionsAsync(List<Condition> conditions);
}
```

Implementations: `JsonFileDataProvider` (current), `SqliteDataProvider` (future), `ReadOnlyBundleProvider` (embedded resources).

---

## 3. Feature Extensions

### 3.1 Symptom–Condition Relationship Graph

**Priority:** Medium  
**Description:** Interactive visualization showing how symptoms connect to conditions.

**Implementation Notes:**
- Use a graph layout library (e.g., Microsoft Automatic Graph Layout — MSAGL) or custom GDI+ rendering
- Nodes: symptoms (circles) and conditions (rectangles)
- Edges: symptom→condition association
- Color coding: matched symptoms highlighted, red-flag symptoms marked
- Click a node to see details

**Educational Value:** Helps users understand why certain conditions score higher.

### 3.2 Symptom Frequency Analytics

**Priority:** Low  
**Description:** Track which symptoms are most/least selected across sessions for educational pattern analysis.

**Implementation Notes:**
- Aggregate data stored locally in `analytics.json`
- No personally identifiable data — only symptom selection counts
- Bar chart or heatmap visualization
- Reset option to clear analytics data

### 3.3 Condition Detail Cards

**Priority:** Medium  
**Description:** Richer educational information per condition beyond the current details dialog.

**Proposed Content:**
- Prevalence estimate (from Wikidata or manual annotation)
- Related conditions (conditions sharing ≥ 3 symptoms)
- Differential diagnosis hints (educational)
- External reference links (Wikipedia, MedlinePlus) — opens in default browser

### 3.4 Multi-Patient Comparison (Educational)

**Priority:** Low  
**Description:** Side-by-side comparison of two saved sessions to illustrate how different symptom profiles produce different results.

**Use Case:** Classroom setting where instructor demonstrates how adding/removing symptoms changes match rankings.

### 3.5 Guided Walkthrough Mode

**Priority:** Medium  
**Description:** Step-by-step tutorial mode for new users.

**Steps:**
1. Select a category → expand accordion
2. Check some symptoms → see instant feedback
3. Enter vitals → observe triage banner
4. Run check → explore results
5. Open details → review educational content
6. Export → save results

**Implementation:** Overlay tooltips pointing to each UI element in sequence.

---

## 4. Localization Extensions

### 4.1 Additional Languages

**Priority:** Medium  
**Current:** EN, FR, AR

**Candidate Languages:**
| Language | Code | RTL? | Notes |
|---|---|---|---|
| Spanish | es | No | Large user base |
| German | de | No | Medical education context |
| Chinese (Simplified) | zh-CN | No | Requires CJK font consideration |
| Urdu | ur | Yes | Shares RTL infrastructure with AR |

**Requirements per new language:**
1. Add translations to `translations.json` (ui, symptoms, conditions, messages, categories, uiDetails sections)
2. Add `Name_Xx` and `Treatment_Xx` fields to conditions (or use fallback to EN)
3. Test RTL layout if applicable
4. Validate font glyph coverage

### 4.2 Translation Workflow Tooling

**Priority:** Low  
**Description:** CLI tool or script to:
- Extract all translation keys and their EN values
- Generate a template file for translators
- Validate completeness of a new language against EN baseline
- Report missing keys (leveraging existing `_missing` set in `TranslationService`)

---

## 5. Data Extensions

### 5.1 Condition Metadata Enrichment

Add optional fields to `conditions.json`:

```json
{
  "Name": "Common Cold",
  "ICD10": "J00",
  "Prevalence": "very_common",
  "OnsetSpeed": "gradual",
  "TypicalDuration": "7-10 days",
  "AgeGroups": ["child", "adult", "elderly"],
  "Sources": ["https://en.wikipedia.org/wiki/Common_cold"]
}
```

All fields optional for backward compatibility.

### 5.2 Symptom Metadata

Add optional fields to category symptom entries:

```json
{
  "Name": "Fever",
  "Severity": "moderate",
  "BodySystem": "systemic",
  "CommonWith": ["infection", "inflammation"],
  "WikidataId": "Q38933"
}
```

### 5.3 Evidence Annotations

Each condition–symptom association could carry a weight or evidence level:

```json
{
  "Name": "Pneumonia",
  "SymptomWeights": {
    "Cough": 0.9,
    "Fever": 0.85,
    "Headache": 0.3
  }
}
```

This enables weighted matching models without changing the data structure fundamentally.

---

## 6. Technical Debt Reduction

### 6.1 MainForm Decomposition

**Priority:** Critical  
**Current:** 2548 lines in a single file

**Recommended Split:**

| Component | Responsibility | Est. Lines |
|---|---|---|
| `MainForm.cs` | Shell, initialization, top-level event wiring | ~200 |
| `MainForm.Layout.cs` | `InitializeLayout()`, control creation | ~400 |
| `MainForm.Theme.cs` | `ApplyTheme()`, color definitions | ~150 |
| `MainForm.Symptoms.cs` | Category accordion, search, checkbox logic | ~300 |
| `MainForm.Results.cs` | Owner-draw ListBox, result rendering | ~250 |
| `MainForm.DecisionRules.cs` | Centor, McIsaac, PERC UI logic | ~200 |
| `MainForm.Triage.cs` | Triage banner rendering | ~100 |
| `MainForm.Export.cs` | CSV, MD, HTML export methods | ~200 |
| `MainForm.Session.cs` | Save/load session, settings profiles | ~150 |
| `MainForm.Vitals.cs` | Vitals panel, value-change handlers | ~150 |

Use `partial class MainForm` to keep all code in the same class while splitting across files.

### 6.2 TranslationService Optimization

**Priority:** Medium  
**Current:** `FirstOrDefault()` linear scans on every lookup (O(n))

**Fix:** Build `Dictionary<string, T>` at load time, keyed by translation key. Reduces lookups to O(1).

### 6.3 Settings Validation

**Priority:** Low  
**Current:** `SettingsService` loads JSON without schema validation

**Fix:** Add `settings.schema.json` and validate at load; apply defaults for missing or out-of-range values.

### 6.4 Test Coverage Expansion

**Priority:** Medium  
**Current tests:** `SymptomCheckerServiceTests`, `TriageServiceTests`, `TranslationServiceTests`, `CategoriesServiceTests`, `CategoryWeightingTests`, `NaiveBayesTemperatureTests`

**Missing coverage:**
- `SynonymService` unit tests
- `WikidataImporter` tests (mock HTTP)
- `SchemaValidator` edge cases
- `LoggerService` rotation tests
- Export format validation tests
- RTL layout assertions (if feasible in headless test)

---

## 7. Deployment Extensions

### 7.1 Single-File Publish

```xml
<PublishSingleFile>true</PublishSingleFile>
<SelfContained>true</SelfContained>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
```

Produces a single `.exe` with embedded runtime — no .NET install required on target machine.

### 7.2 MSIX Packaging

Package as MSIX for Windows Store or enterprise sideloading:
- Auto-update support
- Clean install/uninstall
- Sandboxed file access (data files bundled as AppData)

### 7.3 CI/CD Pipeline

```yaml
# Proposed GitHub Actions workflow
name: Build & Test
on: [push, pull_request]
jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - run: dotnet build --configuration Release
      - run: dotnet test --configuration Release --logger trx
      - uses: actions/upload-artifact@v4
        with:
          name: test-results
          path: '**/*.trx'
```

---

## 8. Roadmap Summary

| Phase | Items | Priority |
|---|---|---|
| **Phase 1 — Stabilize** | MainForm decomposition, TranslationService optimization, test coverage expansion, synonyms schema | Critical / High |
| **Phase 2 — Modularize** | IMatchingModel interface, IDecisionRule interface, IDataProvider abstraction | High |
| **Phase 3 — Enrich** | Condition detail cards, symptom–condition graph, additional languages | Medium |
| **Phase 4 — Distribute** | Single-file publish, MSIX packaging, CI/CD pipeline | Medium |
| **Phase 5 — Explore** | Frequency analytics, guided walkthrough, multi-session comparison, evidence annotations | Low |
