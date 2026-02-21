# UX / UI Specification

> **DISCLAIMER:** This application is an educational tool only. All UI elements must reinforce this context. No user should mistake this application for a clinical decision-support system.

---

## 1. Application Window

### 1.1 Shell Structure

| Property | Value |
|---|---|
| Title | "Symptom Checker (Educational)" |
| Minimum size | 900 × 600 px |
| Start position | CenterScreen |
| Resizable | Yes — all panels scale via proportional layout |
| Font | Segoe UI, 10pt base |
| Icon | Custom icon or default WinForms icon |

### 1.2 Top-Level Layout

```
┌─────────────────────────────────────────────────────────┐
│ [Top Bar] Language · Theme · Settings · Sync · Session  │
├────────────────────────┬────────────────────────────────┤
│                        │                                │
│   Left Panel           │   Right Panel                  │
│   • Search / Filter    │   • Results List               │
│   • Category Accordion │   • Details Dialog (modal)     │
│   • Vitals Panel       │   • Export Buttons             │
│   • PERC Panel         │   • Triage Banner              │
│   • Decision Rules     │   • Decision Rule Display      │
│   • Check Button       │                                │
│                        │                                │
├────────────────────────┴────────────────────────────────┤
│ [Status Bar] Disclaimer · Condition count · Version     │
└─────────────────────────────────────────────────────────┘
```

### 1.3 SplitContainer Configuration

| Property | Value |
|---|---|
| Orientation | Vertical (left/right) |
| SplitterDistance | 50% of form width |
| Panel1MinSize | 350 px |
| Panel2MinSize | 350 px |
| IsSplitterFixed | No (user-adjustable) |

---

## 2. Left Panel — Input Controls

### 2.1 Search / Filter Bar

- **Control:** `TextBox` with placeholder text (localized: "Search symptoms…")
- **Behavior:** Filters the visible symptom list in real-time using `SynonymService.MatchSymptomsByQuery()` — matches canonical names and aliases
- **Clear button:** "×" button clears text and restores full list
- **Access key:** Not applicable (always focused first via tab order)

### 2.2 Symptom List (Category Accordion)

Layout uses nested `FlowLayoutPanel` containers:

```
▼ Category A (checkbox header — select/deselect all)
   ☐ Symptom 1
   ☐ Symptom 2
   ☑ Symptom 3
▶ Category B (collapsed)
```

| Behavior | Detail |
|---|---|
| Collapse/expand | Click category header label toggles child visibility |
| Category checkbox | Tri-state: unchecked (none selected), checked (all selected), indeterminate (some selected) |
| Individual checkbox | Standard CheckBox with localized symptom name |
| Scroll | Parent FlowLayoutPanel has `AutoScroll = true` |
| Tooltip | Canonical English name shown on hover when language ≠ EN |
| Category re-ordering | Categories sorted by localized name at load time |

### 2.3 Vitals Input Panel

Collapsible panel with `NumericUpDown` controls:

| Vital | Control | Range | Increment | Decimal Places | Default |
|---|---|---|---|---|---|
| Age | NumericUpDown | 1–120 | 1 | 0 | 30 |
| Temperature °C | NumericUpDown | 30.0–45.0 | 0.1 | 1 | 37.0 |
| SpO₂ % | NumericUpDown | 50–100 | 1 | 0 | 98 |
| Heart Rate bpm | NumericUpDown | 20–250 | 1 | 0 | 72 |
| Resp Rate /min | NumericUpDown | 4–60 | 1 | 0 | 16 |
| Systolic BP mmHg | NumericUpDown | 50–300 | 1 | 0 | 120 |
| Diastolic BP mmHg | NumericUpDown | 20–200 | 1 | 0 | 80 |

All vitals trigger re-evaluation of triage + decision rules on `ValueChanged`.

### 2.4 PERC Panel

Collapsible panel with 4 boolean checkboxes (age, HR, SpO₂ are inferred from vitals):

| Checkbox | Default |
|---|---|
| Hemoptysis | Unchecked |
| Estrogen use | Unchecked |
| Prior DVT / PE | Unchecked |
| Unilateral leg swelling | Unchecked |
| Recent surgery / trauma | Unchecked |

