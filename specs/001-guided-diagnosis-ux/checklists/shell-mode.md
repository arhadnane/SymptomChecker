# Shell / Mode Execution Slice Checklist: Guided Diagnosis Assistant

**Purpose**: Validate that the MainForm shell and mode-switch slice is fully specified for implementation and regression-safe review.
**Created**: 2026-05-23
**Feature**: [spec.md](../spec.md)

**Note**: This checklist targets the highest-priority execution slice covering shell hosting, first-run mode selection, UiMode persistence/restore, cross-mode state preservation, shortcut switching, and permanent disclaimer coverage.

## Requirement Completeness

- [ ] CHK001 Are MainForm shell-host responsibilities explicitly defined, including what becomes the single mounted content region and what remains global chrome across both modes? [Completeness, Spec §FR-001, Spec §FR-002, Plan §Project Structure]
- [ ] CHK002 Are first-run mode-selection requirements complete about when the selector appears, whether it blocks the main workflow, how it is dismissed, and how reset-settings returns the app to that state? [Clarity, Spec §FR-001, Spec §FR-002, Contract: ui-mode-toggle]
- [ ] CHK003 Are UiMode persistence and restore requirements explicit about nullable defaults, backward compatibility for older settings files, and the fallback behavior when `settings.json` cannot be written? [Completeness, Spec §FR-002, Data Model §AppSettings additions, Constitution §III]
- [ ] CHK004 Are cross-mode preservation requirements defined for both symptom selections and vitals, including mid-wizard switches and large existing selections entering Professional mode? [Coverage, Spec §FR-016, Contract: ui-mode-toggle]

## Safety And UI Guardrails

- [ ] CHK005 Are permanent disclaimer requirements unambiguous about which shell-owned surfaces must always render the disclaimer so coverage cannot disappear during host remounts or mode toggles? [Clarity, Spec §FR-005, Spec §FR-012, Constitution §I]
- [ ] CHK006 Are educational-safety wording constraints specific enough for the mode selector, mode-switch affordances, and shortcut-driven transitions to avoid clinical or diagnostic phrasing drift? [Consistency, Spec §FR-001, Safety §Educational Boundary, Constitution §I]
- [ ] CHK007 Does the slice explicitly preserve the existing professional surface by defining what must remain reachable, visible, or unchanged after shell hosting is introduced? [Completeness, Spec §US2, Plan §Summary, Gap]
- [ ] CHK008 Are localization and accessibility requirements defined for shared shell elements such as the mode selector, permanent disclaimer, and shell-level controls, including EN/FR/AR fallback and RTL behavior? [Coverage, Spec §FR-015, Constitution §V]

## Interaction And State Clarity

- [ ] CHK009 Is the `Ctrl+M` toggle requirement precise about scope, focus handling, conflicts with existing shortcuts, and any circumstances where the shortcut must be ignored? [Clarity, Spec §FR-017, Constitution §V]
- [ ] CHK010 Are focus-restoration requirements after a mode switch measurable enough to prevent keyboard-navigation regressions in the current professional UI? [Measurability, Contract: ui-mode-toggle, Constitution §V, Gap]
- [ ] CHK011 Are requirements consistent about what state is preserved only in memory across mode switches versus what is persisted between sessions, especially to avoid writing symptoms or vitals into settings? [Consistency, Spec §FR-002, Spec §FR-016, Constitution §II]
- [ ] CHK012 Are failure-path requirements documented for corrupted, partial, or unwritable UI settings so the shell can recover without blocking the existing desktop workflow? [Exception Flow, Contract: ui-mode-toggle, Constitution §II, Constitution §III]

## Acceptance And Regression Coverage

- [ ] CHK013 Are acceptance criteria defined for this slice independently of later wizard and result-card work so shell hosting and mode switching can be implemented and validated as a standalone increment? [Completeness, Spec §US1, Spec §US2, Tasks §Independent shipping units]
- [ ] CHK014 Are regression requirements measurable for “no professional UI degradation,” including reachability of existing controls, preserved workflow continuity, and unchanged access to advanced features after introducing the host shell? [Acceptance Criteria, Spec §US2, Constitution §V, Gap]
- [ ] CHK015 Does the validation plan specify the narrowest automated and manual checks for settings round-trip, mode restore, cross-mode preservation, `Ctrl+M`, and disclaimer persistence across both modes? [Traceability, Spec §FR-019, Plan §Validation Strategy, Constitution §IV]
