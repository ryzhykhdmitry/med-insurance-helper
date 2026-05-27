# Research: Intelligent Document Processing and Retrieval

**Feature**: 003-foundry-integration | **Date**: May 27, 2026

## Overview

This document captures technical research and decisions for implementing cloud-based document processing with Azure AI Foundry integration. Research focused on three key areas: Azure AI Foundry RAG orchestration, Azure AI Search Indexer for document processing, and in-memory session management.

---

## 1. Azure AI Foundry Integration

### Decision: Use Azure AI Foundry RAG Endpoint for Complete Pipeline Orchestration

**Rationale**: Azure AI Foundry provides a fully managed RAG pipeline that eliminates the need for backend orchestration code. Foundry handles query embedding, search, context assembly, and LLM response generation in a single optimized endpoint call.

**Architecture Flow**:
```
Azure Blob Storage (PDFs)
         ↓
   AI Search Indexer (monitors container)
         ↓
   Skillset (text extraction → chunking → embeddings)
         ↓
   Vector Search Index (connected to Foundry)
         ↓
   Azure AI Foundry RAG Endpoint
      • Generates query embedding
      • Searches vector index (hybrid)
      • Retrieves relevant chunks
      • Assembles prompt with context
      • Calls LLM for response
      • Returns answer with citations
         ↓
   .NET API (forwards requests, manages sessions)
```

**Key Components**:
- **Azure Blob Storage**: PDF document storage
- **Azure AI Search Indexer + Skillset**: Document processing (extraction, chunking, embedding for indexing)
- **Vector Search Index**: Stores document chunks with embeddings
- **Azure AI Foundry RAG Endpoint**: Orchestrates entire query pipeline
- **Azure OpenAI Models** (deployed in Foundry):
  - `text-embedding-ada-002`: Query embedding + chunk embedding (via skillset)
  - `gpt-35-turbo` or `gpt-4`: Response generation

**SDK Packages** (Backend simplified):
- `Azure.AI.Projects` - Call Foundry RAG endpoint
- `Azure.Identity` - For authentication

**NOT NEEDED** (processing and orchestration handled by Azure):
- ~~`Azure.AI.OpenAI`~~ (Foundry handles LLM calls)
- ~~`Azure.Search.Documents`~~ (Foundry handles search queries)
- ~~`UglyToad.PdfPig`~~ (indexer handles PDF extraction)
- ~~`Azure.Messaging.EventGrid`~~ (indexer monitors blob storage)
- ~~Manual embedding generation~~ (Foundry does this for queries)
- ~~Manual prompt assembly~~ (Foundry does this)

**Foundry RAG Configuration**:
- **Index Connection**: Foundry project linked to Azure AI Search index
- **Chunking**: Handled by AI Search skillset (512-1024 tokens)
- **Search Type**: Hybrid (vector + semantic ranking)
- **Retrieval Parameters**:
  - `top_n`: 5 documents
  - `strictness`: 3 (moderate filtering)
  - `in_scope`: true (restrict to document content)

**Backend Interaction**:
```csharp
// Backend simply forwards request to Foundry
var response = await foundryClient.ChatAsync(new RagRequest
{
    Query = userMessage,
    ConversationHistory = sessionMessages,
    MaxResults = 5
});

// Foundry returns: answer, source citations, relevance scores
return new ChatResponse
{
    Message = response.Answer,
    SourceDocuments = response.Citations
};
```

**Advantages Over Manual Orchestration**:
- No backend embedding generation
- No manual search queries
- No prompt engineering in code
- Optimized retrieval strategies
- Built-in citation tracking
- Consistent model alignment (embedding + LLM)
- Prompt flow integration available if needed

**Token Budget Management** (handled by Foundry):
- System message: ~1,000 tokens
- Conversation history: ~2,000 tokens (5 messages)
- Retrieved context: 5 chunks × 512 = 2,560 tokens
- Response: ~1,500 tokens reserved
- **Total**: ~7,000 tokens (within GPT-3.5/4 limits)

