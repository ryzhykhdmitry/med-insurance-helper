# Tasks: Medical Insurance RAG Chatbot

**Feature**: specs/001-insurance-offer-chatbot
**Spec**: specs/001-insurance-offer-chatbot/spec.md
**Plan**: specs/001-insurance-offer-chatbot/plan/impl_plan.md
**Data Model**: specs/001-insurance-offer-chatbot/plan/data-model.md
**Contracts**: specs/001-insurance-offer-chatbot/contracts/api.md
**Research**: specs/001-insurance-offer-chatbot/plan/research.md

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project scaffolding, tooling configuration, and local development environment

- [X] T001 [P] Initialize .NET 8 WebAPI solution and project at src/backend/MedInsuranceHelper.sln and src/backend/MedInsuranceHelper.Api/
- [X] T002 [P] Initialize Angular 17 frontend app shell with ng new at src/frontend/ (enable routing, use standalone components)
- [X] T003 Add Azurite local blob-storage setup script at scripts/setup-azurite.ps1 with start, stop, and reset commands
- [X] T004 Create .env.example with all required env vars: FOUNDRY_API_KEY, BLOB_CONN_STRING, FOUNDRY_ENDPOINT, APP_ENV at .env.example
- [X] T005 Add AppSettings model and appsettings.json at src/backend/MedInsuranceHelper.Api/appsettings.json and src/backend/MedInsuranceHelper.Api/Configuration/AppSettings.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure shared by ALL user stories — MUST be complete before any story work begins

**⚠️ CRITICAL**: No user story implementation can start until this phase is complete

- [X] T006 [P] Create InsuranceOffer domain model with all fields (id, insurer_name, title, blob_uri, uploaded_at, version, status enum) at src/backend/MedInsuranceHelper.Api/Models/InsuranceOffer.cs
- [X] T007 [P] Create DocumentChunk domain model with all fields (id, offer_id FK, text, start_page, end_page, offset, length, embedding ref, created_at) at src/backend/MedInsuranceHelper.Api/Models/DocumentChunk.cs
- [X] T008 [P] Implement BlobStorageService (Azure.Storage.Blobs SDK, Azurite-compatible) with upload, download and list operations at src/backend/MedInsuranceHelper.Api/Services/BlobStorageService.cs
- [X] T009 Implement PdfIngestionService (parse PDF to text pages, extract page numbers, OCR fallback stub) at src/backend/MedInsuranceHelper.Api/Services/PdfIngestionService.cs
- [X] T010 Implement ChunkingService (sliding window with configurable size/overlap, attach page+offset metadata) at src/backend/MedInsuranceHelper.Api/Services/ChunkingService.cs
- [X] T011 Implement EmbeddingService wrapper (calls Foundry embeddings API, batching, error handling) at src/backend/MedInsuranceHelper.Api/Services/EmbeddingService.cs
- [X] T012 Implement FileVectorStore (local JSON file-backed, cosine similarity search, topK) at src/backend/MedInsuranceHelper.Api/Services/VectorStore/FileVectorStore.cs
- [X] T013 Implement FoundryClient (Azure AI Foundry auth, chat completion with streaming SSE, retry logic) at src/backend/MedInsuranceHelper.Api/Services/FoundryClient.cs
- [X] T014 Implement IngestController (POST /api/ingest) — accepts blob URI + insurer info, persists InsuranceOffer, returns 202 with offerId at src/backend/MedInsuranceHelper.Api/Controllers/IngestController.cs
- [X] T015 Implement ProcessingWorker background job (POST /api/process/{offerId}) — downloads PDF from blob, chunks, embeds, stores vectors at src/backend/MedInsuranceHelper.Api/Workers/ProcessingWorker.cs

**Checkpoint**: Foundation ready — IngestController, vector store, FoundryClient, and chunking pipeline are operational. User story phases can now begin.

---

## Phase 3: User Story 1 — Ask About a Specific Insurance Offer (Priority: P1) 🎯 MVP

**Goal**: User asks a natural language question → system retrieves relevant PDF passages → streams a cited answer back via the chat UI.

