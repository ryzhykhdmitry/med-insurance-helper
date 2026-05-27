# Medical Insurance Helper

A RAG (Retrieval-Augmented Generation) chatbot for exploring, comparing, and getting recommendations from medical insurance offer documents.

## Features

### 🚀 **Latest: Intelligent Document Processing and Retrieval (Feature 003)**

Fully cloud-managed document processing and RAG-powered chat using Azure AI services:

- **Automatic Document Processing**: Upload PDFs to Azure Blob Storage, Azure AI Search Indexer automatically processes them (text extraction, chunking, embedding generation)
- **RAG-Powered Chat**: Ask natural language questions about insurance plans with AI-generated answers backed by source citations
- **Session Management**: 30-minute conversation sessions with automatic expiration and cleanup
- **Source Citations**: Responses include references to specific document chunks with relevance scores

**Architecture** (Feature 003 - Azure-Managed RAG):

```
┌──────────────────┐
│  Azure Blob      │ ◄─── User uploads PDFs
│  Storage         │
└────────┬─────────┘
         │ Monitored by
         ▼
┌──────────────────┐
│  AI Search       │ Automatically processes new documents
│  Indexer         │
└────────┬─────────┘
         │ Executes
         ▼
┌──────────────────┐
│  Skillset        │ Text extraction → Chunking → Embedding generation
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│  Vector Index    │ Stores document chunks with embeddings
│  (AI Search)     │
└────────┬─────────┘
         │
         │ RAG Pipeline:
┌────────▼─────────┐  • Generate query embedding
│  .NET API        │  • Search vector index
│  (/api/chat)     │  • Retrieve relevant chunks
│                  │  • Assemble prompt with context
│  Services:       │  • Call Azure OpenAI for answer
│  - FoundryRag    │  • Return response with citations
│  - Session Mgmt  │
└──────────────────┘
         ▲
         │ User queries
┌────────┴─────────┐
│  Angular 17 SPA  │
│  (Frontend)      │
└──────────────────┘
```

**Key Differences from Legacy Approach**:
- ❌ No backend document processing (handled by Azure AI Search Indexer)
- ❌ No manual chunking or embedding in backend (handled by skillset)
- ❌ No local vector store (uses Azure AI Search)
- ✅ Fully cloud-managed, scalable, production-ready

See [Feature Documentation](specs/003-foundry-integration/) for detailed implementation guide.

---

### 📚 Legacy Features (Features 001-002)

<details>
<summary>Click to expand legacy architecture</summary>

## Legacy Architecture Overview

```
┌──────────────────────────────────────────────────────────────────┐
│  Angular 17 SPA (src/frontend)                                   │
│  ┌──────────┐  ┌──────────┐  ┌───────────┐                      │
│  │   Chat   │  │ Compare  │  │ Recommend │  Standalone components │
│  └──────────┘  └──────────┘  └───────────┘  + SessionService     │
└────────────────────────────┬─────────────────────────────────────┘
                             │ HTTP + SSE (text/event-stream)
┌────────────────────────────▼─────────────────────────────────────┐
│  .NET 8 WebAPI (src/backend/MedInsuranceHelper.Api)              │
│                                                                  │
│  Controllers: Ingest · Process · Search · Compare · Session      │
│                                                                  │
│  Services (Legacy - Deprecated):                                 │
│   BlobStorageService → Azure Blob / Azurite                     │
│   PdfIngestionService → PdfPig (text extraction)  [DEPRECATED]  │
│   ChunkingService → Sliding-window chunking       [DEPRECATED]  │
│   EmbeddingService → Azure AI Foundry embeddings                │
│   FileVectorStore → Local JSON files              [DEPRECATED]  │
│   FoundryClient → Azure AI Foundry chat completions             │
│                                                                  │
│  Services (Current - Feature 003):                               │
│   FoundryRagService → RAG orchestration with Azure AI Search    │
│   SessionService → Conversation management with expiration      │
└──────────┬───────────────────────────┬───────────────────────────┘
           │                           │
    ┌──────▼──────┐           ┌────────▼─────────┐
    │  Azurite    │           │  Azure AI Search  │
    │  (local     │           │  + Azure OpenAI   │
    │  blob store)│           │  (cloud services) │
    └─────────────┘           └──────────────────┘
```