---

## 2. Azure AI Search Indexer (Automatic Processing)

### Decision: Use AI Search Indexer to Monitor Blob Storage

**Rationale**: Azure AI Search indexer provides automatic blob monitoring without requiring Event Grid, webhooks, or backend processing code. The indexer polls the container or uses change feed to detect new documents.

**Architecture**:
```
User uploads PDF to Blob Storage
         ↓
AI Search Indexer (runs on schedule or detects changes)
         ↓
Pulls document from blob storage
         ↓
Executes skillset pipeline:
  - Document cracking (extract content from PDF)
  - Text splitting (chunk into segments)
  - Embedding generation (call Azure OpenAI)
  - Indexing (store chunks + vectors)
         ↓
Document searchable in vector index
```

**Indexer Configuration**:
- **Data Source**: Azure Blob Storage container
- **Schedule**: Every 5 minutes OR change detection (blob metadata)
- **Deletion Detection**: Soft delete policy on blobs
- **File Types**: `.pdf` filter via indexer configuration

**Skillset Pipeline**:
1. **Document Extraction Skill**: Extracts text from PDF (built-in OCR + text extraction)
2. **Text Split Skill**: Chunks text into 512-1024 token segments
3. **Azure OpenAI Embedding Skill**: Generates embeddings for each chunk
4. **Index Projections**: Maps chunks to searchable index fields

**Advantages Over Event Grid**:
- No webhook endpoint needed
- No idempotency tracking required (indexer handles this)
- No backend processing code
- Built-in retry logic
- Automatic change detection
- Simpler architecture

**Backend Responsibility**: NONE - indexer handles everything

**Indexer Triggers**:
- **Schedule-based**: Runs every 5-15 minutes (configurable)
- **On-demand**: Manual trigger via Azure Portal or API
- **Change Detection**: Uses blob metadata timestamps

**NOT NEEDED**:
- ~~Event Grid subscription~~
- ~~Webhook validation endpoint~~
- ~~EventGridController~~
- ~~Document processing queue~~

---

## 3. Document Deduplication

### Decision: Rely on Blob Storage Path + Indexer Change Detection

**Rationale**: Azure AI Search indexer uses blob path and metadata (eTag, last modified) to detect changes. Duplicate detection is built-in; no custom hashing needed.

**How It Works**:
- Indexer tracks `blob_uri` as document key in search index
- If same blob re-indexed (content updated): Existing document updated
- If new blob with different path: New document created
- If blob deleted: Document removed from index (with deletion detection enabled)

**Deduplication Flow**:
1. User uploads `alpha-health-plan.pdf` → indexed as `/insurance-docs/alpha-health-plan.pdf`
2. User uploads same file renamed as `alpha-v2.pdf` → indexed as separate document (different path)
3. User updates `alpha-health-plan.pdf` → existing document re-indexed with new content

**Backend Responsibility**: NONE - indexer handles this automatically

**Content-Based Deduplication** (Optional Enhancement):
- If strict content deduplication needed (same content, different filename):
  - Enable blob metadata: Store `ContentHash` in blob metadata on upload
  - Configure indexer to read custom metadata fields
  - Use hash as document key instead of blob path
- **Not implemented for learning project** (simple path-based approach sufficient)

**NOT NEEDED**:
- ~~SHA256 content hashing in backend~~
- ~~ContentHashService~~
- ~~Duplicate detection queries~~

---

## 4. Retry Logic (Indexer-Managed)

### Decision: Rely on Azure AI Search Indexer Built-In Retry

**Rationale**: Azure AI Search indexer has built-in retry logic with exponential backoff. No custom retry implementation needed in backend.

**Indexer Retry Behavior**:
- Transient errors (network timeouts, throttling): Automatic retry with exponential backoff
- Skill execution failures: Retry up to configured limit (default: 3 attempts)
- Document-level failures: Marked as failed; can be reprocessed on next run
- Total retry time: Configurable max duration per document

