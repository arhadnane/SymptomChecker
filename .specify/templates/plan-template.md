# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]
**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

[Extract from feature spec: primary requirement + technical approach from research]

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: [e.g., C# / .NET 8 or NEEDS CLARIFICATION]  
**Primary Dependencies**: [e.g., WinForms, NJsonSchema, local services or NEEDS CLARIFICATION]  
**Storage**: [e.g., local JSON files under data/ plus schemas/ or N/A]  
**Testing**: [e.g., xUnit via `dotnet test` or NEEDS CLARIFICATION]  
**Target Platform**: [e.g., Windows desktop (WinForms) or NEEDS CLARIFICATION]
**Project Type**: [e.g., desktop-app or NEEDS CLARIFICATION]  
**Performance Goals**: [e.g., startup < 2 s, filter < 100 ms, matching < 200 ms or NEEDS CLARIFICATION]  
**Constraints**: [e.g., educational-only wording, offline-capable core flow, no PHI, graceful degradation for optional integrations]  
**Scale/Scope**: [e.g., single desktop app, local datasets, tens to hundreds of conditions]

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [ ] Educational safety preserved: user-facing wording stays educational-only,
  disclaimers remain visible, and the feature does not present output as a
  diagnosis.
- [ ] Local-first/privacy boundaries preserved: the core flow remains usable
  offline, and any outbound call is optional, user-initiated, and limited
  to non-sensitive data.
- [ ] Data governance captured: every JSON, export, or settings shape change is
  paired with schema, fallback, and migration notes or an explicit
  compatibility exception.
- [ ] Validation plan defined: focused xUnit coverage is identified for
  matching, triage, parser, or safety logic changes, plus `dotnet build
  --nologo`.
- [ ] UX impact captured: localization, RTL, accessibility, and performance
  implications are identified for every user-visible change.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)
<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature. Delete unused options and expand the chosen structure with
  real paths (e.g., apps/admin, packages/something). The delivered plan must
  not include Option labels.
-->

```text
Program.cs
Models/
Services/
UI/
data/
schemas/
docs/
tests/
└── SymptomChecker.Tests/
```

**Structure Decision**: [Document the selected structure and reference the real
directories captured above]

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
| --------- | ---------- | ----------------------------------- |
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