**Independent Test**: Upload one PDF, call POST /api/ingest + POST /api/process/{offerId}, then open the chat UI and ask "What is the annual coverage limit for hospitalisation?" — verify a streamed, cited answer references the correct document section.

### Implementation for User Story 1

- [X] T016 [P] [US1] Implement SearchController (POST /api/search — embed query, query FileVectorStore, return topK chunks with page/score) at src/backend/MedInsuranceHelper.Api/Controllers/SearchController.cs
- [X] T017 [US1] Implement RetrievalService (orchestrate embedding query + vector lookup, assemble ranked DocumentChunk results) at src/backend/MedInsuranceHelper.Api/Services/RetrievalService.cs
- [X] T018 [US1] Implement LLMPipelineService (compose RAG prompt from chunks, call FoundryClient with streaming, produce Citation objects) at src/backend/MedInsuranceHelper.Api/Services/LLMPipelineService.cs
- [X] T019 [US1] Implement StreamController (GET /api/stream — SSE endpoint, pipes FoundryClient stream tokens to client, flushes citations at end) at src/backend/MedInsuranceHelper.Api/Controllers/StreamController.cs
- [X] T020 [US1] Add "no relevant information found" guard in LLMPipelineService: when RetrievalService returns empty or low-confidence results, return a clear not-found message without calling Foundry at src/backend/MedInsuranceHelper.Api/Services/LLMPipelineService.cs
- [X] T021 [US1] Create Angular ChatModule with ChatComponent (message input, message list, loading indicator) at src/frontend/src/app/chat/chat.component.ts
- [X] T022 [US1] Implement ChatService (EventSource-based SSE client, accumulate streamed tokens, parse citation payloads) at src/frontend/src/app/chat/chat.service.ts
- [X] T023 [US1] Add citation display in chat message template (source document name, page reference, excerpt tooltip) at src/frontend/src/app/chat/chat.component.html

**Checkpoint**: User Story 1 fully functional — user can ask questions in the chat UI and receive streamed, cited answers from real PDFs.

---

## Phase 4: User Story 2 — Compare Two or More Insurance Offers (Priority: P2)

**Goal**: User asks to compare a specific benefit across multiple insurers → system retrieves passages from each insurer's PDF and returns a structured comparison with per-insurer citations.

**Independent Test**: Upload two insurer PDFs, process both, then ask "Compare dental coverage between Company A and Company B" — verify both sources are cited and any missing data is clearly noted.

### Implementation for User Story 2

- [X] T024 [P] [US2] Implement CompareController (POST /api/compare — accepts offerIds + aspects, aggregates chunk results per offer per aspect) at src/backend/MedInsuranceHelper.Api/Controllers/CompareController.cs
- [X] T025 [US2] Implement ComparisonService (for each aspect, run RetrievalService per offerId, normalise snippets, flag missing coverage with explicit null marker) at src/backend/MedInsuranceHelper.Api/Services/ComparisonService.cs
- [X] T026 [US2] Add CompareComponent in Angular (side-by-side table layout, missing-data badge, per-cell citation links) at src/frontend/src/app/compare/compare.component.ts and compare.component.html

**Checkpoint**: User Story 2 functional — user can compare any aspect across at least two insurers with per-offer citations and clear missing-data indication.

---

## Phase 5: User Story 3 — Get a Best-Match Recommendation (Priority: P3)

**Goal**: User describes their needs → system evaluates all stored offers against the criteria → returns ranked recommendation(s) with reasoning and citations.

**Independent Test**: Upload 3+ insurer PDFs, process all, then ask "I need cover for a family of four with good dental and vision" — verify a named offer is recommended with supporting citations and a fallback message when no perfect match exists.

### Implementation for User Story 3

- [X] T028 [P] [US3] Implement RecommendController (POST /api/recommend — accepts free-text criteria, returns ranked offers with reason + citations) at src/backend/MedInsuranceHelper.Api/Controllers/RecommendController.cs
- [X] T029 [US3] Implement RecommendationService (run RetrievalService for each offer vs criteria, score relevance, sort and format with closest-match fallback) at src/backend/MedInsuranceHelper.Api/Services/RecommendationService.cs
- [X] T030 [US3] Add RecommendComponent in Angular (criteria input form, recommendation result card with ranked offers and citation list) at src/frontend/src/app/recommend/recommend.component.ts and recommend.component.html

