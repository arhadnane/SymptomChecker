# Data Specification

## 1. Data Files Overview

| File | Purpose | Schema | Required |
|---|---|---|---|
| `data/conditions.json` | Disease-symptom mappings + educational treatment info | `schemas/conditions.schema.json` | Yes |
| `data/categories.json` | Symptom category groups (keyword or explicit list) | `schemas/categories.schema.json` | No (degrades gracefully) |
| `data/translations.json` | Localized UI strings, symptom/condition/category names | `schemas/translations.schema.json` | No (falls back to English keys) |
| `data/synonyms.json` | Alias-to-canonical symptom mappings | None (see §1.1) | No (degrades gracefully) |
| `data/settings.json` | Persisted user preferences | None (auto-generated) | No (reset to defaults) |

### 1.1 Missing Schema: synonyms.json

**Recommendation:** Create `schemas/synonyms.schema.json`:

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://example.com/schemas/synonyms.schema.json",
  "title": "SynonymDatabase",
  "type": "object",
  "required": ["synonyms"],
  "properties": {
    "synonyms": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["canonical", "aliases"],
        "properties": {
          "canonical": { "type": "string", "minLength": 1 },
          "aliases": {
            "type": "array",
            "items": { "type": "string", "minLength": 1 },
            "minItems": 1
          }
        },
        "additionalProperties": false
      }
    }
  },
  "additionalProperties": false
}
```

---

## 2. JSON Schema Definitions

### 2.1 conditions.json

```
{
  "conditions": [
    {
      "name": string (required, non-empty),
      "symptoms": string[] (required, min 1 item, unique within condition),
      "treatments": string[]?,
      "treatments_fr": string[]?,
      "treatments_ar": string[]?,
      "medications": string[]?,
      "medications_fr": string[]?,
      "medications_ar": string[]?,
      "careAdvice": string?,
      "careAdvice_fr": string?,
      "careAdvice_ar": string?
    }
  ]
}
```

**Constraints:**
- `name` must be non-empty after trimming
- `symptoms` must contain at least 1 unique, non-empty string
- Localized fields (`_fr`, `_ar`) are optional; absence triggers fallback to base field
- `additionalProperties: false` — no extra fields allowed

**Current scale:** ~60+ conditions, ~200 unique symptoms.

### 2.2 categories.json

```
{
  "categories": [
    {
      "name": string (required, non-empty),
      "keywords": string[]? (substring matchers, case-insensitive),
      "symptoms": string[]? (explicit list; overrides keywords when non-empty)
    }
  ]
}
```

**Current categories:** Respiratory, Gastrointestinal, Neurological, Musculoskeletal, Dermatological, ENT/Eye, Cardiac/Vascular, Endocrine/Metabolic, Genitourinary, Mental Health, General/Systemic, Sexual Health / STIs, Laboratory Findings.

**Matching logic:**
1. If `symptoms` array is non-empty → use exactly those symptom names
2. Else → for each symptom in vocabulary, check if any keyword is a case-insensitive substring

### 2.3 translations.json

```
{
  "languages": string[] (required, min 1, unique; e.g. ["en","fr","ar"]),
  "ui": [{ "key": string, "en": string, "fr": string?, "ar": string? }],
  "symptoms": [{ "key": string, "fr": string?, "ar": string? }],
  "conditions": [{ "key": string, "fr": string?, "ar": string? }],
  "messages": [{ "key": string, "en": string, "fr": string?, "ar": string? }],
  "categories": [{ "key": string, "fr": string?, "ar": string? }],
  "ui_details": [{ "key": string, "en": string, "fr": string?, "ar": string? }]
}
```

**Notes:**
- `key` values must match canonical names used in code and other data files
- `en` is required for `ui` and `messages` sections; optional for `symptoms`/`conditions`/`categories` where the key itself serves as the English label
- Null or empty localized values trigger fallback

### 2.4 synonyms.json

```
{
  "synonyms": [
    {
      "canonical": string (the known symptom name in conditions.json),
      "aliases": string[] (alternative names / medical terms)
    }
  ]
}
```

**Current scale:** ~47 synonym entries covering lab findings, common symptoms, and medical terminology.

### 2.5 settings.json (auto-generated)

```
{
  "Language": string?,
  "DarkMode": boolean,
  "Model": string?,
  "ThresholdPercent": int,
  "MinMatch": int,
  "TopK": int,
  "ShowOnlyCategory": boolean,
  "SelectedCategory": string?,
  "FilterText": string?,
  "TempC": double?,
  "HeartRate": int?,
  "RespRate": int?,
  "SystolicBP": int?,
  "DiastolicBP": int?,
  "SpO2": int?,
  "WeightKg": double?,
  "AgeYears": int?,
  "PercHemoptysis": boolean?,
  "PercEstrogenUse": boolean?,
  "PercPriorDvtPe": boolean?,
  "PercUnilateralLegSwelling": boolean?,
  "PercRecentSurgeryTrauma": boolean?,
  "LastExportFolder": string?,
  "CategoryWeights": { [category: string]: double }?,
  "NaiveBayesTemperature": double?,
  "LeftPanelCollapsed": boolean?
}
```

---

## 3. Versioning Strategy

### 3.1 Current State

No version field exists in any data file. This creates risk when migrating formats.

### 3.2 Recommended Approach

Add a `"version"` field to the root of each data file:

```json
{
  "version": "1.0",
  "conditions": [...]
}
```

Update schemas to include `version` as an optional string field. Application logic:
1. Read `version` — if absent, assume `"1.0"` (backward compatible)
2. If version is newer than expected, warn user and proceed with best-effort parsing
3. On save, always write current version
4. Migration functions can be chained: `Migrate_1_0_to_1_1()`, etc.

### 3.3 Schema Evolution Rules

| Change Type | Handling |
|---|---|
| New optional field added | Backward-compatible; old files parse fine (field is `null`) |
| Field renamed | Requires migration function; support old name temporarily |
| Field removed | Ignore on read; stop writing on save |
| Array item structure changed | Version bump; migration function converts old items |
| New required field | Must provide default in migration; update schema |

---

## 4. Merge Rules for Wikidata Sync

### 4.1 Merge Algorithm (`MergeConditions`)

```
For each incoming condition:
  1. Skip if name is empty/whitespace
  2. Lookup existing condition by name (case-insensitive)
  3. If not found:
     a. Create new condition with trimmed name
     b. Deduplicate symptoms (case-insensitive), sort alphabetically
     c. Add to database
  4. If found:
     a. Union existing symptoms with incoming symptoms
     b. Deduplicate (case-insensitive), filter empty, sort
     c. If symptom count increased → count as a change
  5. If any changes → RebuildVocabulary()
  6. Return change count
