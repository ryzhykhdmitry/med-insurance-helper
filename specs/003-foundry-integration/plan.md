# Implementation Plan: Intelligent Document Processing and Retrieval

**Branch**: `003-foundry-integration` | **Date**: May 27, 2026 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/003-foundry-integration/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Enable cloud-based document processing and AI-powered question answering for insurance plan documents. Users add PDF documents to Azure Blob Storage. Azure AI Search Indexer automatically monitors the blob container, processes new documents through a skillset (text extraction, chunking, embedding generation), and indexes them into a vector search index. The .NET API forwards chat requests to **Azure AI Foundry's RAG endpoint**, which orchestrates the entire retrieval-augmented generation pipeline (query embedding → search → context assembly → LLM response generation). All document processing AND RAG orchestration are handled by Azure services; the backend only manages sessions and forwards requests.

**Scope**: Initial implementation focuses on core chat functionality (User Stories 1-3: Infrastructure Setup, Document Processing, and Question Answering). Advanced features like document summarization and cross-document comparison are excluded to maintain simplicity.

## Technical Context

**Language/Version**: C# / .NET 8.0

**Primary Dependencies**: 
- Azure.AI.Projects (for calling Foundry RAG endpoint)
- Azure.Storage.Blobs (for blob container setup/verification)
- ASP.NET Core Web API (existing application framework)

**Storage**: 
- Azure Blob Storage (PDF document storage)
- Azure AI Search (vector index, built-in document processing via indexer + skillset)
- In-memory or simple persistence for conversation sessions

**Testing**: Per constitution - no automated tests required for study project

**Target Platform**: Cloud-hosted .NET Web API (Azure or compatible hosting)

**Project Type**: Web service with RESTful API endpoints

**Performance Goals**: 
- Document processing: Handled by Azure AI Search indexer (no backend involvement)
- Chat response latency: <5 seconds end-to-end
- Concurrent users: 10 without >20% degradation

**Constraints**: 
- Must use Azure Blob Storage and Azure AI Search (existing project constraints)
- Must integrate with existing .NET API without UI changes
- Must use free/emulator tiers where possible (constitution requirement)
- All document processing handled by Azure AI Search indexer + skillset
- All RAG orchestration handled by Azure AI Foundry
- Backend only manages sessions and forwards requests to Foundry
- 30-minute conversation session timeout

**Scale/Scope**: 
- Learning project scale (hundreds of documents)
- 10 concurrent users
- English language only
- PDF documents only

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Design Check (Before Phase 0)

| Constitution Principle | Compliance Status | Notes |
|------------------------|-------------------|-------|
| **LLM: Foundry (single provider)** | ✅ **PASS** | Using Azure AI Foundry as specified in constitution |
| **Document storage: Blob storage** | ✅ **PASS** | Using Azure Blob Storage as specified in constitution |
| **Testing: No automated tests required** | ✅ **PASS** | Study project - no test suite required |
| **Environment: Free services and local emulators** | ✅ **PASS** | Azure free tiers for cloud development and testing |
| **Security: Avoid sensitive personal data** | ✅ **PASS** | Insurance plan documents contain policy information, not personal health records |
| **License: MIT** | ✅ **PASS** | Existing project already uses MIT license |

**Gate Result**: ✅ **PROCEED** - No constitution violations detected

### Post-Design Check (After Phase 1)

**Re-evaluation of compliance after technical design and architecture decisions:**

| Constitution Principle | Compliance Status | Design Impact |
|------------------------|-------------------|---------------|
| **LLM: Foundry (single provider)** | ✅ **PASS** | Design uses Azure AI Foundry RAG endpoint for orchestration; no additional LLM providers introduced |
| **Document storage: Blob storage** | ✅ **PASS** | PDFs stored in Azure Blob Storage; no alternative storage (S3, GCS, local files) introduced |
| **Testing: No automated tests required** | ✅ **PASS** | Quickstart includes manual test scenarios; no xUnit/NUnit test projects added |
| **Environment: Free services and local emulators** | ✅ **PASS** | Azure Basic tier for cloud (within free credits); no premium services required |
| **Security: Avoid sensitive personal data** | ✅ **PASS** | Sample insurance plan documents are generic policy information; no PII, PHI, or financial data ingested |
| **License: MIT** | ✅ **PASS** | No new dependencies with incompatible licenses; all Azure SDKs are MIT-compatible |

**New Dependencies Introduced**:
- `Azure.AI.Projects` (MIT license) ✓
- Built-in libraries only

**Final Gate Result**: ✅ **PROCEED TO IMPLEMENTATION** - Design adheres to all constitution principles without exceptions or violations

## Project Structure

### Documentation (this feature)

```text
specs/003-foundry-integration/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   └── api.md          # API contract extensions
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── backend/
│   └── MedInsuranceHelper.Api/
│       ├── Controllers/          # Existing: Chat, Search, Session controllers
│       │   └── ChatController.cs # Extend to forward requests to Foundry
│       ├── Services/             
│       │   ├── FoundryRagService.cs       # NEW: Call Foundry RAG endpoint
│       │   └── SessionService.cs          # Existing: extend for 30min timeout
│       ├── Models/               
│       │   ├── ConversationSession.cs     # Existing: extend with expiration
│       │   └── ChatMessage.cs             # Existing: reuse
│       └── Configuration/        
│           └── AppSettings.cs             # Extend for Foundry settings
├── frontend/                    # Existing Angular app - no changes needed
│   └── src/
│       └── app/
└── scripts/                     # Azure infrastructure setup
    ├── setup-blob-storage.ps1   # NEW: Create container
    ├── setup-search-indexer.ps1 # NEW: Configure indexer + skillset
    └── setup-foundry-project.ps1 # NEW: Configure Foundry RAG endpoint
```

**Structure Decision**: Existing web application structure (backend + frontend) is retained. Document processing is handled by Azure AI Search indexer + skillset. RAG orchestration is handled by Azure AI Foundry. Backend is simplified to session management and forwarding requests to Foundry RAG endpoint.

## Complexity Tracking

**No violations detected** - Design adheres to all constitution principles without exceptions.
