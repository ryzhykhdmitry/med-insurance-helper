# Tasks: Simplify Chat Flow (002-simplify-chat-flow)

**Input**: `spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/api.md`, `quickstart.md`

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add scaffolding and register services required by the unified chat surface.

- [X] T001 [P] Create controller scaffold `src/backend/MedInsuranceHelper.Api/Controllers/ChatController.cs`
- [X] T002 [P] Add DTOs `src/backend/MedInsuranceHelper.Api/Models/ChatRequestDto.cs` and `src/backend/MedInsuranceHelper.Api/Models/ChatResponseDto.cs`
- [X] T003 [P] Add `ResponseArtifact` model `src/backend/MedInsuranceHelper.Api/Models/ResponseArtifact.cs`
- [X] T004 [P] Register chat services and routing in `src/backend/MedInsuranceHelper.Api/Program.cs`
- [X] T005 [P] Create `IntentDetectionService` scaffold at `src/backend/MedInsuranceHelper.Api/Services/IntentDetectionService.cs`
- [X] T006 [P] Create `ChatOrchestrationService` scaffold at `src/backend/MedInsuranceHelper.Api/Services/ChatOrchestrationService.cs`
- [X] T007 [P] Add client-side helper `src/frontend/src/app/services/chat.service.ts` to call `/api/chat`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Implement core logic and integrations that user stories depend on.

- [X] T008 Implement `ResponseArtifact` serialization and validation in `src/backend/MedInsuranceHelper.Api/Models/ResponseArtifact.cs` (depends on T003)
- [X] T009 Update `src/backend/MedInsuranceHelper.Api/Services/SessionService.cs` to persist and load `ConversationSession` with message list (depends on existing model files)
- [X] T010 Integrate `IntentDetectionService` with `src/backend/MedInsuranceHelper.Api/Services/LLMPipelineService.cs` or `FoundryClient.cs` to perform intent detection (depends on T005)
- [X] T011 Ensure `OfferRepository.cs` plan normalization resolves `InsurancePlanReference` and returns normalized `id`s (`src/backend/MedInsuranceHelper.Api/Services/OfferRepository.cs`)
- [X] T012 Implement error handling and user-friendly error responses for chat flows in `src/backend/MedInsuranceHelper.Api/Middleware/ErrorHandlingMiddleware.cs`
- [X] T013 Add lightweight feature-flag or configuration toggle for Foundry orchestration behavior (e.g., single-plan compare policy) in `src/backend/MedInsuranceHelper.Api/Configuration/AppSettings.cs`

**Checkpoint**: Foundational services implemented and registered — user story work may begin.

---

## Phase 3: User Story 1 - Ask Anything in One Chat (Priority: P1) 🎯 MVP

**Goal**: Accept natural language in one chat, detect intents, and return combined response sections (comparison, recommendation, answer) from the same chat thread.

**Independent Test**: POST a variety of messages to `/api/chat` and confirm `responseArtifact.sections` contains appropriate typed sections.

### Tests
- [ ] T014 [P] [US1] Contract test for `/api/chat` in `src/backend/MedInsuranceHelper.Api.Tests/Integration/ChatContractTests.cs`
- [ ] T015 [US1] Integration test for common user journeys in `src/backend/MedInsuranceHelper.Api.Tests/Integration/ChatIntegrationTests.cs`

### Implementation
- [X] T016 [US1] Implement `ChatController.cs` logic in `src/backend/MedInsuranceHelper.Api/Controllers/ChatController.cs` (depends on T001, T008, T009)
- [X] T017 [P] [US1] Implement `ChatOrchestrationService` to: parse message, call `IntentDetectionService`, orchestrate retrieval/comparison/recommendation pipelines and build `ResponseArtifact` in `src/backend/MedInsuranceHelper.Api/Services/ChatOrchestrationService.cs` (depends on T006, T010, T011)
- [X] T018 [US1] Integrate `ComparisonService` (`src/backend/MedInsuranceHelper.Api/Services/ComparisonService.cs`) to produce comparison `payload` for ResponseArtifact (depends on T011)
- [X] T019 [US1] Ensure session messages appended to `ConversationSession` during flow in `src/backend/MedInsuranceHelper.Api/Services/SessionService.cs` (depends on T009)
- [X] T020 [P] [US1] Update frontend `chat.component.ts` and `chat.component.html` to call `/api/chat` and render labeled sections (`src/frontend/src/app/chat/chat.component.ts`, `src/frontend/src/app/chat/chat.component.html`)
- [X] T021 [US1] Add client-side parsing/rendering for `responseArtifact.sections` in `src/frontend/src/app/services/chat.service.ts` and UI components

