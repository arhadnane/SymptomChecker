# Architecture Specification

## 1. High-Level Architecture

```
┌─────────────────────────────────────────────────────────┐
│                      Program.cs                         │
│  (Entry point, exception handlers, logger bootstrap)    │
└──────────────────────┬──────────────────────────────────┘
                       │ creates
                       ▼
┌─────────────────────────────────────────────────────────┐
│                    UI / MainForm.cs                      │
│  ┌──────────┐  ┌──────────┐  ┌───────────┐              │
│  │ Left     │  │ Right    │  │ Dialogs   │              │
│  │ Panel    │  │ Panel    │  │ (Details,  │              │
│  │ (Filter, │  │ (Model,  │  │  Help,     │              │
│  │ Symptom  │  │ Vitals,  │  │  Missing   │              │
│  │ List)    │  │ Rules,   │  │  Trans.)   │              │
│  │          │  │ Results, │  │            │              │
│  │          │  │ Triage)  │  │            │              │
│  └──────────┘  └──────────┘  └───────────┘              │
└──────────────────────┬──────────────────────────────────┘
                       │ delegates to
                       ▼
┌─────────────────────────────────────────────────────────┐
│                   Services Layer                         │
│                                                         │
│  SymptomCheckerService   CategoriesService               │
│  SynonymService          TranslationService              │
│  SettingsService         TriageService (static)          │
│  WikidataImporter        SchemaValidator (static)        │
│  LoggerService                                           │
└──────────────────────┬──────────────────────────────────┘
                       │ reads / writes
                       ▼
┌─────────────────────────────────────────────────────────┐
│                   Data Layer (Files)                     │
│                                                         │
│  data/conditions.json    data/categories.json            │
│  data/synonyms.json      data/translations.json          │
│  data/settings.json      schemas/*.schema.json           │
│  logs/log_*.txt                                          │
└─────────────────────────────────────────────────────────┘
```

## 2. Service Responsibilities

| Service | Responsibility | State |
|---|---|---|
| `SymptomCheckerService` | Loads conditions, builds vocabulary, computes matches (Jaccard/Cosine/NaiveBayes), merges external data, saves database | Stateful — holds `ConditionDatabase`, vocabulary list, condition-set cache |
| `CategoriesService` | Loads category definitions, builds category-to-symptom sets from keywords or explicit lists | Stateful — holds `SymptomCategoryDatabase` |
| `SynonymService` | Loads synonym mappings, resolves aliases to canonical symptom names, enables synonym-aware filtering | Stateful — holds `SynonymDatabase` |
| `TranslationService` | Loads translations, resolves UI/symptom/condition/category/message/details keys per language, tracks missing keys | Stateful — holds `TranslationDatabase`, current language, missing keys set |
| `SettingsService` | Loads/saves user preferences (language, theme, model, vitals, PERC flags, etc.) | Stateful — holds `AppSettings`, file path |
| `TriageService` | Evaluates red-flag patterns from symptoms and vitals; returns localization keys | Stateless (static) |
| `SchemaValidator` | Validates JSON data files against JSON Schema (NJsonSchema) | Stateless (static) |
| `WikidataImporter` | Fetches disease-symptom pairs via SPARQL, normalizes labels to Title Case | Stateful — holds `HttpClient` |
| `LoggerService` | Writes timestamped log entries, rotates files at 512 KB, prunes to 5 files | Stateful — holds file path, lock object |

## 3. Data Flow

### 3.1 Startup Sequence

```
Program.Main()
  ├── Create LoggerService(logs/)
  ├── Create MainForm
  │     └── InitializeLayout() — build all controls
  └── Application.Run(form)
        └── MainForm_Load()
              ├── [Async] SchemaValidator.ValidateAsync() for conditions, categories, translations
              ├── SymptomCheckerService(data/conditions.json)
              │     └── Deserialize → ConditionDatabase
              │     └── RebuildVocabulary()
              ├── SettingsService(data/settings.json)
              ├── TranslationService(data/translations.json)
              ├── CategoriesService(data/categories.json)
              │     └── Build _categorySetsCache per category
              ├── SynonymService(data/synonyms.json)
              ├── Restore UI state from settings
              ├── ApplyTranslations()
              ├── ApplyTheme()
              └── RefreshSymptomList()
```