</details>

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | .NET 8 WebAPI (controllers) |
| Frontend | Angular 17 (standalone components, routing) |
| Document Storage | Azure Blob Storage |
| Document Processing | Azure AI Search Indexer + Skillset |
| Vector Search | Azure AI Search (hybrid: vector + keyword) |
| LLM + Embeddings | Azure OpenAI (via Azure AI Search) |
| Logging | Serilog with console sink |
| CI | GitHub Actions |

## Quick Start (Feature 003 - Azure-Managed RAG)

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- [Azure CLI](https://aka.ms/installazurecliwindows)
- Azure subscription with permissions to create resources

### Setup Steps

1. **Provision Azure Resources**

```powershell
# Login to Azure
az login

# Run provisioning scripts (in order)
cd scripts
.\setup-blob-storage.ps1
.\setup-search-indexer.ps1
.\setup-foundry-project.ps1  # Provides manual setup instructions
```

2. **Configure Application**

Update `src/backend/MedInsuranceHelper.Api/appsettings.Development.json` with values from setup scripts:

```json
{
  "AppSettings": {
    "ProjectEndpoint": "<from-setup-foundry-project>",
    "SearchEndpoint": "<from-setup-search-indexer>",
    "SearchKey": "<from-setup-search-indexer>",
    "SearchIndexName": "insurance-documents",
    "BlobConnectionString": "<from-setup-blob-storage>",
    "FoundryEndpoint": "<Azure-OpenAI-endpoint>",
    "FoundryApiKey": "<Azure-OpenAI-key>",
    "EmbeddingDeployment": "text-embedding-ada-002",
    "ChatDeployment": "gpt-35-turbo"
  }
}
```

3. **Upload Sample Documents**

```powershell
# Upload PDFs to blob storage
az storage blob upload `
  --account-name medinsurancestorage `
  --container-name insurance-docs `
  --name sample-plan.pdf `
  --file ./docs/samples/alpha-health-plan.pdf `
  --auth-mode key

# Wait 1-3 minutes for indexer to process
# Check status in Azure Portal or with Azure CLI
```

4. **Start the Backend**

```bash
cd src/backend/MedInsuranceHelper.Api
dotnet restore
dotnet run
# API available at https://localhost:5001
# Swagger UI at https://localhost:5001/swagger
```

5. **Test the API**

```powershell
# Send a chat message
Invoke-RestMethod `
  -Uri "https://localhost:5001/api/chat" `
  -Method Post `
  -ContentType "application/json" `
  -Body '{"message":{"text":"What is the deductible for Alpha Health Plan?"}}' `
  -SkipCertificateCheck
```

**Expected Response:**
```json
{
  "sessionId": "abc123...",
  "message": "The Alpha Health Plan has a $1,500 annual deductible...",
  "sourceCitations": [
    {
      "fileName": "alpha-health-plan.pdf",
      "content": "Annual deductible: $1,500...",
      "pageNumber": 3,
      "score": 0.92
    }
  ],
  "timestamp": "2026-05-27T10:45:30Z",
  "expiresAt": "2026-05-27T11:15:30Z"
}
```

See [Feature 003 Documentation](specs/003-foundry-integration/quickstart.md) for detailed testing instructions.

---

## API Endpoints (Feature 003)

| Method | Endpoint | Description |
|--------|---------|-------------|
| POST | `/api/chat` | Send message, get RAG-powered response with citations |
| GET | `/api/sessions/{id}` | Check session status (metadata only) |
| DELETE | `/api/sessions/{id}` | Explicitly end conversation session |

**Legacy endpoints** (Features 001-002) remain available at `/api/chat/legacy`, `/api/ingest`, `/api/process`, etc.

---

## Legacy Development Setup (Features 001-002)

<details>
<summary>Click to expand legacy setup instructions</summary>

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- [npm](https://www.npmjs.com/)
- [Azure AI Foundry](https://ai.azure.com/) account (for embeddings + chat)
- (Optional) [Azurite](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite) for local blob storage

### 1. Configure Environment

```bash
cp .env.example .env
# Edit .env and set:
#   FOUNDRY_API_KEY=your_key
#   FOUNDRY_ENDPOINT=https://your-endpoint.openai.azure.com/
```

### 2. Start Azurite (Local Blob Storage)

```powershell
.\scripts\setup-azurite.ps1 -Command start
```

### 3. Start the Backend

```bash
cd src/backend/MedInsuranceHelper.Api
dotnet run
# API available at http://localhost:5000
# Swagger UI at http://localhost:5000/swagger
```

### 4. Start the Frontend

```bash
cd src/frontend
npm install
npm start
# App available at http://localhost:4200
```

### 5. Load Sample Documents

```powershell
.\scripts\load-samples.ps1
# Ingests 3 sample insurance plans and triggers processing
```

## API Endpoints

| Method | Endpoint | Description |
|--------|---------|-------------|
| POST | `/api/ingest` | Register a new PDF from blob storage |
| POST | `/api/process/{offerId}` | Trigger parse → chunk → embed → store |
| POST | `/api/search` | Semantic search across all offers |
| GET | `/api/stream?query=...` | SSE streaming chat response with citations |
| POST | `/api/compare` | Compare offers on specific aspects |
| POST | `/api/recommend` | Get ranked recommendations for criteria |
| POST | `/api/session` | Create or retrieve a conversation session |

## User Stories

| # | Story | Feature |
|---|-------|---------|
| 1 | Ask about a specific offer | Chat with SSE streaming + citations |
| 2 | Compare two or more offers | Side-by-side comparison table |
| 3 | Get a best-match recommendation | Scored + ranked offers with reasoning |
| 4 | Multi-turn conversation | Session-aware context with history |

## Project Structure

```
med-insurance-helper/
├── .github/workflows/ci.yml          # GitHub Actions CI
├── .env.example                       # Environment variable template
├── docs/samples/                      # Sample insurance documents
├── scripts/
│   ├── setup-azurite.ps1             # Azurite blob storage manager
│   └── load-samples.ps1              # Sample data loader
├── specs/001-insurance-offer-chatbot/ # Feature specification
│   ├── spec.md
│   ├── tasks.md
│   ├── plan/
│   └── contracts/
├── src/
│   ├── backend/
│   │   └── MedInsuranceHelper.Api/
│   │       ├── Configuration/        # AppSettings
│   │       ├── Controllers/          # IngestController, SearchController, etc.
│   │       ├── Middleware/           # ErrorHandlingMiddleware
│   │       ├── Models/               # InsuranceOffer, DocumentChunk, Session, etc.
│   │       ├── Services/             # All application services
│   │       │   └── VectorStore/      # FileVectorStore
│   │       └── Workers/              # ProcessingWorker
│   └── frontend/
│       └── src/app/
│           ├── chat/                 # ChatComponent + ChatService + SessionService
│           ├── compare/              # CompareComponent
│           └── recommend/            # RecommendComponent
└── data/vectors/                     # Generated: local vector store JSON files
```

## Configuration

All settings are in `src/backend/MedInsuranceHelper.Api/appsettings.json` and can be overridden via environment variables:

| Setting | Env Var | Default |
|---------|---------|---------|
| Azure AI Foundry API Key | `FOUNDRY_API_KEY` | — |
| Azure AI Foundry Endpoint | `FOUNDRY_ENDPOINT` | — |
| Blob Connection String | `BLOB_CONN_STRING` | Azurite local |
| App Environment | `APP_ENV` | Development |

## Notes

- This is a local dev/study project — no authentication required for v1
- Vector store is local file-backed (`data/vectors/`) — no cloud vector DB needed
- Switch `BLOB_CONN_STRING` to Azure Blob Storage connection string for cloud deployment
- Conversation history is in-memory per session — resets on backend restart