**Checkpoint**: US1 should be testable end-to-end with one POST to `/api/chat` returning structured sections.

---

## Phase 4: User Story 2 - Continue Context Across Requests (Priority: P2)

**Goal**: Preserve conversation context across turns and handle clarification when input is missing.

**Independent Test**: Start a session, do a compare, then ask follow-up; ensure follow-up uses earlier context.

- [X] T022 [US2] Implement session continuation: ensure `sessionId` passed back and loaded by `ChatController` (src/backend/MedInsuranceHelper.Api/Controllers/ChatController.cs)
- [X] T023 [US2] Implement clarification flow when plan resolution fails: `ChatOrchestrationService` should emit a `clarification` section (src/backend/MedInsuranceHelper.Api/Services/ChatOrchestrationService.cs)
- [ ] T024 [US2] Add integration tests for follow-up context in `src/backend/MedInsuranceHelper.Api.Tests/Integration/ChatFollowupTests.cs`

---

## Phase 5: User Story 3 - Streamlined Interface and Behavior (Priority: P3)

**Goal**: Remove separate compare/suggest surfaces and ensure equivalent outcomes remain available from unified chat.

- [X] T025 [US3] Remove or mark deprecated `src/backend/MedInsuranceHelper.Api/Controllers/CompareController.cs` and `RecommendController.cs` and remove their public routes (hard cutover) (ensure backups or PR history)
- [X] T026 [US3] Update frontend navigation and remove separate compare/suggest UI (`src/frontend/src/app/compare/`, `src/frontend/src/app/recommend/`)
- [X] T027 [US3] Update public docs and API usage guidance to reference `/api/chat` only (`README.md`, docs/, `specs/002-simplify-chat-flow/quickstart.md`)

---

## Phase N: Polish & Cross-Cutting Concerns

- [ ] T028 [P] Documentation updates in `docs/` and `specs/002-simplify-chat-flow/quickstart.md`
- [ ] T029 Code cleanup and refactoring in `src/backend/MedInsuranceHelper.Api/` and `src/frontend/`
- [ ] T030 Performance and telemetry: add metrics for response latency and intent detection accuracy (instrument `LLMPipelineService.cs` and `ChatOrchestrationService.cs`)
- [ ] T031 Security review and input validation hardening in `src/backend/MedInsuranceHelper.Api/Controllers/ChatController.cs`
- [ ] T032 [P] Run quickstart validation steps in `specs/002-simplify-chat-flow/quickstart.md`

---

## Dependencies & Execution Order

- Complete Phase 1 (T001..T007) first — these tasks are largely parallelizable (marked `[P]`).
- Complete Phase 2 (T008..T013) before implementing user stories.
- Implement User Story 1 (T014..T021) as the MVP deliverable (P1). User Stories 2 and 3 follow.

## Parallel Opportunities

- Many setup and foundational tasks are parallelizable (marked `[P]`).
- Frontend and backend work can proceed in parallel once foundational services are stable.

## Parallel Execution Example

1. Developer A: Implement `IntentDetectionService` and `ChatOrchestrationService` (T005, T006, T017)
2. Developer B: Implement `ResponseArtifact` model and session persistence (T003, T008, T009)
3. Developer C: Add frontend `chat.service.ts` and update `chat.component` (T007, T020)

## Implementation Strategy

- MVP First: Deliver User Story 1 (T014..T021) after Phases 1–2. Validate end-to-end and iterate on policies (Foundry) before removing specialized endpoints.
- Incremental: After US1 validated, implement US2 and US3 in priority order and merge changes behind feature flags if desired.

## Estimated Task Counts & Summary

- Total tasks: 32
- P1 (US1) task count: 8 (including tests)
- Parallel opportunities: many foundational and setup tasks marked `[P]`