PERC result label shows "PERC negative" or "PERC positive" (localized), updates on any change.

### 2.5 Decision Rules Display

- **Centor Score:** 4 read-only checkboxes (Fever, Tonsils, Nodes, No Cough) + score label
- **McIsaac Score:** Label showing adjusted score and advice band text (localized)
- Panel background color varies by score range (informational shading only)

### 2.6 Check Button

| Property | Value |
|---|---|
| Text | Localized "Check Symptoms" |
| Dock | Bottom of left panel |
| Enabled | Always (returns empty results if no symptoms checked) |
| Action | Calls `SymptomCheckerService.GetMatches()` and populates right panel |

---

## 3. Right Panel — Results Display

### 3.1 Results ListBox (Owner-Draw)

| Property | Value |
|---|---|
| Control | `ListBox` with `DrawMode.OwnerDrawVariable` |
| Item types | `GroupHeader` (non-selectable separator) and `ListItem` (selectable condition) |
| Sorting | Groups sorted by match score descending; items sorted within group |
| Double-click | Opens Details dialog for selected `ListItem` |

**GroupHeader rendering:**
- Bold font, category-colored background stripe
- Displays group name + item count

**ListItem rendering:**
- Regular font, alternating row background (light mode) or uniform dark background (dark mode)
- Columns: Condition name (localized) | Match score (%) | Model indicator icon
- Hover: Highlight background

### 3.2 Details Dialog

Modal dialog shown on double-click of a result item:

```
┌──────────────────────────────────────┐
│ Condition Name (Localized)           │
├──────────────────────────────────────┤
│ Match Score: 85% (Jaccard)           │
│                                      │
│ Matched Symptoms:                    │
│  • Fever ✓                           │
│  • Cough ✓                           │
│  • Headache ✗                        │
│                                      │
│ Description:                         │
│ [Localized description text]         │
│                                      │
│ Treatments (Educational):            │
│  • Rest                              │
│  • Fluids                            │
│                                      │
│ ⚠️ Educational only, not medical     │
│    advice.                           │
├──────────────────────────────────────┤
│                        [ Close ]     │
└──────────────────────────────────────┘
```

### 3.3 Triage Banner

| State | Behavior |
|---|---|
| No red flags | Banner is hidden (height = 0) |
| Red flags detected | Banner appears at top of right panel with colored background |

Banner colors by highest severity present:

| Severity | Background | Foreground |
|---|---|---|
| 1 (critical) | `#D32F2F` (red) | White |
| 2 (high) | `#F57C00` (orange) | White |
| 3 (moderate) | `#FBC02D` (amber) | Black |

### 3.4 Export Buttons

Row of buttons below results list:

| Button | Action | Format |
|---|---|---|
| Export CSV | Save results to `.csv` file | Comma-separated, UTF-8 BOM |
| Export Markdown | Save results to `.md` file | Markdown table with header |
| Export HTML | Save results to `.html` file | Styled HTML with inline CSS |

All exports include:
- Timestamp
- Language code
- Model name
- Educational disclaimer
- Session vitals (if entered)

---

## 4. Top Bar Controls

### 4.1 Language Selector

| Property | Value |
|---|---|
| Control | `ComboBox` |
| Items | English (en), Français (fr), العربية (ar) |
| Default | en |
| On change | Reloads all UI strings, symptom/condition names, category names; preserves checked symptom state |

### 4.2 Theme Toggle

| Property | Value |
|---|---|
| Control | `Button` or `CheckBox` styled as toggle |
| States | Light mode (default), Dark mode |
| On change | Recursively applies color scheme to all controls |

### 4.3 Model Selector

| Property | Value |
|---|---|
| Control | `ComboBox` |
| Items | Jaccard, Cosine, Naive Bayes |
| Default | Jaccard |
| On change | Re-runs matching if results are currently displayed |

### 4.4 Session Controls

| Button | Action |
|---|---|
| Save Session | Serializes current state (symptoms, vitals, PERC flags, language, model, results) to JSON file |
| Load Session | Restores saved state from JSON file; validates structure before applying |
| Reset | Clears all inputs to defaults; clears results |