### 3.2 Match Computation Flow

```
User clicks "Check"
  └── CheckButton_Click()
        ├── Collect _checkedSymptoms
        ├── Read model, threshold, minMatch, topK from controls
        ├── Read categoryWeights, nbTemperature from settings
        ├── Define GetCats() delegate using _categorySetsCache
        ├── Call _service.GetMatches(...)
        │     ├── Build selectedSet (case-insensitive)
        │     ├── Guard: empty selection → return []
        │     ├── Switch on model:
        │     │     ├── Jaccard: |A∩B| / |A∪B|
        │     │     ├── Cosine: dot(A,B) / (‖A‖·‖B‖)
        │     │     └── NaiveBayes: log-likelihood → softmax → optional temperature
        │     ├── Filter by threshold
        │     ├── Filter by minMatchCount
        │     ├── Apply category weighting (multiply score, renormalize for NB)
        │     ├── Sort: score desc, matchCount desc, name asc
        │     └── Apply topK limit
        ├── Store _lastResults
        ├── RebuildResultsListItems() — group by category, add headers
        ├── UpdateTriageBanner() — call TriageService.EvaluateV2()
        └── Display elapsed time
```

### 3.3 Wikidata Sync Flow

```
User clicks "Sync"
  └── SyncFromWikidataAsync()
        ├── WikidataImporter.FetchConditionsAsync(limit: 200)
        │     ├── Construct SPARQL query for diseases with symptoms
        │     ├── HTTP GET to query.wikidata.org/sparql
        │     ├── Deserialize SPARQL JSON results
        │     └── Normalize labels to Title Case, group by disease
        ├── _service.MergeConditions(fetched)
        │     ├── For each new condition: add with deduped symptoms
        │     ├── For existing conditions: union symptoms
        │     └── RebuildVocabulary() if changes > 0
        ├── _service.SaveDatabase() — write updated conditions.json
        └── Refresh _allSymptoms and UI
```

## 4. Dependency Boundaries

### 4.1 Current Dependencies

```
MainForm ──depends──▶ SymptomCheckerService
         ──depends──▶ CategoriesService
         ──depends──▶ SynonymService
         ──depends──▶ TranslationService
         ──depends──▶ SettingsService
         ──depends──▶ TriageService (static)
         ──depends──▶ WikidataImporter
         ──depends──▶ SchemaValidator (static)

SymptomCheckerService ──depends──▶ Models (Condition, ConditionDatabase, ConditionMatch)
CategoriesService     ──depends──▶ Models (SymptomCategory, SymptomCategoryDatabase)
SynonymService        ──depends──▶ Models (SymptomSynonyms, SynonymDatabase)
WikidataImporter      ──depends──▶ Models (Condition)
                      ──depends──▶ System.Net.Http.HttpClient

All services ──depends──▶ System.Text.Json
SchemaValidator ──depends──▶ NJsonSchema (NuGet)
```

### 4.2 Coupling Issues

| Issue | Impact | Recommendation |
|---|---|---|
| `MainForm` directly instantiates all services | No dependency injection; difficult to test or swap implementations | Introduce constructor injection or a simple service locator |
| `SymptomCheckerService.GetMatches()` contains all three model algorithms in a switch | Adding a model requires editing the service | Extract `IMatchingModel` with `CalculateScore(selectedSet, conditionSet, vocabulary)` |
| `TriageService` is static with hardcoded rule set | Adding rules requires code changes | Consider rule definitions in JSON or a pipeline pattern |
| `MainForm` accesses `SettingsService.Settings` properties directly throughout | Tight coupling to settings shape | Use an event-based or reactive pattern for settings changes |
| `TranslationService` uses `FirstOrDefault` linear scans for every lookup | Functional but O(n) per call | Build `Dictionary<string, T>` indexes at load time |