```

### 4.2 Merge Guarantees

| Guarantee | Detail |
|---|---|
| Additive only | Existing conditions are never deleted; existing symptoms are never removed |
| Idempotent | Syncing the same data twice produces 0 changes on the second run |
| Case-insensitive matching | "flu" and "Flu" are treated as the same condition |
| Label normalization | Incoming symptom labels are Title-Cased (`ToTitleCase`) |
| No localization merge | Wikidata sync only provides English labels; localized fields are untouched |

### 4.3 Conflict Resolution

| Conflict | Resolution |
|---|---|
| Name collision with different casing | First-seen casing wins; symptoms are merged |
| Symptom exists in different casing | Case-insensitive dedup preserves first occurrence |
| Wikidata returns condition with 0 symptoms | Skipped (empty symptom list) |

---

## 5. Localization Fallback Rules

### 5.1 UI Labels (`T()` method)

```
1. Look up key in ui[]
2. If found: return field for current language (fr/ar), or en if empty
3. If not in ui[]: look up key in messages[]
4. If found: return field for current language, or en if empty
5. If not found: return the raw key string
```

### 5.2 Symptom / Condition / Category Names

```
1. Look up key in respective array (symptoms[]/conditions[]/categories[])
2. If found and localized value is non-empty: return localized value
3. Else: return the canonical key (which is the English name)
```

### 5.3 Condition Detail Fields (Treatments, Medications, CareAdvice)

```
1. Read language from TranslationService.CurrentLanguage
2. If "fr": use Treatments_Fr ?? Treatments, Medications_Fr ?? Medications, CareAdvice_Fr ?? CareAdvice
3. If "ar": use Treatments_Ar ?? Treatments, Medications_Ar ?? Medications, CareAdvice_Ar ?? CareAdvice
4. Else: use base (English) fields
```

### 5.4 Missing Key Registration

When a lookup succeeds but the localized value is empty, or when a key is not found at all, the key is added to `_missing` set as `"{section}:{key}:{language}"` (e.g., `"sym:Fever:ar"`).

---

## 6. Validation Constraints Summary

| Constraint | Enforcement Point |
|---|---|
| `conditions.json` must have at least one condition | Schema: `conditions` array required |
| Each condition must have a non-empty name | Schema: `name` minLength 1 |
| Each condition must have at least one symptom | Schema: `symptoms` minItems 1 |
| Symptoms within a condition must be unique | Schema: `uniqueItems: true` |
| Categories must have a non-empty name | Schema: `name` minLength 1 |
| Languages array must have at least one entry | Schema: `languages` minItems 1 |
| Language codes must be unique | Schema: `uniqueItems: true` |
| No additional JSON properties allowed | Schema: `additionalProperties: false` on all definitions |
| Settings file can be absent or corrupt | Runtime: deserialization failure → reset to defaults |
| Synonyms file can be absent | Runtime: `SynonymService` stays null; filter uses substring matching only |
