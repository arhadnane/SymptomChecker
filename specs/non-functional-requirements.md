# Non-Functional Requirements

## 1. Performance

| Metric | Target | Notes |
|---|---|---|
| Startup time (cold) | < 2 s | Load JSON files, validate schemas (async), build vocabulary, render UI |
| Symptom filter response | < 100 ms perceived | On keystroke; consider debouncing (current: immediate rebuild) |
| Match computation (Check) | < 200 ms for 60–100 conditions | Measured via `Stopwatch`; displayed in `_lblPerf` |
| Vocabulary rebuild | < 50 ms | Called after sync merge; linear scan of all condition symptoms |
| Export file generation | < 500 ms | In-memory string builder, single file write |
| Memory footprint | < 100 MB RSS | Small dataset (~60 conditions, ~200 symptoms); scales linearly |

### 1.1 Current Performance Observations

- `RefreshSymptomList()` rebuilds the full `CheckedListBox` on every filter keystroke. With large datasets (post-Wikidata sync), this may cause perceivable lag. **Recommendation:** introduce a 150–250 ms debounce timer.
- `GetMatches()` iterates all conditions × full vocabulary for Naive Bayes. With V > 500 symptoms, consider caching log-probability tables or pruning irrelevant conditions early.
- `_categorySetsCache` is well-designed and avoids redundant set-building.

---

## 2. Accessibility

### 2.1 Keyboard Navigation

| Requirement | Status |
|---|---|
| All interactive controls reachable via Tab | Implemented (TabIndex assigned 0–64) |
| Alt+F focuses filter box | Implemented |
| Alt+C triggers Check button | Implemented |
| Enter on focused button activates it | Default WinForms behavior |
| Escape closes dialogs | Not explicitly implemented; add `CancelButton` binding |

### 2.2 Screen Reader Support

| Requirement | Status |
|---|---|
| `AccessibleName` set on all primary controls | Implemented (filter, symptoms list, results, model selector, vitals, PERC checkboxes) |
| `AccessibleDescription` set where helpful | Partially implemented (filter, symptoms list, results, disclaimer, triage banner) |
| GroupBox text announces Centor/McIsaac and PERC sections | Implemented |
| Focus set to filter box on form shown | Implemented |

### 2.3 RTL Layout (Arabic)

| Requirement | Status |
|---|---|
| `RightToLeft = Yes` and `RightToLeftLayout = true` applied at Form level | Implemented |
| Individual controls explicitly set for RTL (lists, checkboxes, groups) | Implemented |
| Bullet points reversed for RTL (appended to end) | Implemented in banner and details |
| Result items rendered with `TextFormatFlags.RightToLeft | Right` | Implemented in owner-draw |
| Context menu mirrors for RTL | Implemented |

### 2.4 Contrast & Readability

| Requirement | Status |
|---|---|
| Light mode uses system colors (high contrast compatible) | Implemented via `SystemColors` |
| Dark mode uses sufficient contrast (Gainsboro on #202020) | Implemented; ratio ≈ 11:1 |
| Top result highlighted with distinct background (light green / dark green text) | Implemented |
| Triage banner uses warm accent (soft orange / khaki in dark) | Implemented |
| Check button uses accent green color for prominence | Implemented |

**Improvement:** Test with Windows High Contrast themes; current dark mode bypasses system HC settings.

---

## 3. Localization Rules

### 3.1 Supported Languages

| Code | Language | Direction | Status |
|---|---|---|---|
| `en` | English | LTR | Complete |
| `fr` | Français | LTR | Partially complete (missing keys tracked) |
| `ar` | العربية | RTL | Partially complete (missing keys tracked) |

### 3.2 Fallback Chain

```
Requested language → available translation → English fallback → raw key
```

- UI labels: `translations.json > ui[]` → match by `key`, select `fr`/`ar` field → fallback to `en` → fallback to literal key
- Symptoms: `translations.json > symptoms[]` → match by `key` → fallback to canonical name
- Conditions: `translations.json > conditions[]` → match by `key` → fallback to canonical name
- Categories: `translations.json > categories[]` → same pattern
- Details labels: `translations.json > ui_details[]` → same pattern
- Messages: `translations.json > messages[]` → same pattern

### 3.3 Missing Key Tracking

- `TranslationService._missing` collects keys not found or with empty localized values.
- On form close, a report is written to `data/translation_report.txt`.
- Users can view and export via the "Missing Translations" button.

### 3.4 Condition-Level Localization

Treatment, medication, and care-advice fields have `_Fr` / `_Ar` suffixed variants directly on the `Condition` model. The UI selects the localized variant with fallback to the base (English) field.

**Improvement:** Consider a more generic per-locale map (e.g., `Dictionary<string, LocalizedContent>`) to avoid N×L suffix fields as more languages are added.

---

## 4. Offline Behavior

| Scenario | Behavior |
|---|---|
| Normal operation (no network) | Fully functional — all data loaded from local `data/` folder |
| Wikidata sync without network | Sync fails gracefully with error MessageBox; local data unchanged |
| Export | Writes to local filesystem only |
| Settings persistence | Local file I/O only |
| Schema validation | Local schema files; no external fetch |

The application must never require a network connection for core functionality. Network is used exclusively for the optional Wikidata sync.

---

## 5. Maintainability & Extensibility

### 5.1 Current State Assessment

| Area | Assessment |
|---|---|
| Service layer | Well-separated: each service has a single file and clear responsibility |
| Models | Clean, minimal POCOs in `Models/` |
| UI layer | **MainForm.cs at 2548 lines is the primary maintainability risk** — mixes layout, event handling, business logic delegation, theming, and export |
| Data files | Clean JSON with schemas; categories and synonyms are user-editable |
| Test coverage | Tests exist for CategoriesService, SymptomCheckerService, TranslationService, TriageService, NaiveBayes, CategoryWeighting |
| Logging | Simple file-based logger with rotation |

### 5.2 Refactoring Recommendations

| Priority | Recommendation |
|---|---|
| High | Split `MainForm.cs` into partial classes or extract: `LayoutBuilder`, `ThemeManager`, `ExportService`, `DecisionRulesPanel` (UserControl), `VitalsPanel` (UserControl) |
| High | Introduce `IMatchingModel` interface with `Jaccard`, `Cosine`, `NaiveBayes` implementations — enables adding models without modifying `SymptomCheckerService` |
| Medium | Introduce `IDataProvider` interface over JSON file access — enables in-memory testing and potential future data sources |
| Medium | Move inline lambda event handlers in `InitializeLayout()` to named methods for testability and readability |
| Medium | Add a synonyms JSON schema (`synonyms.schema.json`) for startup validation |
| Low | Replace `try { } catch { }` swallowed exceptions with structured logging via `LoggerService` |
| Low | Extract export logic (CSV/MD/HTML) into a standalone `ExportService` class |

### 5.3 Extensibility Points

| Extension | Mechanism |
|---|---|
| New detection model | Implement `IMatchingModel` (proposed) and register in model selector |
| New language | Add language code to `translations.json > languages[]`, add translations for all categories |
| New symptom category | Add entry to `categories.json` with keywords and/or explicit symptoms |
| New condition | Add to `conditions.json` or sync from Wikidata; vocabulary auto-rebuilds |
| New decision rule | Add method to `TriageService` or new service; wire into triage banner |
| New export format | Add method to `ExportService` (proposed) and context menu item |
