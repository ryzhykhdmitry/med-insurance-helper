# Data Model: Intelligent Document Processing and Retrieval

**Feature**: 003-foundry-integration | **Date**: May 27, 2026

## Overview

This document defines the data entities and relationships for the chat system. Document processing is handled entirely by Azure AI Search indexer + skillset, so the backend only manages conversation sessions and queries the search index. No document processing state tracking is needed in the backend.

---

## Core Entities

### 1. SourceCitation (Read-Only from Azure AI Search)

**Purpose**: Represents a document chunk retrieved from the search index and cited in responses.

**Properties** (Mapped from search index):

| Property | Type | Description | Source |
|----------|------|-------------|--------|
| `DocumentId` | `string` | Unique document identifier (blob path) | Search index `id` field |
| `Content` | `string` | Chunk text content | Search index `content` field |
| `FileName` | `string` | Original PDF filename | Search index `metadata_storage_name` |
| `PageNumber` | `int?` | Source page in PDF | Search index custom field |
| `ChunkIndex` | `int` | Position within document | Search index custom field |
| `Score` | `double` | Search relevance score | Search API response |

**Notes**:
- This is NOT a backend entity; it's a DTO mapped from search results
- Search index schema defined in Azure AI Search, not in backend code
- Backend queries index and maps results to this model

---

### 2. ConversationSession

**Purpose**: Maintains chat context and conversation history for multi-turn interactions.

**Properties**:

| Property | Type | Description | Constraints |
|----------|------|-------------|-------------|
| `SessionId` | `string` (GUID) | Unique session identifier | Primary key |
| `CreatedAt` | `DateTime` (UTC) | Session creation timestamp | Immutable |
| `LastActiveAt` | `DateTime` (UTC) | Last message timestamp | Updated on every interaction |
| `ExpiresAt` | `DateTime` (UTC) | Calculated expiration time | `LastActiveAt + 30 minutes` |
| `Messages` | `List<Message>` | Conversation history | In-memory collection |

**Relationships**:
- One Session → Many Messages (1:N, in-memory only)

**Lifecycle**:
- Created on first user message
- Updated on every interaction (sliding expiration)
- Removed by background cleanup worker when `ExpiresAt < UtcNow`

**Notes**:
- In-memory storage only (no persistence for learning project)
- Messages NOT persisted to database
- Lost on application restart

---

### 3. Message

**Purpose**: Represents a single message in a conversation (user query or assistant response).

**Properties**:

| Property | Type | Description | Constraints |
|----------|------|-------------|-------------|
| `Role` | `MessageRole` (enum) | Sender: User, Assistant | User, Assistant |
| `Content` | `string` | Message text | Max 2000 chars for user, 1500 for assistant |
| `Timestamp` | `DateTime` (UTC) | When message was created | Immutable |
| `SourceCitations` | `List<SourceCitation>?` | Document chunks cited in response | Nullable, assistant messages only |

**Relationships**:
- Many Messages → One ConversationSession (N:1, in-memory)

**Notes**:
- System messages (prompts) not stored in conversation history
- `SourceCitations` enables citation tracking for response verification

---

### 4. FoundryRagRequest

**Purpose**: Represents a request sent to Azure AI Foundry RAG endpoint for retrieval-augmented generation.

**Properties**:

| Property | Type | Description | Constraints |
|----------|------|-------------|-------------|
| `UserQuery` | `string` | Current user question | Required, max 2000 chars |
| `ConversationHistory` | `List<Message>` | Previous messages for context | Max 20 messages |
| `RetrievalParameters` | `RetrievalConfig` | Search configuration | See RetrievalConfig below |

**RetrievalConfig Properties**:

| Property | Type | Description | Default |
|----------|------|-------------|---------|
| `TopK` | `int` | Number of chunks to retrieve | 5 |
| `SearchMode` | `string` | Search type: "hybrid", "vector", "text" | "hybrid" |
| `MinRelevanceScore` | `double?` | Minimum relevance threshold | null |

