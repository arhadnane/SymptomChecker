# Contract — Results Card Rendering

## Inputs

- `ConditionMatch match` (canonical name, score, match count).
- `Condition condition` (full record from `data/conditions.json`).
- `string activeLanguage` ∈ {`"en"`, `"fr"`, `"ar"`}.
- `bool darkMode`.
- `SymptomCheckerService.DetectionModel currentModel`.

## Output

A `ConditionResultCard` UserControl that renders:

1. Header row: localized condition name (via `TranslationService.Condition`), confidence badge (Low/Moderate/High), matched-symptom count.
2. Section "Home care" using `condition.Treatments_<Lang>` with English fallback.
3. Section "OTC examples" using `condition.Medications_<Lang>` with English fallback.
4. Section "When to seek care" using `condition.CareAdvice_<Lang>` with English fallback.
5. Non-dismissable red "Important — always consult a professional" inset at the bottom.

## Behavior

- If a language-specific field is missing, fall back to the English field with no warning, no empty section.
- If the English field is also missing/empty:
  - Treatments → hide the section (do not render an empty stub).
  - Medications → render the section with the localized "No over-the-counter medication listed — consult a professional" message.
  - CareAdvice → hide the section.
- RTL: when `activeLanguage == "ar"`, the card sets `RightToLeft = Yes` and `RightToLeftLayout = true`, and the layout flows from right to left.
- Dark mode: background uses `Theme.CardBackground`, the "Important" inset uses a fixed red palette tuned for AA contrast in both themes.
- Tooltip on the confidence badge: localized explanation of the bucket (e.g. "Educational confidence band, not a diagnosis").

## Performance

- Single card render ≤ 50 ms on cold draw.
- Cards are recycled in the `FlowLayoutPanel` when the result set changes (no per-keystroke recreation).

## Accessibility

- Each card declares `AccessibleName = condition name` and `AccessibleDescription = "Educational result card with treatments, medications and care advice"`.
- Tab order: header → home care list → meds list → advice → red inset.
- Tooltips honor `ToolTip.IsBalloon = true` for screen readers.