**Indexer Error Handling**:
- Failed documents logged in indexer execution history
- Errors viewable in Azure Portal: Indexer → Execution History → Failed documents
- Can manually reset and re-run indexer for failed documents

**Backend Responsibility**: NONE - indexer handles all retries

**Monitoring Failures**:
- Check indexer status via Azure Portal or Search API
- Query indexer execution history for error details
- Set up alerts for indexer failures

**NOT NEEDED**:
- ~~Custom exponential backoff implementation~~
- ~~RetryCount tracking in backend~~
- ~~Retry scheduling logic~~
- ~~ProcessingStatus state machine~~

---

## 5. Session Management

### Decision: In-Memory with Background Cleanup

**Rationale**: For learning project scale (10 concurrent users), in-memory storage with periodic cleanup is simple, fast, and sufficient. No external dependencies or complexity.

**Current State**: 
- `SessionService` already exists with `Dictionary<string, ConversationSession>`
- `LastActiveAt` timestamp tracking implemented
- Missing: Expiration and cleanup logic

**Implementation Approach**:

**1. Add Cleanup Method to SessionService**:
```csharp
public void RemoveExpiredSessions(int inactivityMinutes = 30)
{
    lock (_lock)
    {
        var expired = _sessions
            .Where(kvp => DateTime.UtcNow - kvp.Value.LastActiveAt > TimeSpan.FromMinutes(inactivityMinutes))
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var id in expired)
        {
            _sessions.Remove(id);
            _logger.LogInformation("Expired session {SessionId} after {Minutes} minutes of inactivity", id, inactivityMinutes);
        }
    }
}
```

**2. Create Background Cleanup Worker**:
```csharp
public class SessionCleanupWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<SessionCleanupWorker> _logger;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Yield(); // Prevent blocking startup
        
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(10), ct);
            
            using var scope = _services.CreateScope();
            var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();
            sessionService.RemoveExpiredSessions(30);
        }
    }
}
```

**3. Register in Program.cs**:
```csharp
services.AddHostedService<SessionCleanupWorker>();
```

**Cleanup Frequency**: Every 10 minutes
- **Rationale**: 30-minute timeout means sessions expire slowly; 10-min cleanup is responsive without overhead
- **Scale**: 100 sessions scanned in <1ms; negligible CPU/memory cost

**Sliding Expiration**:
- Every `Get()` or `AppendMessage()` call updates `LastActiveAt = DateTime.UtcNow`
- Already implemented in existing `SessionService`
- Ensures active conversations never expire

**Session Lifecycle**:
1. User sends first message → Session created with `LastActiveAt = UtcNow`
2. User sends follow-up within 30min → `LastActiveAt` updated, session remains active
3. User idle for >30min → Background worker removes session on next cleanup cycle (10min)
4. User sends message after expiration → New session created (conversation history lost)

**Data Persistence**:
- **Not implemented**: Sessions lost on app restart
- **Acceptable for learning project**: Users re-establish context easily
- **Migration path**: Switch to `IDistributedCache` + Redis for persistence if scaling beyond 10 users

**Alternatives Considered**:
- **IMemoryCache with built-in expiration**: Requires serialization; less control over cleanup logging
- **IDistributedCache + Redis**: Over-engineered for 10 users; adds deployment complexity
- **Database storage**: Unnecessary overhead; slow for high-frequency reads/writes
- **Decision**: In-memory + manual cleanup is simplest for current scale

---

## 6. Cross-Cutting Concerns

### Authentication & Security
- **Azure Identity**: Use managed identity (production) or connection strings (development)
- **Blob access**: Indexer uses connection string or managed identity
- **Foundry access**: Backend uses Azure AI Projects SDK with managed identity or API key

### Observability
- **Indexer Monitoring**: 
  - Check indexer execution history in Azure Portal
  - Set up alerts for indexer failures
  - Track document processing success rate