**Notes**:
- DTO constructed by backend before forwarding to Foundry
- Conversation history enables multi-turn context
- Foundry handles embedding generation and search execution

---

### 5. FoundryRagResponse

**Purpose**: Represents the response returned from Azure AI Foundry RAG endpoint after retrieval-augmented generation.

**Properties**:

| Property | Type | Description | Constraints |
|----------|------|-------------|-------------|
| `GeneratedAnswer` | `string` | LLM-generated response text | Max 1500 chars (truncated if longer) |
| `SourceCitations` | `List<SourceCitation>` | Retrieved document chunks used in generation | 0-5 items (based on TopK) |
| `TokenUsage` | `TokenMetadata?` | Token consumption details | Nullable |
| `ResponseTimestamp` | `DateTime` (UTC) | When response was generated | Immutable |

**TokenMetadata Properties**:

| Property | Type | Description |
|----------|------|-------------|
| `PromptTokens` | `int` | Tokens in prompt (including retrieved context) |
| `CompletionTokens` | `int` | Tokens in generated response |
| `TotalTokens` | `int` | Sum of prompt + completion tokens |

**Relationships**:
- One FoundryRagResponse → Many SourceCitation citations (1:N)
- FoundryRagResponse → FoundryRagRequest (1:1, request-response pair)

**Notes**:
- DTO mapped from Foundry API response by backend
- `SourceCitations` populated by Foundry with relevance scores
- `TokenUsage` useful for monitoring costs and performance
- Backend appends this response to conversation session as Message with role=Assistant

---

## Enums

### MessageRole

**Purpose**: Identifies message sender in conversation.

| Value | Description |
|-------|-------------|
| `User` | Message from end user |
| `Assistant` | Response from AI system |

---

## Relationships Diagram

```
Azure AI Search Index (Azure-managed)
  ├── Documents (blob_uri as key)
  ├── Chunks (text content + embeddings)
  └── Metadata (filename, page numbers)
        ↑
        │ (Foundry queries during RAG)
        │
        └── SourceCitation (DTO, read-only)
                  ↓
            FoundryRagResponse
                  ├── GeneratedAnswer
                  ├── SourceCitations (List<SourceCitation>)
                  └── TokenUsage
                        ↑
                        │ (Backend forwards)
                        │
                  FoundryRagRequest
                  ├── UserQuery
                  ├── ConversationHistory
                  └── RetrievalParameters
                        ↑
                        │ (Backend constructs)
                        │
ConversationSession (in-memory, backend-managed)
  ├── SessionId
  ├── LastActiveAt (sliding expiration: +30 min)
  └── Messages (N, in-memory list)
        ├── Role (User | Assistant)
        ├── Content
        └── SourceCitations (List<SourceCitation>)

[Background Cleanup Worker]
  └── Scans sessions every 10 min, removes if ExpiresAt < UtcNow
```

---

## Data Validation Rules

### SourceCitation
- Read-only; validation performed by Azure AI Search
- Backend maps but does not modify

### ConversationSession
- `LastActiveAt`: Must be >= `CreatedAt`
- `ExpiresAt`: Must be = `LastActiveAt + 30 minutes`
- `Messages`: Max 20 messages retained (oldest trimmed to fit token budget)

### Message
- `Content`: Must be non-empty
- `Role`: User messages precede Assistant messages (alternating pattern)
- `SourceCitations`: Only set for Assistant messages

### FoundryRagRequest
- `UserQuery`: Must be non-empty, max 2000 characters
- `ConversationHistory`: Max 20 messages (truncate oldest if exceeded)
- `RetrievalParameters.TopK`: Must be between 1 and 10
- `RetrievalParameters.SearchMode`: Must be one of: "hybrid", "vector", "text"

### FoundryRagResponse
- `GeneratedAnswer`: May be empty if no relevant information found
- `SourceCitations`: Empty list is valid (no relevant documents found)
- `TokenUsage`: Optional; may be null if not provided by Foundry

