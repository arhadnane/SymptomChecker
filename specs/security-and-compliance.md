# Security and Compliance Specification

> **DISCLAIMER:** This application is an educational desktop tool. It is not a medical device, does not process Protected Health Information (PHI), and must never be used for clinical decision-making.

---

## 1. Classification

### 1.1 Regulatory Status

| Dimension | Status |
|---|---|
| Medical device | **No** — educational tool only |
| FDA / CE classification | Not applicable |
| HIPAA applicability | Not applicable (no PHI processed) |
| GDPR applicability | Minimal — no personal data collected or transmitted |
| SOC 2 applicability | Not applicable (desktop-only, no cloud services) |

### 1.2 Justification for Non-Regulated Status

The application:
- Does **not** accept free-text patient descriptions
- Does **not** store patient identifiers (name, DOB, address, insurance, etc.)
- Does **not** render diagnoses — only educational condition matches
- Uses hedging language throughout ("consider", "may suggest", "educational only")
- Displays prominent disclaimers on every screen and export
- Does **not** transmit any data to external servers (except optional Wikidata SPARQL queries for disease data enrichment)

---

## 2. Data Privacy

### 2.1 No-PHI Guarantee

| Data Type | Collected? | Stored? | Transmitted? |
|---|---|---|---|
| Patient name | No | No | No |
| Date of birth | No | No | No |
| Address / contact info | No | No | No |
| Insurance / billing | No | No | No |
| Medical record number | No | No | No |
| Symptom selections | In-memory only | Session file (user-initiated, local) | No |
| Vitals values | In-memory only | Session file (user-initiated, local) | No |
| Age (numeric only) | In-memory only | Session/settings (local) | No |
| PERC flags | In-memory only | Session file (local) | No |
| Results / matches | In-memory only | Export file (user-initiated, local) | No |

### 2.2 Session Files

- **Format:** JSON
- **Location:** User-chosen path via `SaveFileDialog`
- **Content:** Symptom selections, vitals, PERC flags, language, model, results — no identifying data
- **Encryption:** None (plain text) — acceptable because no PHI is present
- **Retention:** User-managed; application does not auto-save sessions

### 2.3 Settings File

- **Location:** `settings.json` in application directory
- **Content:** UI preferences (language, theme, model, layout), vitals defaults, category weights, NB temperature
- **Sensitivity:** None — contains no patient or user-identifying data

### 2.4 Log Files

- **Location:** `logs/` subdirectory of application directory
- **Content:** Timestamps, operation names, error messages, file paths
- **No PHI logged:** Symptom names may appear in debug logs but are medical terms, not patient data
- **Rotation:** 512 KB max per file, 5 files max — oldest auto-deleted
- **Access:** Local filesystem only; no remote log shipping

---

## 3. Network Security

### 3.1 Network Boundary

| Connection | Direction | Destination | Data Sent | Optional? |
|---|---|---|---|---|
| Wikidata SPARQL | Outbound HTTPS | `query.wikidata.org` | SPARQL query (disease/symptom request) | Yes — user-initiated only |

### 3.2 No Inbound Connections

The application:
- Does not open any listening ports
- Does not register any URL handlers or protocol handlers
- Does not expose any API endpoints

### 3.3 Wikidata Request Details

```
POST https://query.wikidata.org/sparql
Content-Type: application/x-www-form-urlencoded
Accept: application/sparql-results+json
User-Agent: SymptomCheckerEducational/1.0

Body: query=SELECT ... WHERE { ... } LIMIT 200
```

- No API keys or authentication tokens required
- No patient data included in any request
- Query returns only disease names and associated symptom names from the public Wikidata knowledge graph
- Rate limit: Wikidata imposes its own rate limits; the app makes at most one request per user-initiated sync
- Timeout: `HttpClient` with configurable timeout (default: 30s)

### 3.4 Offline Operation

The application is fully functional offline. Wikidata sync is optional enrichment; failure to connect results in a status message and no data loss.

---

## 4. Data Integrity

### 4.1 Schema Validation

At startup, the following data files are validated against their JSON schemas:

| Data File | Schema File | Validation Library |
|---|---|---|
| `conditions.json` | `conditions.schema.json` | NJsonSchema |
| `categories.json` | `categories.schema.json` | NJsonSchema |
| `translations.json` | `translations.schema.json` | NJsonSchema |