## 5. Suggested Refactors

### 5.1 MainForm Decomposition (Priority: High)

Split the 2548-line `MainForm.cs` into:

| Component | Responsibility |
|---|---|
| `MainForm.cs` | Orchestration, form setup, top-level event wiring |
| `MainForm.Layout.cs` (partial) | `InitializeLayout()` and control construction |
| `MainForm.Theme.cs` (partial) | `ApplyTheme()`, `ApplyThemeToControl()` |
| `MainForm.Export.cs` (partial) or `ExportService` | CSV, Markdown, HTML export logic |
| `VitalsPanel : UserControl` | Vitals input controls (Temp, HR, RR, BP, SpO₂, Weight) |
| `DecisionRulesPanel : UserControl` | Centor/McIsaac + PERC UI and computation |
| `ResultsRenderer` | Owner-draw logic for `ResultsList_DrawItem`, `MeasureItem` |

### 5.2 Model Strategy Pattern (Priority: High)

```csharp
public interface IMatchingModel
{
    string Name { get; }
    List<ConditionMatch> ComputeMatches(
        HashSet<string> selectedSymptoms,
        IReadOnlyList<Condition> conditions,
        IReadOnlyList<string> vocabulary,
        double threshold);
}
```

Register implementations: `JaccardModel`, `CosineModel`, `NaiveBayesModel`. The service iterates registered models; `MainForm` populates the dropdown from available model names.

### 5.3 Translation Lookup Optimization (Priority: Medium)

Replace `List<T>.FirstOrDefault()` lookups with `Dictionary<string, T>` built once at construction. Current complexity per lookup: O(n). After: O(1).

### 5.4 Data Provider Abstraction (Priority: Medium)

```csharp
public interface IConditionDataProvider
{
    ConditionDatabase Load();
    void Save(ConditionDatabase db);
}
```

Implementations: `JsonFileConditionDataProvider`, `InMemoryConditionDataProvider` (for tests).

## 6. Component Diagram

```
┌──────────────────────────────────────────────────┐
│                    Application                    │
│                                                  │
│  ┌──────────┐     ┌────────────────────────┐     │
│  │ Program  │────▶│ UI Layer               │     │
│  │ .cs      │     │  MainForm              │     │
│  │          │     │  (Dialogs)             │     │
│  └──────────┘     └───────────┬────────────┘     │
│                               │                  │
│                    ┌──────────▼──────────┐        │
│                    │  Services Layer     │        │
│                    │                     │        │
│                    │  SymptomChecker*    │        │
│                    │  Categories*        │        │
│                    │  Synonym*           │        │
│                    │  Translation*       │        │
│                    │  Settings*          │        │
│                    │  Triage (static)    │        │
│                    │  WikidataImporter   │        │
│                    │  SchemaValidator    │        │
│                    │  Logger             │        │
│                    └──────────┬──────────┘        │
│                               │                  │
│                    ┌──────────▼──────────┐        │
│                    │  Models Layer       │        │
│                    │  Condition          │        │
│                    │  ConditionMatch     │        │
│                    │  SymptomCategory    │        │
│                    │  SynonymMap         │        │
│                    └──────────┬──────────┘        │
│                               │                  │
│                    ┌──────────▼──────────┐        │
│                    │  Data Layer (Files) │        │
│                    │  data/*.json        │        │
│                    │  schemas/*.json     │        │
│                    │  logs/*.txt         │        │
│                    └────────────────────┘         │
│                                                  │
│  External:                                       │
│  ┌───────────────────────┐                       │
│  │ Wikidata SPARQL API   │ (optional, no key)    │
│  └───────────────────────┘                       │
│  ┌───────────────────────┐                       │
│  │ NJsonSchema (NuGet)   │                       │
│  └───────────────────────┘                       │
└──────────────────────────────────────────────────┘
```