**Checkpoint**: User Story 3 functional — user receives a personalised recommendation with cited reasoning within 3 conversation turns.

---

## Phase 6: User Story 4 — Continue a Multi-Turn Conversation (Priority: P4)

**Goal**: User asks follow-up questions within the same session, referencing earlier answers — system resolves implicit references from conversation history without requiring the user to repeat context.

**Independent Test**: Ask a question, receive an answer, then ask "What about that plan's vision coverage?" — verify the system resolves "that plan" from conversation history and responds accurately.

### Implementation for User Story 4

- [X] T031 [P] [US4] Create ConversationSession and Message domain models (session id, started_at, last_active_at; message role enum, text, citations array) at src/backend/MedInsuranceHelper.Api/Models/ConversationSession.cs and src/backend/MedInsuranceHelper.Api/Models/Message.cs
- [X] T032 [US4] Implement SessionService (in-memory store: create session, append message, retrieve history) at src/backend/MedInsuranceHelper.Api/Services/SessionService.cs
- [X] T033 [US4] Implement SessionController (POST /api/session — create or retrieve session, return sessionId) at src/backend/MedInsuranceHelper.Api/Controllers/SessionController.cs
- [X] T034 [US4] Update LLMPipelineService to accept conversation history parameter and include prior turns in Foundry prompt for context-aware responses at src/backend/MedInsuranceHelper.Api/Services/LLMPipelineService.cs
- [X] T035 [US4] Implement SessionService on frontend (persist sessionId in memory, pass it with every chat request) at src/frontend/src/app/chat/session.service.ts
- [X] T036 [US4] Update ChatComponent to display conversation history and pass sessionId on all requests at src/frontend/src/app/chat/chat.component.ts

**Checkpoint**: User Story 4 functional — follow-up questions within the same session are correctly resolved in context.

---

## Final Phase: Polish & Cross-Cutting Concerns

**Purpose**: Error handling, observability, sample data, CI, and documentation

- [X] T037 [P] Add sample PDF set (at least 2 insurers) to docs/samples/ and loader script scripts/load-samples.ps1 that ingests and processes them via the API
- [X] T038 [P] Add GitHub Actions CI workflow (build .NET backend, ng build frontend, fail on errors) at .github/workflows/ci.yml
- [X] T039 Add global error handling middleware and structured logging (Serilog or ILogger) across all backend controllers and services at src/backend/MedInsuranceHelper.Api/Middleware/ErrorHandlingMiddleware.cs
- [X] T040 Add ingestion-failure notification path: log unreadable/failed PDFs with clear operator message in ProcessingWorker at src/backend/MedInsuranceHelper.Api/Workers/ProcessingWorker.cs
- [X] T043 Add heuristic sensitive-data detection in PdfIngestionService: after text extraction, scan for PII-like patterns (NIN/SSN, IBAN, date-of-birth, credit card regex); emit a structured WARNING log entry with offerId and match summary (no content dumped) so operator can review before proceeding — at src/backend/MedInsuranceHelper.Api/Services/PdfIngestionService.cs
- [X] T041 Write integration demo checklist (upload PDF → ingest → process → query → compare → recommend → multi-turn) at specs/001-insurance-offer-chatbot/checklists/integration.md
- [X] T042 Update README with architecture diagram description, tech stack, and local run steps at README.md

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately; T001 and T002 are fully parallel
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS** all user story phases
- **User Story Phases (3–6)**: All depend on Foundational phase completion; stories can proceed in priority order or in parallel with a full team
- **Polish (Final Phase)**: Depends on all desired stories being complete

### User Story Dependencies