**On validation failure:**
- Warning logged
- MessageBox displayed to user
- Application continues with valid data files; invalid files are skipped
- No data is silently corrupted

### 4.2 Wikidata Merge Integrity

- Merge is **additive only** — existing conditions are never deleted or modified
- New conditions are added with Title Case normalization
- Duplicate detection is by condition name (case-insensitive)
- Merged file is saved atomically (write-then-rename pattern recommended; current implementation writes directly)

### 4.3 No Code Injection Vectors

| Vector | Mitigation |
|---|---|
| SQL injection | No database — JSON file storage only |
| XSS | No web content rendered; HTML export is pre-formatted with escaped values |
| Command injection | No shell commands executed; no `Process.Start()` with user input |
| Deserialization attacks | `System.Text.Json` with strongly-typed POCOs — no polymorphic deserialization |
| Path traversal | File dialogs constrain paths; no user-supplied path strings parsed |

---

## 5. Application Security

### 5.1 Dependencies

| Dependency | Version | Risk Assessment |
|---|---|---|
| .NET 8 SDK | 8.x | Maintained by Microsoft; regular security patches |
| NJsonSchema | 11.0.0 | Widely used; no known critical CVEs at time of spec |
| System.Text.Json | Built-in | Part of .NET runtime; patched with runtime updates |

### 5.2 Dependency Update Policy

- Monitor NuGet advisories for NJsonSchema
- Update .NET SDK with each LTS patch release
- No transitive dependency on Newtonsoft.Json (eliminated attack surface)

### 5.3 Build Security

| Measure | Status |
|---|---|
| Signed assemblies | Not currently implemented (not required for educational tool) |
| Deterministic builds | Enabled via `<Deterministic>true</Deterministic>` (default in .NET 8) |
| Source Link | Configured (`SymptomChecker.sourcelink.json` present) |

---

## 6. Compliance Requirements

### 6.1 Educational Disclaimer Obligations

Every user-facing surface must display or reference the educational disclaimer:

| Surface | Requirement |
|---|---|
| Main form | Persistent footer: "⚠️ Educational only. Not medical advice." |
| Results panel | Disclaimer visible without scrolling when results are displayed |
| Triage banner | Disclaimer appended to every triage warning |
| Details dialog | "Educational" label on treatments and care advice |
| Export files (CSV, MD, HTML) | Disclaimer in header and/or footer |
| About / Help | Full disclaimer text with scope and limitations |

### 6.2 Language Requirements

All disclaimers must be:
- Available in all supported languages (EN, FR, AR)
- Displayed in the currently selected language
- Never truncated or hidden behind scroll

### 6.3 No-Diagnosis Guarantee

The application must never:
- Use the word "diagnosis" or "diagnose" in any user-facing text (use "match", "suggestion", "educational result")
- Present results with > 100% confidence
- Recommend specific medications or dosages without "educational only" qualifier
- Suggest urgency without the triage disclaimer

### 6.4 Audit Trail

| Audit Need | Current Support |
|---|---|
| Who accessed the app | Not tracked (single-user desktop app, no auth) |
| What data was viewed | Not tracked (no PHI to audit) |
| Data modifications | Log file records Wikidata sync additions and settings changes |
| Export history | Log file records export file paths and timestamps |

---

## 7. Threat Model Summary

| Threat | Likelihood | Impact | Mitigation |
|---|---|---|---|
| User mistakes results for medical advice | Medium | High | Pervasive disclaimers; hedging language; no "diagnosis" terminology |
| Corrupted data files | Low | Medium | Schema validation at startup; additive-only merges |
| Malicious data file replacement | Low | Low | App validates schema; no code execution from data |
| Network interception (Wikidata) | Very Low | Very Low | HTTPS only; no sensitive data transmitted |
| Dependency vulnerability | Low | Medium | Single NuGet dependency; regular updates recommended |
| Local file access by other users | Low | Very Low | No PHI stored; standard OS file permissions apply |

---

## 8. Recommendations

### 8.1 Short-Term

1. Add a `synonyms.schema.json` for completeness of validation coverage
2. Ensure HTML exports escape all condition/symptom names to prevent any injection if opened in browser
3. Add file integrity check (hash) for data files at startup (optional hardening)

### 8.2 Long-Term

1. If the app ever collects identifiable data (e.g., export with patient context), implement encryption at rest
2. If network features expand beyond Wikidata, implement certificate pinning
3. Consider code signing for distribution builds to prevent tampering
