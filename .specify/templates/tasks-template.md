---

description: "Task list template for feature implementation"
---

# Tasks: [FEATURE NAME]

**Input**: Design documents from `/specs/[###-feature-name]/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Include test tasks whenever the specification or constitution flags
matching logic, triage, AI parsing, safety messaging, data contracts,
localization, or other regression-prone behavior. Tests may be omitted only
when the plan explains why the change is purely presentational or otherwise low
risk.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Application code**: `Program.cs`, `Models/`, `Services/`, `UI/`
- **Local data and schemas**: `data/`, `schemas/`
- **Automated tests**: `tests/SymptomChecker.Tests/`
- **Feature artifacts**: `specs/[###-feature-name]/`

<!-- 
  ============================================================================
  IMPORTANT: The tasks below are SAMPLE TASKS for illustration purposes only.
  
  The /speckit.tasks command MUST replace these with actual tasks based on:
  - User stories from spec.md (with their priorities P1, P2, P3...)
  - Feature requirements from plan.md
  - Entities from data-model.md
  - Endpoints from contracts/
  
  Tasks MUST be organized by user story so each story can be:
  - Implemented independently
  - Tested independently
  - Delivered as an MVP increment
  
  DO NOT keep these sample tasks in the generated tasks.md file.
  ============================================================================
-->

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm feature scaffolding and shared project updates

- [ ] T001 Identify affected files and feature structure per implementation plan
- [ ] T002 Update project or package configuration if the feature needs it
- [ ] T003 [P] Create any new scaffolding under Models/, Services/, UI/, or
  tests/

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

Examples of foundational tasks (adjust based on your project):

- [ ] T004 Update affected models, data contracts, or schemas in Models/,
  data/, and schemas/
- [ ] T005 [P] Extend shared services or abstractions in Services/
- [ ] T006 [P] Wire shared UI, localization, accessibility, or disclaimer
  support in UI/
- [ ] T007 Define logging, fallback, and error handling for changed workflows
- [ ] T008 Capture validation commands (`dotnet build --nologo`, targeted
  `dotnet test`) in the plan or task notes

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - [Title] (Priority: P1) 🎯 MVP

**Goal**: [Brief description of what this story delivers]

**Independent Test**: [How to verify this story works on its own]

### Tests for User Story 1 (include when constitution or spec requires) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T010 [P] [US1] Add or update a focused regression test in
  tests/SymptomChecker.Tests/[Feature]Tests.cs
- [ ] T011 [P] [US1] Add coverage for data fallback, parsing, localization, or
  triage behavior when the story changes those surfaces

### Implementation for User Story 1

- [ ] T012 [P] [US1] Update model or DTO types in Models/[Entity].cs
- [ ] T013 [P] [US1] Implement service changes in Services/[Service].cs
- [ ] T014 [US1] Implement the user-facing workflow in UI/[File].cs or
  Program.cs
- [ ] T015 [US1] Update data, schema, or translation assets in data/ and
  schemas/ when required
- [ ] T016 [US1] Add validation, disclaimer, accessibility, and localization
  wiring for the story
- [ ] T017 [US1] Run the targeted tests and build validation for user story 1

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Story 2 - [Title] (Priority: P2)

**Goal**: [Brief description of what this story delivers]

**Independent Test**: [How to verify this story works on its own]

### Tests for User Story 2 (include when constitution or spec requires) ⚠️

- [ ] T018 [P] [US2] Add or update a focused regression test in
  tests/SymptomChecker.Tests/[Feature]Tests.cs
- [ ] T019 [P] [US2] Add coverage for the affected parser, fallback, or
  user-flow behavior

### Implementation for User Story 2

- [ ] T020 [P] [US2] Update model or data definitions in Models/ or data/
- [ ] T021 [US2] Implement service changes in Services/[Service].cs
- [ ] T022 [US2] Implement the user-facing workflow in UI/[File].cs
- [ ] T023 [US2] Integrate with User Story 1 components and shared validation
  paths as needed

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: User Story 3 - [Title] (Priority: P3)

**Goal**: [Brief description of what this story delivers]

**Independent Test**: [How to verify this story works on its own]

### Tests for User Story 3 (include when constitution or spec requires) ⚠️

- [ ] T024 [P] [US3] Add or update a focused regression test in
  tests/SymptomChecker.Tests/[Feature]Tests.cs
- [ ] T025 [P] [US3] Add coverage for the affected user journey, fallback, or
  localization behavior

### Implementation for User Story 3

- [ ] T026 [P] [US3] Update shared models, data, or schema assets
- [ ] T027 [US3] Implement service changes in Services/[Service].cs
- [ ] T028 [US3] Implement the user-facing workflow in UI/[File].cs

**Checkpoint**: All user stories should now be independently functional

---

[Add more user story phases as needed, following the same pattern]

---

## Phase N: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] TXXX [P] Documentation updates in docs/ and affected specs/
- [ ] TXXX Code cleanup and refactoring
- [ ] TXXX Performance optimization across all stories
- [ ] TXXX [P] Additional xUnit coverage in tests/SymptomChecker.Tests/
- [ ] TXXX Validate localization, disclaimers, and schema/fallback behavior
- [ ] TXXX Run `dotnet build --nologo` and any story-specific validation steps

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - User stories can then proceed in parallel (if staffed)
  - Or sequentially in priority order (P1 → P2 → P3)
- **Polish (Final Phase)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - May integrate with US1 but should be independently testable
- **User Story 3 (P3)**: Can start after Foundational (Phase 2) - May integrate with US1/US2 but should be independently testable

### Within Each User Story

- Required tests MUST be written and FAIL before implementation
- Models and data contracts before services
- Services before UI wiring
- Core implementation before integration
- Story complete before moving to next priority

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel
- All Foundational tasks marked [P] can run in parallel (within Phase 2)
- Once Foundational phase completes, all user stories can start in parallel (if team capacity allows)
- All tests for a user story marked [P] can run in parallel
- Models within a story marked [P] can run in parallel
- Different user stories can be worked on in parallel by different team members

---

## Parallel Example: User Story 1

```text
# Launch all tests for User Story 1 together:
Task: "Add or update a focused regression test in tests/SymptomChecker.Tests/[Feature]Tests.cs"
Task: "Add coverage for data fallback, parsing, localization, or triage behavior"

# Launch independent implementation tasks together:
Task: "Update model or DTO types in Models/[Entity].cs"
Task: "Implement service changes in Services/[Service].cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Test User Story 1 independently
5. Deploy/demo if ready

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → Deploy/Demo (MVP!)
3. Add User Story 2 → Test independently → Deploy/Demo
4. Add User Story 3 → Test independently → Deploy/Demo
5. Each story adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1
   - Developer B: User Story 2
   - Developer C: User Story 3
3. Stories complete and integrate independently

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Verify tests fail before implementing
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Avoid: vague tasks, same file conflicts, cross-story dependencies that break independence