### 4.5 Wikidata Sync Button

| Property | Value |
|---|---|
| Label | "Sync from Wikidata" (localized) |
| Action | Runs `WikidataImporter.ImportFromWikidataAsync()` |
| During sync | Button disabled, progress status shown |
| On success | Toast/status message with count of new conditions added |
| On failure | Error message in status bar; no data lost (additive merge only) |

---

## 5. Theming

### 5.1 Color Palette

| Token | Light Mode | Dark Mode |
|---|---|---|
| Background | `#FFFFFF` | `#1E1E1E` |
| Surface | `#F5F5F5` | `#2D2D2D` |
| Text primary | `#212121` | `#E0E0E0` |
| Text secondary | `#757575` | `#9E9E9E` |
| Accent | `#1976D2` | `#64B5F6` |
| Error/Warning | `#D32F2F` | `#EF5350` |
| Border | `#E0E0E0` | `#424242` |
| Selection | `#BBDEFB` | `#1565C0` |

### 5.2 Theme Application

```
ApplyTheme(Control root, bool isDark)
  foreach control in root.Controls (recursive):
    set BackColor, ForeColor based on control type
    special handling for: ListBox, ComboBox, NumericUpDown, TextBox, Button, Label, CheckBox, GroupBox
```

Theme changes apply immediately without restart.

---

## 6. RTL Support

### 6.1 RTL Activation

When language = `ar`:
- `Form.RightToLeft = RightToLeft.Yes`
- `Form.RightToLeftLayout = true`
- All controls inherit RTL layout

### 6.2 RTL-Specific Adjustments

| Element | LTR (en, fr) | RTL (ar) |
|---|---|---|
| Text alignment | Left | Right |
| Split panel order | Input left, Results right | Input right, Results left |
| Bullet lists | ` • text` | `text  •` |
| Status bar | Left-aligned | Right-aligned |
| Scroll bars | Right side | Left side |
| Triage banner bullets | ` • Flag` | `Flag  •` |

### 6.3 Font Consideration

Arabic text uses the same Segoe UI font family which has Arabic glyph support on Windows 10+.

---

## 7. Accessibility

### 7.1 Keyboard Navigation

| Key | Action |
|---|---|
| Tab / Shift+Tab | Navigate between controls in tab order |
| Space | Toggle checkbox |
| Enter | Activate focused button / open details |
| Escape | Close modal dialog |
| Arrow keys | Navigate within lists |

### 7.2 Screen Reader Compatibility

- All controls have meaningful `AccessibleName` and `AccessibleDescription` properties
- Group headers in ListBox announce group name and item count
- Triage banner announces severity level and flag descriptions
- Status bar updates announce via `AccessibleRole`

### 7.3 Color Contrast

- All text meets WCAG 2.1 AA contrast ratio (4.5:1 for normal text, 3:1 for large text)
- Severity colors are paired with text labels — color is never the sole indicator
- Dark mode maintains equivalent or better contrast ratios

### 7.4 Focus Indicators

- All focusable controls show visible focus rectangles
- Custom-drawn ListBox items render focus rectangle via `DrawFocusRectangle()`

---

## 8. Error and Warning Presentation

### 8.1 Schema Validation Errors (Startup)

Displayed as a `MessageBox` with warning icon. Lists each invalid file and the validation error. Application continues with valid files; invalid files are skipped with logged warnings.

### 8.2 Runtime Errors

| Error Type | Presentation |
|---|---|
| File save failure | MessageBox (Error icon) with file path |
| Session load failure | MessageBox (Warning icon) with parse error |
| Wikidata sync failure | Status bar message (no modal) |
| Export failure | MessageBox (Error icon) |

### 8.3 Informational Feedback

| Event | Feedback |
|---|---|
| Successful export | Status bar message: "Exported to [path]" (auto-clears after 5s) |
| Successful sync | Status bar message: "Added N conditions from Wikidata" |
| No results | Results list shows single item: "No matching conditions found" |
| Zero symptoms checked | Results cleared; no error shown |
