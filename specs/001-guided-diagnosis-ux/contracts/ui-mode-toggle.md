# Contract — UI Mode Toggle

## Inputs

- `AppSettings.UiMode` (nullable) from `data/settings.json`.
- User action: click on `ModeSelector` button, click "Switch mode" button in either shell, or `Ctrl+M` shortcut.
- Active symptom + vitals selection at the moment of switching.

## Preconditions

- `MainForm` has loaded `SettingsService`, `TranslationService`, `CategoriesService`, `SymptomCheckerService`.
- The dataset (`conditions.json`) has loaded successfully.

## Postconditions

- The previous shell is disposed; the new shell is mounted as the sole child of `MainForm`'s content panel.
- `AppSettings.UiMode` is persisted with the new value.
- The user's symptom selection (the canonical names) and vitals values are preserved across the switch.
- The disclaimer label remains visible in both shells.
- Focus moves to the primary control of the new shell (first wizard step button for Patient, filter box for Professional).

## Errors and edge cases

- If switching from Patient (mid-wizard, step 2) to Professional: symptoms selected so far are merged into the Pro `CheckedListBox`.
- If switching from Professional to Patient with > 50 symptoms checked: keep the selection, jump to Step 2 with all of them visible, scrollable.
- If `settings.json` cannot be written: log via `LoggerService`, show a non-blocking toast, keep the in-memory `UiMode` change.

## Default values

- First-ever launch: `UiMode = null` → `ModeSelector` is shown as a modal-style overlay until user picks one.
- Reset Settings clears `UiMode` and `CollapsedSections` → mode chooser is shown again on next launch.
