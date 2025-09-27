# UML Diagrams (Mermaid-based)

This document collects common UML diagram types for the Symptom Checker app, rendered using Mermaid. Some notations are approximations due to Mermaid’s syntax.

Contents:

- Use Case
- Class
- Sequence
- Activity
- State Machine
- Component
- Deployment
- Package
- Communication (Collaboration) — approximated
- Object diagram (snapshot) — approximated
- Timing — approximated
- Composite Structure — approximated

Note: Indentation uses spaces (no hard tabs) to keep markdown linters happy.

---

## Use Case

```mermaid
graph TD
  actor[User]
  uc1((Select Symptoms))
  uc2((Compute Matches))
  uc3((View Condition Details))
  uc4((Sync Dataset))
  actor --> uc1
  actor --> uc2
  actor --> uc3
  actor --> uc4
  uc1 --> uc2
  uc2 --> uc3
```

## Class Diagram

```mermaid
classDiagram
  class Condition {
    +string Name
    +List<string> Symptoms
    +List<string>~?~ Treatments
    +List<string>~?~ Treatments_Fr
    +List<string>~?~ Treatments_Ar
    +List<string>~?~ Medications
    +List<string>~?~ Medications_Fr
    +List<string>~?~ Medications_Ar
    +string~?~ CareAdvice
    +string~?~ CareAdvice_Fr
    +string~?~ CareAdvice_Ar
  }
  class ConditionDatabase {
    +List<Condition> Conditions
  }
  class SymptomCheckerService {
    +GetMatches(symptoms, model, threshold, topK, minMatchCount) List<ConditionMatch>
    +MergeConditions(external) int
    +SaveDatabase() void
  }
  class CategoriesService {
    +GetAllCategories() List<Category>
    +BuildCategorySet(category, allSymptoms) Set<string>
  }
  class SynonymService {
    +MatchSymptomsByQuery(query, items) IEnumerable<string>
  }
  class TranslationService {
    +SetLanguage(lang) void
    +T(key) string
    +TDetails(key) string
    +Symptom(key) string
    +Condition(key) string
    +Category(key) string
  }
  class SettingsService {
    +Settings.Language : string
    +Save() void
  }
  class WikidataImporter {
    +FetchConditionsAsync(limit) Task<List<Condition>>
  }
  class ConditionMatch {
    +string Name
    +double Score
    +int MatchCount
  }
  class MainForm {
    -_symptomList : CheckedListBox
    -_resultsList : ListBox
    -_languageSelector : ComboBox
    -_modelSelector : ComboBox
    ...
  }

  SymptomCheckerService --> Condition
  SymptomCheckerService --> ConditionMatch
  CategoriesService --> Category
  MainForm --> SymptomCheckerService
  MainForm --> CategoriesService
  MainForm --> SynonymService
  MainForm --> TranslationService
  MainForm --> SettingsService
  MainForm --> WikidataImporter
```

## Sequence Diagram — Check and Details

```mermaid
sequenceDiagram
  participant U as User
  participant UI as MainForm
  participant SC as SymptomCheckerService
  participant TR as TranslationService
  U->>UI: Select symptoms + Click Check
  UI->>SC: GetMatches(symptoms, model, thr, topK, minMatch)
  SC-->>UI: List<ConditionMatch>
  UI-->>U: Show ranked results (highlight top)
  U->>UI: Double-click a result
  UI->>TR: T(), TDetails() for labels
  UI->>SC: TryGetCondition(name)
  SC-->>UI: Condition
  UI-->>U: Details with localized treatments/meds/advice
```

## Activity Diagram — Filtering and Selection

```mermaid
flowchart TD
  A[Start] --> B{Filter text empty?}
  B -- Yes --> C[Show all symptoms]
  B -- No --> D[Apply synonyms + translation matches]
  C --> E[User checks items]
  D --> E[User checks items]
  E --> F{Category Only?}
  F -- Yes --> G[Restrict to selected category]
  F -- No --> H[Keep filtered set]
  G --> I[Select/Clear Visible]
  H --> I[Select/Clear Visible]
  I --> J[Click Check]
  J --> K[Compute & show results]
  K --> L[End]
```

## State Machine — Language/RTL

```mermaid
stateDiagram-v2
  [*] --> English
  English --> French : Lang = fr
  English --> Arabic : Lang = ar
  French --> English : Lang = en
  French --> Arabic : Lang = ar
  Arabic --> English : Lang = en
  Arabic --> French : Lang = fr
  state Arabic {
    [*] --> RTL
  }
```

## Component Diagram

```mermaid
graph LR
  UI[WinForms UI]
  SC[SymptomCheckerService]
  CAT[CategoriesService]
  SYN[SynonymService]
  TR[TranslationService]
  SET[SettingsService]
  WIKI[WikidataImporter]
  COND[(conditions.json)]
  CATS[(categories.json)]
  SYNJ[(synonyms.json)]
  TRJ[(translations.json)]
  SETJ[(settings.json)]

  UI --> SC
  UI --> CAT
  UI --> SYN
  UI --> TR
  UI --> SET
  UI --> WIKI
  SC --> COND
  CAT --> CATS
  SYN --> SYNJ
  TR --> TRJ
  SET --> SETJ
  WIKI --> SC
```

## Deployment Diagram

```mermaid
graph TD
  subgraph Workstation
    App[SymptomChecker.exe]
    Files[(data/*.json)]
  end
  subgraph Internet
    WD[Wikidata SPARQL]
  end
  App --- Files
  App === WD
```

## Package Diagram

```mermaid
graph TD
  A[Models]
  B[Services]
  C[UI]
  D[data]
  E[Root]
  A --> B
  B --> C
  D --> B
  D --> C
  E --> A
  E --> B
  E --> C
```

## Communication Diagram (approx.)

```mermaid
graph TD
  U[User]
  UI[MainForm]
  SC[SymptomCheckerService]
  TR[TranslationService]
  U -- 1:Check --> UI
  UI -- 2:GetMatches --> SC
  SC -- 3:List<ConditionMatch> --> UI
  U -- 4:DoubleClick --> UI
  UI -- 5:T()/TDetails() --> TR
  UI -- 6:Show Details --> U
```

## Object Diagram (snapshot, approx.)

```mermaid
classDiagram
  class Condition~Flu~ {
    Name = "Flu"
    Symptoms = [Fever, Cough, Fatigue]
    Treatments = [Rest, Hydration]
  }
  class ConditionMatch~Flu~ {
    Name = "Flu"
    Score = 0.83
    MatchCount = 3
  }
```

## Timing Diagram (approx.)

```mermaid
gantt
  dateFormat X
  title UI Response Timeline
  section Interaction
  Select symptoms        :done,    0, 1
  Compute matches        :active,  1, 1
  Render results         :         2, 0.5
  Open details           :         2.5, 0.5
```

## Composite Structure (approx.)

```mermaid
graph TD
  MainForm -->|contains| CheckedListBox
  MainForm -->|contains| ListBox
  MainForm -->|contains| ComboBox
  MainForm -->|depends on| SymptomCheckerService
  SymptomCheckerService -->|uses| ConditionDatabase
```
