# API Contracts: Intelligent Document Processing and Retrieval

**Feature**: 003-foundry-integration | **Date**: May 27, 2026

## Overview

This document defines the REST API contract extensions for chat functionality with Azure AI Search integration. Document processing is handled entirely by Azure AI Search indexer + skillset, so there are no document upload or processing endpoints. The API only handles chat queries and session management.

---

## Base URL

```
https://localhost:5001/api  (Development)
https://{your-app}.azurewebsites.net/api  (Production)
```

---

## Authentication

**Development**: None (local testing)
**Production**: Azure AD / API Key (TBD based on deployment)

All endpoints return standard HTTP status codes:
- `200 OK` - Success
- `400 Bad Request` - Invalid input
- `404 Not Found` - Resource not found
- `500 Internal Server Error` - Processing failure

---

## 1. Chat Endpoints

### POST `/api/chat`

**Purpose**: Send a user message and receive AI-generated response with document citations from Azure AI Search.

**Headers**:
```
Content-Type: application/json
X-Session-Id: {optional-session-guid}
```

**Request Body**:
```json
{
  "message": "What is the deductible for the Alpha Health Plan?",
  "sessionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

**Response**:
```json
{
  "sessionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "message": "The Alpha Health Plan has a $1,500 annual deductible for individual coverage and $3,000 for family coverage.",
  "sourceDocuments": [
    {
      "documentId": "/insurance-docs/alpha-health-plan.pdf",
      "fileName": "alpha-health-plan.pdf",
      "content": "Annual deductibles: Individual $1,500, Family $3,000...",
      "pageNumber": 3,
      "score": 0.92
    }
  ],
  "timestamp": "2026-05-27T10:45:30Z",
  "expiresAt": "2026-05-27T11:15:30Z"
}
```

**Status Codes**:
- `200 OK`: Success
- `400 Bad Request`: Empty message or invalid session ID
- `404 Not Found`: Session expired
- `503 Service Unavailable`: Azure AI Foundry unavailable

**Behavior**:
1. If `sessionId` provided:
   - Retrieve existing session
   - If expired, return `404 Not Found` with message "Session expired"
   - Append user message to conversation history
   - Update `LastActiveAt`, recalculate `ExpiresAt`
2. If `sessionId` omitted or invalid:
   - Create new session
   - Generate new session GUID
3. Forward request to **Azure AI Foundry RAG endpoint** with:
   - User message
   - Conversation history (up to 20 messages)
   - Retrieval parameters (top 5 chunks)
4. **Foundry orchestrates entire RAG pipeline**:
   - Generates query embedding
   - Queries Azure AI Search vector index (hybrid search)
   - Retrieves top 5 relevant document chunks
   - Assembles prompt with retrieved context
   - Calls Azure OpenAI for response generation
   - Returns answer with source citations
5. Backend receives Foundry response and appends to session
6. Return response with session ID and expiration

**Notes**:
- Maximum message length: 2000 characters
- Maximum conversation history: 20 messages (oldest trimmed)
- Response includes `expiresAt` to inform UI of session timeout
- Citations include search relevance score for transparency (provided by Foundry)
- Backend does NOT generate embeddings, search, or assemble prompts - Foundry handles all of this

---

### GET `/api/sessions/{id}`

**Purpose**: Check if a session is still active and retrieve conversation metadata.

**Path Parameters**:
- `id`: Session GUID

**Response**:
```json
{
  "sessionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "createdAt": "2026-05-27T10:30:00Z",
  "lastActiveAt": "2026-05-27T10:45:30Z",
  "expiresAt": "2026-05-27T11:15:30Z",
  "messageCount": 8
}
```

**Status Codes**:
- `200 OK`: Session active
- `404 Not Found`: Session expired or never existed

**Behavior**:
- Lookup session in in-memory dictionary
- If found and `ExpiresAt > UtcNow`: Return metadata
- Otherwise: Return `404 Not Found`

**Notes**:
- This endpoint does NOT extend session expiration (read-only)
- Use for UI to check session validity before sending message

---

### DELETE `/api/sessions/{id}`

**Purpose**: Explicitly end a conversation session (user logout, clear history).

**Path Parameters**:
- `id`: Session GUID

**Response**:
```
204 No Content
```

**Status Codes**:
- `204 No Content`: Session deleted
- `404 Not Found`: Session not found (already expired or invalid)

**Behavior**:
- Remove session from in-memory dictionary
- Log session deletion
- Return success regardless of whether session existed

---

## 2. Document Summarization Endpoint

### POST `/api/search/summarize`

**Purpose**: Generate a concise summary of a document by querying all its chunks (User Story 4).

**Request Body**:
```json
{
  "documentPath": "/insurance-docs/alpha-health-plan.pdf",
  "maxWords": 500
}
```

**Response**:
```json
{
  "documentPath": "/insurance-docs/alpha-health-plan.pdf",
  "fileName": "alpha-health-plan.pdf",
  "summary": "The Alpha Health Plan is a comprehensive medical insurance policy offering coverage for...",
  "wordCount": 487,
  "generatedAt": "2026-05-27T10:50:00Z"
}
```

**Status Codes**:
- `200 OK`: Summary generated
- `404 Not Found`: Document not found in search index
- `503 Service Unavailable`: Azure AI Foundry unavailable

**Behavior**:
1. Query Azure AI Search for all chunks of the specified document (filter by `metadata_storage_path`)
2. Retrieve all chunks ordered by `chunkIndex`
3. Send concatenated content to Azure AI Foundry with summarization prompt
4. Return generated summary

**Notes**:
- No document processing status check needed (indexer handles this)
- If document not in index, it hasn't been processed yet
- Optional: Cache summary for repeated requests
- Foundry handles LLM interaction for summary generation

---

## 3. Document Comparison Endpoint

### POST `/api/search/compare`

**Purpose**: Compare multiple documents and identify differences (User Story 5).

**Request Body**:
```json
{
  "documentPaths": [
    "/insurance-docs/alpha-health-plan.pdf",
    "/insurance-docs/beta-care-plan.pdf",
    "/insurance-docs/gamma-premium-plan.pdf"
  ],
  "criteria": ["deductible", "coverage", "premium", "exclusions"]
}
```

**Response**:
```json
{
  "comparisonId": "comp-12345",
  "documentCount": 3,
  "criteria": ["deductible", "coverage", "premium", "exclusions"],
  "results": {
    "deductible": {
      "alpha-health-plan": "$1,500 individual / $3,000 family",
      "beta-care-plan": "$2,000 individual / $4,000 family",
      "gamma-premium-plan": "$500 individual / $1,000 family"
    },
    "coverage": {
      "alpha-health-plan": "80% after deductible",
      "beta-care-plan": "70% after deductible",
      "gamma-premium-plan": "90% after deductible"
    }
  },
  "summary": "Gamma Premium Plan offers the lowest deductible and highest coverage percentage, while Beta Care Plan has the highest deductible.",
  "generatedAt": "2026-05-27T10:55:00Z"
}
```

**Status Codes**:
- `200 OK`: Comparison generated
- `400 Bad Request`: Less than 2 document paths provided
- `404 Not Found`: One or more documents not found in search index
- `503 Service Unavailable`: Azure AI Foundry unavailable

**Behavior**:
1. Query Azure AI Search for chunks from each document (filter by `metadata_storage_path`)
2. Retrieve relevant sections from all documents
3. Send to Azure AI Foundry with comparison prompt and criteria
4. Parse structured comparison response
5. Return comparison results

**Notes**:
- Foundry handles LLM interaction for multi-document comparison
- Backend orchestrates document retrieval and result formatting

---

## 4. Search Index Query Endpoint (Diagnostic/Admin)

### GET `/api/search/documents`

**Purpose**: List all documents in the search index (useful for verifying indexer status).

**Query Parameters**:
- `limit` (optional): Max results to return (default: 50, max: 200)
- `skip` (optional): Pagination offset (default: 0)

**Response**:
```json
{
  "documents": [
    {
      "id": "/insurance-docs/alpha-health-plan.pdf",
      "fileName": "alpha-health-plan.pdf",
      "chunkCount": 42,
      "lastModified": "2026-05-27T10:32:00Z"
    },
    {
      "id": "/insurance-docs/beta-care-plan.pdf",
      "fileName": "beta-care-plan.pdf",
      "chunkCount": 38,
      "lastModified": "2026-05-27T10:35:00Z"
    }
  ],
  "total": 25,
  "limit": 50,
  "skip": 0
}
```

**Status Codes**:
- `200 OK`: Success
- `503 Service Unavailable`: Azure AI Search unavailable

**Behavior**:
- Query Azure AI Search with `search=*` to get all documents
- Group by `metadata_storage_path` to get document-level view
- Return aggregated list

**Notes**:
- This endpoint is primarily for debugging/verification
- Shows what documents the indexer has processed
- Does NOT show document processing status (that's managed by Azure AI Search indexer execution history)

---

## Error Response Format

All error responses follow this structure:

```json
{
  "error": {
    "code": "SESSION_EXPIRED",
    "message": "Session a1b2c3d4-e5f6-7890-abcd-ef1234567890 expired or not found",
    "timestamp": "2026-05-27T10:45:30Z",
    "requestId": "req-abc123"
  }
}
```

**Common Error Codes**:
- `INVALID_REQUEST`: Malformed input
- `DOCUMENT_NOT_FOUND`: Document not in search index
- `SESSION_EXPIRED`: Session no longer active
- `SERVICE_UNAVAILABLE`: Azure OpenAI or Azure AI Search unavailable
- `RATE_LIMIT_EXCEEDED`: Too many requests (future enhancement)

---

## Frontend Integration Notes

**No UI Changes Required** per specification. Existing Angular frontend should:

1. **Chat Interface**: Use existing chat component
   - Send messages to `/api/chat` with session ID
   - Display `sourceDocuments` as citations with relevance scores
   - Show `expiresAt` countdown in UI
   - Handle `404 Session Expired` by creating new session

2. **Document List** (Optional Enhancement):
   - Call `/api/search/documents` to show indexed documents
   - No processing status available (handled by Azure indexer)
   - Can check indexer execution history in Azure Portal instead

3. **Session Management**:
   - Store `sessionId` in component state or local storage
   - Include in every `/api/chat` request
   - Clear session ID on timeout or explicit logout

---

## Document Upload Process (Outside API)

**Important**: Backend does NOT handle document uploads. Users upload PDFs directly to Azure Blob Storage using:
- Azure Portal
- Azure Storage Explorer
- Azure CLI (`az storage blob upload`)
- Azure Storage SDK (if building custom uploader)

Azure AI Search indexer automatically detects and processes new documents.