---

## Storage Strategy

### Search Index (Azure AI Search)
**Ownership**: Azure AI Search service
- Indexer creates and maintains index schema
- Backend queries but does not modify
- Skillset populates fields (content, embeddings, metadata)

**Index Schema** (Configured in Azure AI Search):
```json
{
  "name": "insurance-documents",
  "fields": [
    { "name": "id", "type": "Edm.String", "key": true },
    { "name": "content", "type": "Edm.String", "searchable": true },
    { "name": "contentVector", "type": "Collection(Edm.Single)", "vectorSearchDimensions": 1536 },
    { "name": "metadata_storage_name", "type": "Edm.String", "filterable": true },
    { "name": "metadata_storage_path", "type": "Edm.String", "filterable": true },
    { "name": "pageNumber", "type": "Edm.Int32", "filterable": true },
    { "name": "chunkIndex", "type": "Edm.Int32", "sortable": true }
  ]
}
```

### Sessions (In-Memory)
**Storage**: In-memory `Dictionary<string, ConversationSession>`
- Already implemented in `SessionService`
- Background worker cleans up expired sessions
- No persistence required for learning project

### Blobs (Azure Blob Storage)
**Storage**: Azure Blob Storage
- PDFs stored in dedicated container (e.g., `insurance-docs`)
- Indexer monitors container automatically
- Optional: Archive processed blobs to cool tier after indexing

---

## Session Expiration Algorithm

**Objective**: Remove inactive sessions to conserve memory.

**Implementation**:

```
Background Worker (runs every 10 minutes):
1. Lock session dictionary
2. currentTime = DateTime.UtcNow
3. expiredSessions = sessions.Where(s => s.ExpiresAt < currentTime)
4. foreach session in expiredSessions:
   a. Log session expiration
   b. Remove from dictionary
5. Unlock dictionary
```

**Sliding Window**:
- Every `GetSession()` or `AppendMessage()` call:
  - Update `LastActiveAt = DateTime.UtcNow`
  - Recalculate `ExpiresAt = LastActiveAt + TimeSpan.FromMinutes(30)`
- Ensures active conversations never expire

---

## Query Patterns

### Query Search Index
```csharp
var searchClient = new SearchClient(endpoint, "insurance-documents", credential);
var searchOptions = new SearchOptions
{
    VectorSearch = new VectorSearchOptions
    {
        Queries = { new VectorizedQuery(queryEmbedding) { KNearestNeighborsCount = 5 } }
    },
    Select = { "id", "content", "metadata_storage_name", "pageNumber", "chunkIndex" },
    Top = 5
};

var results = await searchClient.SearchAsync<SearchResult>(query, searchOptions);
```

### Get Active Sessions (for cleanup)
```csharp
var expired = _sessions.Values
    .Where(s => s.ExpiresAt < DateTime.UtcNow)
    .Select(s => s.SessionId)
    .ToList();
```

---

## Migration from Existing Models

**Current State**: Project has `DocumentChunk`, `ConversationSession`, `Message` models.

**Changes**:
- **Remove**: `DocumentChunk` (replaced by Azure AI Search index)
- **Keep**: `ConversationSession` - extend with `ExpiresAt` property
- **Keep**: `Message` - add optional `SourceDocuments` property
- **Add**: `SearchResult` DTO for mapping search index results

**Migration Steps**:
1. Remove `DocumentChunk` entity (no longer needed)
2. Add `SearchResult` DTO for search result mapping
3. Add `ExpiresAt` to `ConversationSession`
4. Add `SourceDocuments` to `Message`
5. Remove any document processing state tracking (ProcessingStatus, RetryCount, etc.)

---

## Next Steps

Proceed to **contracts/api.md** to define:
- API endpoints for chat with search integration
- Session management endpoints
- Search index query patterns (no document upload/processing endpoints needed)