| Story | Depends on | Notes |
|-------|-----------|-------|
| US1 (P1) | Phase 2 | No dependency on other stories — implement first |
| US2 (P2) | Phase 2 | Builds on RetrievalService from US1 but independently testable |
| US3 (P3) | Phase 2 | Builds on RetrievalService from US1 but independently testable |
| US4 (P4) | Phase 2 + US1 | Requires LLMPipelineService from US1 for context-aware prompts |

### Within Each User Story

1. Models (if new) → Services → Controllers → Frontend components
2. Core happy-path implementation before error/fallback handling
3. Story complete and checkpoint validated before starting next priority

### Parallel Opportunities

- **Phase 1**: T001 and T002 (backend + frontend init) fully parallel
- **Phase 2**: T006, T007, T008 fully parallel; T009–T015 sequential (each builds on prior)
- **Phase 3**: T016 (SearchController) parallel with T017–T020 backend chain; T021–T023 frontend parallel with backend
- **Phase 4**: T024 (CompareController) parallel with T025–T026 service+UI chain
- **Phase 5**: T028 (RecommendController) parallel with T029–T030 service+UI chain
- **Phase 6**: T031 (models) parallel; T032–T036 mostly sequential
- **Final Phase**: T037 and T038 fully parallel

---

## Parallel Example: User Story 1

```bash
# Backend and frontend can proceed in parallel once Phase 2 is complete:

# Stream 1 — Backend (sequential within stream):
Task T016: Implement SearchController POST /api/search
Task T017: Implement RetrievalService
Task T018: Implement LLMPipelineService with streaming
Task T019: Implement StreamController SSE endpoint
Task T020: Add no-results guard in LLMPipelineService

# Stream 2 — Frontend (can start once SSE contract is agreed):
Task T021: Create ChatComponent
Task T022: Implement ChatService (EventSource SSE)
Task T023: Add citation display in chat template
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete **Phase 1**: Scaffold backend + frontend
2. Complete **Phase 2**: Foundation pipeline (ingest → chunk → embed → store)
3. Complete **Phase 3**: US1 — question → retrieve → stream → cite
4. **STOP and VALIDATE**: Upload one PDF, ask one factual question, confirm streaming + citation
5. Demo or deploy the MVP

### Incremental Delivery

| Milestone | Phases | What user can do |
|-----------|--------|-----------------|
| MVP | 1 + 2 + US1 | Ask factual questions with streamed, cited answers |
| +Compare | + US2 | Compare any aspect across two or more insurers |
| +Recommend | + US3 | Get personalised offer recommendations |
| +Multi-turn | + US4 | Ask follow-up questions without repeating context |
| Production-ready | + Polish | Full CI, error handling, demo data |

### Parallel Team Strategy

With multiple developers available after Phase 2:

- **Dev A**: US1 backend (T016–T020)
- **Dev B**: US1 frontend (T021–T023) — coordinate on SSE contract
- **Dev C**: US2 (T024–T026) — reuses RetrievalService
- **Dev D**: US3 (T028–T030) — reuses RetrievalService

---

## Summary

- **Total tasks**: 43
- **Tasks per story**: US1 = 8 (T016–T023), US2 = 3 (T024–T026), US3 = 3 (T028–T030), US4 = 6 (T031–T036)
- **Setup tasks**: 5 (T001–T005)
- **Foundational tasks**: 10 (T006–T015)
- **Polish tasks**: 6 (T037–T042)
- **Parallel opportunities**: T001/T002 (setup), T006/T007/T008 (foundation), T016/T021 (US1 backend+frontend), T024/T028 (US2/US3 controllers)
- **Suggested MVP scope**: T001–T023 (Phases 1, 2, and US1)

---

## Notes

- `[P]` = task operates on different files with no dependencies on incomplete peer tasks — safe to run in parallel
- `[USn]` label maps task to its user story for traceability and independent testing
- Each user story phase ends with a **Checkpoint** — validate the story independently before moving to the next priority
- Conversation history (US4) is in-memory per session; cross-session persistence is out of scope for v1
- Use Azurite locally for blob storage; switch BLOB_CONN_STRING to Azure Blob for cloud deployment
- Research decisions (chunking strategy, retrieval approach, vector store) are captured in specs/001-insurance-offer-chatbot/plan/research.md
