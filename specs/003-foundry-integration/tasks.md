# Tasks: Intelligent Document Processing and Retrieval

**Feature**: 003-foundry-integration | **Branch**: `003-foundry-integration`

**Scope**: Minimal implementation for RAG-powered chat. Focus on User Stories 1-3 only (Infrastructure, Document Processing, Chat).

**Key Principle**: Maximum use of Azure-managed services. Backend only handles session management and forwarding to Foundry RAG endpoint.

---

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: User story (US1 = Infrastructure, US2 = Document Processing, US3 = Chat)
- Paths relative to repository root: `c:\Dev\Sandbox\med-insurance-helper\`

---

## Phase 1: Azure Infrastructure Setup (User Story 1 - P1) 🎯

**Goal**: Provision Azure resources required for document processing and RAG

**Independent Test**: Run scripts and verify resources exist in Azure Portal

### Infrastructure Provisioning Scripts

- [ ] T001 [P] [US1] Create `scripts/setup-blob-storage.ps1` to provision Azure Blob Storage container for PDFs with idempotent resource checks (skip if container already exists)
- [ ] T002 [P] [US1] Create `scripts/setup-search-indexer.ps1` to configure Azure AI Search with indexer, skillset, and vector index with idempotent resource checks (skip if resources already exist)
- [ ] T002a [US1] Verify Azure AI Search indexer automatic retry behavior and execution history tracking in Azure Portal after T002 completes
- [ ] T003 [P] [US1] Create `scripts/setup-foundry-project.ps1` to configure Azure AI Foundry RAG endpoint connected to search index with idempotent resource checks (skip if project already configured)
- [ ] T004 [US1] Create `scripts/README.md` documenting script execution order and required Azure CLI commands

**Checkpoint**: Azure resources provisioned and ready for document uploads

---

## Phase 2: Backend Models & Configuration (Foundational)

**Goal**: Core data models and configuration for chat functionality

**⚠️ CRITICAL**: Must complete before implementing chat endpoints

### Data Models

- [ ] T005 [P] Add `ExpiresAt` property to existing `src/backend/MedInsuranceHelper.Api/Models/ConversationSession.cs`
- [ ] T006 [P] Add optional `SourceCitations` property (List<SourceCitation>) to existing `src/backend/MedInsuranceHelper.Api/Models/Message.cs`
- [ ] T007 [P] Create `src/backend/MedInsuranceHelper.Api/Models/SourceCitation.cs` DTO for Foundry response citations (DocumentId, Content, FileName, PageNumber, ChunkIndex, Score)

### Configuration

- [ ] T008 [US1] Add Azure Foundry settings to `src/backend/MedInsuranceHelper.Api/Configuration/AppSettings.cs` (ProjectEndpoint, ApiKey, RagDeploymentName)
- [ ] T009 Add `Azure.AI.Projects` NuGet package to `src/backend/MedInsuranceHelper.Api/MedInsuranceHelper.Api.csproj`

**Checkpoint**: Models and configuration ready for service implementation

---

## Phase 3: Session Management (Foundational)

**Goal**: Extend existing SessionService with expiration logic and background cleanup

### Session Service Extensions

- [ ] T010 Extend existing `src/backend/MedInsuranceHelper.Api/Services/SessionService.cs` to calculate `ExpiresAt = LastActiveAt + 30 minutes`
- [ ] T011 Add `RemoveExpiredSessions()` method to `SessionService` to remove sessions where `ExpiresAt < UtcNow`
- [ ] T012 Update session retrieval to check expiration before returning (return null if expired)

### Background Cleanup Worker

- [ ] T013 Create `src/backend/MedInsuranceHelper.Api/Workers/SessionCleanupWorker.cs` as BackgroundService that runs every 10 minutes
- [ ] T014 Register `SessionCleanupWorker` as hosted service in `src/backend/MedInsuranceHelper.Api/Program.cs`

**Checkpoint**: Session management with automatic expiration working

---

## Phase 4: Foundry Integration Service (User Story 3 - P3) 🎯

**Goal**: Service to forward chat requests to Azure AI Foundry RAG endpoint

**Independent Test**: Mock Foundry responses and verify request/response mapping

### Foundry RAG Service

- [ ] T015 Create `src/backend/MedInsuranceHelper.Api/Services/FoundryRagService.cs` with `ChatAsync()` method
- [ ] T016 Implement Foundry RAG request construction (user message, conversation history, retrieval params)
- [ ] T017 Implement Foundry RAG response parsing (answer text, source citations, relevance scores)
- [ ] T018 Add error handling for Foundry service unavailability (return 503 on timeout/failure)
- [ ] T019 Register `FoundryRagService` in DI container in `src/backend/MedInsuranceHelper.Api/Program.cs`

**Checkpoint**: Foundry integration service ready to be called from controllers

---

## Phase 5: Chat API Endpoint (User Story 3 - P3) 🎯

**Goal**: REST API for chat interactions with session management

**Independent Test**: Send chat request and verify response with citations

### Chat Controller Implementation

- [ ] T020 Extend existing `src/backend/MedInsuranceHelper.Api/Controllers/ChatController.cs` with session-aware chat endpoint
- [ ] T021 Implement POST `/api/chat` endpoint logic:
  - Validate message (non-empty, max 2000 chars)
  - Get or create session via `SessionService`
  - Forward to `FoundryRagService.ChatAsync()`
  - Append user message and assistant response to session
  - Return response with sessionId, message, citations, expiresAt

### Session Management Endpoints

- [ ] T022 [P] Implement GET `/api/sessions/{id}` endpoint to check session status (metadata only, no expiration extension)
- [ ] T023 [P] Implement DELETE `/api/sessions/{id}` endpoint to explicitly end session

**Checkpoint**: Chat API fully functional with session management

---

## Phase 6: Testing & Validation

**Goal**: Verify end-to-end functionality with real Azure services

### Manual Testing (per quickstart.md)

- [ ] T024 Run infrastructure setup scripts and verify resources in Azure Portal
- [ ] T025 Upload sample PDF to blob storage via Azure CLI (note: sample files in docs/samples/ are .txt format - either convert to PDF first or use actual PDF files if available)
- [ ] T026 Wait for Azure AI Search indexer to process document (check indexer execution history in Portal for successful completion and retry attempts if any failures occur)
- [ ] T027 Start backend API and send test chat message: "What is the deductible for Alpha Health Plan?"
- [ ] T028 Verify response includes answer with source citations and session expiration time
- [ ] T029 Send follow-up message with same sessionId and verify conversation context maintained
- [ ] T030 Wait 30+ minutes idle and verify session expires (next request returns 404 or creates new session)

**Checkpoint**: All user stories working end-to-end

---

## Phase 7: Documentation & Cleanup

**Goal**: Update documentation and remove unused code

### Documentation Updates

- [ ] T031 Update `README.md` at repository root with feature overview and quickstart link
- [ ] T032 Verify `specs/003-foundry-integration/quickstart.md` matches actual implementation

### Code Cleanup

- [ ] T033 Remove backend processing services no longer needed (ChunkingService, EmbeddingService, PdfIngestionService) since processing is now handled by Azure AI Search
- [ ] T034 Remove any unused document processing models if any exist (DocumentChunk, ProcessingStatus, etc.) that are not referenced by remaining code

---

## Dependencies & Execution Order

### Phase Dependencies

1. **Phase 1 (Infrastructure)**: No dependencies - start immediately
2. **Phase 2 (Models/Config)**: Can start in parallel with Phase 1
3. **Phase 3 (Session Management)**: Depends on Phase 2 (models)
4. **Phase 4 (Foundry Service)**: Depends on Phase 2 (config, models)
5. **Phase 5 (Chat API)**: Depends on Phase 3 (SessionService) and Phase 4 (FoundryRagService)
6. **Phase 6 (Testing)**: Depends on Phase 1 (infrastructure) and Phase 5 (API)
7. **Phase 7 (Docs/Cleanup)**: Depends on Phase 6 (validation complete)

### Parallel Opportunities

- **Phase 1**: Infrastructure scripts T001, T002, T003 can be created in parallel; T002a runs after T002 completes
- **Phase 2**: All model updates (T005, T006, T007) can be done in parallel
- **Phase 5**: Session endpoints (T022, T023) can be implemented in parallel after T021 is complete
- **Phase 6**: Some manual tests can run concurrently if using different sessions

### Critical Path

```
T009 (NuGet) → T008 (Config) → T015-T019 (Foundry Service)
                                           ↓
T005-T007 (Models) → T010-T012 (Session Logic) → T020-T021 (Chat Endpoint) → T024-T030 (Testing)
                     ↓
                     T013-T014 (Cleanup Worker)
```

### Within Each User Story

- **US1 (Infrastructure)**: Scripts are independent and idempotent, but execute in order: Blob Storage (T001) → AI Search (T002) → Verify indexer (T002a) → Foundry (T003)
- **US3 (Chat)**: Models → Services → Controllers → Testing

---

## Notes

- **No automated tests**: Per constitution, this is a learning project without test suite requirements
- **No UI changes**: Existing Angular frontend already supports chat; no modifications needed
- **Document processing**: Entirely Azure-managed via AI Search Indexer + Skillset; no backend processing code
- **RAG orchestration**: Entirely Azure-managed via Foundry RAG endpoint; backend only forwards requests