- **Foundry Monitoring**:
  - Track RAG endpoint latency
  - Monitor token usage (handled by Foundry)
  - Log source citations quality
- **Backend Logging**: Serilog already configured
  - Log Foundry requests/responses
  - Log session creation and expiration
- **Metrics**: Track end-to-end chat latency, citation relevance

### Cost Management
- **Azure AI Search**: Use Basic tier for development; Standard for production
- **Azure AI Foundry**: Monitor token usage (embeddings + chat handled by Foundry)
- **Blob Storage**: Use cool tier for infrequently accessed documents
- **Indexer**: Schedule frequency balances freshness vs. cost (5-15 min intervals)

### Backend Simplification (Maximum Simplification with Foundry)
**What Backend Does**:
- Forward chat requests to Azure AI Foundry RAG endpoint
- Maintain conversation sessions (in-memory)
- Format Foundry responses for frontend
- Return responses with source citations

**What Backend Does NOT Do**:
- Extract text from PDFs (handled by indexer)
- Chunk documents (handled by skillset)
- Generate embeddings (handled by Foundry for queries, skillset for indexing)
- Query search index directly (handled by Foundry)
- Assemble prompts (handled by Foundry)
- Call Azure OpenAI directly (handled by Foundry)
- Monitor blob storage (handled by indexer)
- Handle retries (handled by indexer)
- Track processing status (handled by indexer execution history)

---

## Summary of Decisions

| Area | Decision | Key Rationale |
|------|----------|---------------|
| **Document Processing** | Azure AI Search Indexer + Skillset | Fully managed, no backend code, Azure-native |
| **Blob Monitoring** | Indexer schedule or change detection | No Event Grid, no webhooks, automatic polling |
| **Chunking** | Text Split Skill (512-1024 tokens) | Built-in skill, configurable chunk size |
| **Embedding** | Azure OpenAI Embedding Skill | Integrated into skillset pipeline |
| **RAG Orchestration** | Azure AI Foundry RAG Endpoint | Single endpoint call handles entire pipeline |
| **Deduplication** | Blob path + indexer change detection | Built-in, no custom hashing needed |
| **Retry Logic** | Indexer built-in retry | Automatic exponential backoff, no custom code |
| **Session Management** | In-memory + background cleanup | Simple, fast, sufficient for 10 concurrent users |
| **Session Timeout** | 30 minutes sliding expiration | Balances user convenience with resource conservation |
| **Backend Role** | Session management + Foundry forwarding | No processing, no search, no prompt assembly |

---

## Open Questions / Future Enhancements

**Resolved During Research**:
- ✅ How to chunk insurance documents? → 512-1024 tokens (handled by AI Search skillset)
- ✅ How to deduplicate PDFs? → Blob path + indexer change detection (no custom hashing)
- ✅ How to trigger processing? → Indexer monitoring on schedule or change detection (no Event Grid)
- ✅ How long to keep sessions? → 30-minute sliding expiration
- ✅ How many retries? → Indexer built-in exponential backoff (no custom retry code)
- ✅ Who orchestrates RAG? → Azure AI Foundry RAG endpoint (backend only forwards requests)

**Deferred for Future**:
- Multi-language document support (current: English only)
- Persistent session storage for conversation history (current: in-memory)
- Advanced chunking strategies via custom skillset skills (current: token-based splitting)
- Circuit breaker patterns for Foundry API calls
- Custom prompt flows in Azure AI Foundry for specialized RAG behavior
- Content-based deduplication using blob metadata hashes (current: path-based)

---

## Next Phase

Proceed to **Phase 1: Data Model & Contracts** to define:
- Session entities with expiration (ChatSession, Message)
- Source citation DTOs for Foundry responses
- API contract extensions for chat, summarization, and comparison
- Foundry RAG request/response models

**Note**: Document processing entities (Document, DocumentSection, chunks, embeddings) are NOT defined in backend - these are managed entirely by Azure AI Search Indexer.
