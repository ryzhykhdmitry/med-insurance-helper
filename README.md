# Medical Insurance Helper

A RAG (Retrieval-Augmented Generation) chatbot for exploring, comparing, and getting recommendations from medical insurance offer documents.

## Architecture Overview

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
│  Controllers: Ingest · Process · Search · Stream · Compare       │
│               Recommend · Session                                │
│                                                                  │
│  Services:                                                       │
│   BlobStorageService → Azure Blob / Azurite                     │
│   PdfIngestionService → PdfPig (text + PII detection)           │
│   ChunkingService → Sliding-window (configurable size/overlap)  │
│   EmbeddingService → Azure AI Foundry embeddings API            │
│   FileVectorStore → Local JSON files (cosine similarity)        │
│   FoundryClient → Azure AI Foundry chat completions + SSE       │
│   LLMPipelineService → RAG prompt composition + streaming       │
│   RetrievalService · ComparisonService · RecommendationService   │
│   SessionService (in-memory multi-turn conversation)            │
└──────────┬───────────────────────────┬───────────────────────────┘
           │                           │
    ┌──────▼──────┐           ┌────────▼─────────┐
    │  Azurite    │           │  Azure AI Foundry │
    │  (local     │           │  (embeddings +    │
    │  blob store)│           │  chat completions)│
    └─────────────┘           └──────────────────┘
                  data/vectors/{offerId}.json
                  (local file-backed vector store)
```

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | .NET 8 WebAPI (controllers) |
| Frontend | Angular 17 (standalone components, routing) |
| Blob Storage | Azure Blob Storage / Azurite (local dev) |
| LLM + Embeddings | Azure AI Foundry (Azure.AI.OpenAI SDK) |
| PDF Parsing | PdfPig NuGet package |
| Vector Store | Local JSON files with cosine similarity |
| Logging | Serilog with console sink |
| CI | GitHub Actions |

## Local Development Setup

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