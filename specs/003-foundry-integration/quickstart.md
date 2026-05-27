# Developer Quickstart: Intelligent Document Processing and Retrieval

**Feature**: 003-foundry-integration | **Date**: May 27, 2026

## Overview

This guide helps developers set up a local environment for developing and testing the document processing and chat features. Covers Azure emulators, service configuration, and end-to-end testing workflows.

---

## Prerequisites

**Required**:
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli) (for cloud provisioning)
- [Node.js 18+](https://nodejs.org/) (for Angular frontend)
- Azure subscription with permissions to create resources (Blob Storage, AI Search, AI Foundry)

**Optional**:
- [Azure Storage Explorer](https://azure.microsoft.com/features/storage-explorer/) (GUI for blob upload)
- [ngrok](https://ngrok.com/) (for local Event Grid webhook testing)
- [Postman](https://www.postman.com/) or [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) (API testing)

---

## Architecture Diagram

```
┌─────────────────┐
│  Azure Blob     │ ◄──── PDF Upload ──── User (Storage Explorer / CLI)
│  Storage        │
└────────┬────────┘
         │
         │ Monitored by
         ▼
┌─────────────────┐
│  AI Search      │ ──► Pulls PDFs automatically
│  Indexer        │
└────────┬────────┘
         │
         │ Executes
         ▼
┌─────────────────┐
│  Skillset       │ ──► Document cracking → Text split → Embedding
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Vector Index   │ ◄─────────────────┐
│  (AI Search)    │                   │
└─────────────────┘                   │
                                      │
┌─────────────────┐                   │ RAG Pipeline:
│ Azure AI        │ ──► Query Embed   │ • Embed query
│ Foundry         │ ──► Search Index ─┘ • Retrieve chunks
│ (RAG Endpoint)  │ ──► Assemble Prompt  • Build prompt
│                 │ ──► Generate Answer  • Call LLM
└────────┬────────┘                      • Return citations
         ▲
         │ Forward request
         │
┌────────┴────────┐
│   .NET API      │ ◄──── User Query
│  (/api/chat)    │
└─────────────────┘
```

**Key Points**: 
- Backend does NOT process documents (handled by AI Search indexer + skillset)
- Backend does NOT orchestrate RAG (handled by Azure AI Foundry)
- Backend only manages sessions and forwards requests

---

## Quick Start (5 Steps)

### Step 1: Clone and Restore

```powershell
# Clone repository (if not already done)
git clone <repo-url>
cd med-insurance-helper

# Checkout feature branch
git checkout 003-foundry-integration

# Restore .NET dependencies
cd src/backend/MedInsuranceHelper.Api
dotnet restore

# Restore frontend dependencies
cd ../../frontend
npm install
```

---

### Step 2: Provision Azure Cloud Resources

**Run Provisioning Scripts**:

```powershell
# Navigate to scripts directory
cd scripts

# Set variables (customize as needed)
$resourceGroup = "med-insurance-rg"
$location = "eastus"
$storageName = "medinsurancestorage"  # Must be globally unique
$searchName = "medinsurance-search"
$openAIName = "medinsurance-openai"

# Login to Azure
az login

# Create resource group
az group create --name $resourceGroup --location $location

# Provision Blob Storage
az storage account create `
  --name $storageName `
  --resource-group $resourceGroup `
  --location $location `
  --sku Standard_LRS

# Create container
az storage container create `
  --name insurance-docs `
  --account-name $storageName

# Provision Azure AI Search (Basic tier minimum for skillsets + vector search)
az search service create `
  --name $searchName `
  --resource-group $resourceGroup `
  --sku basic `
  --location $location

# Provision Azure OpenAI
az cognitiveservices account create `
  --name $openAIName `
  --resource-group $resourceGroup `
  --kind OpenAI `
  --sku S0 `
  --location eastus

# Deploy embedding model (used by skillset)
az cognitiveservices account deployment create `
  --name $openAIName `
  --resource-group $resourceGroup `
  --deployment-name text-embedding-ada-002 `
  --model-name text-embedding-ada-002 `
  --model-version "2" `
  --model-format OpenAI `
  --scale-capacity 10

# Deploy chat model (used by backend API)
az cognitiveservices account deployment create `
  --name $openAIName `
  --resource-group $resourceGroup `
  --deployment-name gpt-35-turbo `
  --model-name gpt-35-turbo `
  --model-version "0613" `
  --model-format OpenAI `
  --scale-capacity 10
```

**Configure AI Search Indexer + Skillset** (see scripts/setup-search-indexer.ps1):

The indexer will be configured to:
1. Monitor the `insurance-docs` blob container
2. Run every 5-15 minutes (or on-demand)
3. Execute skillset pipeline:
   - Document extraction (built-in PDF cracking)
   - Text splitting (512-1024 token chunks)
   - Azure OpenAI embedding generation (via skillset, not backend)
   - Vector index population

**Configure Azure AI Foundry Project** (see scripts/setup-foundry-project.ps1):

The Foundry project will be configured to:
1. Connect to Azure AI Search index (`insurance-documents`)
2. Configure RAG endpoint with retrieval parameters (top 5 chunks, hybrid search)
3. Deploy or link Azure OpenAI models (`text-embedding-ada-002`, `gpt-35-turbo`)
4. Set up prompt flow for RAG orchestration (optional advanced customization)

**No Event Grid setup needed** - indexer monitors blob storage automatically.

---

### Step 3: Configure Application Settings

**Edit** `src/backend/MedInsuranceHelper.Api/appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AzureFoundry": {
    "ProjectEndpoint": "https://{your-foundry-project}.cognitiveservices.azure.com/",
    "ApiKey": "{your-foundry-api-key}",
    "RagDeploymentName": "rag-deployment",
    "MaxRetrievalResults": 5
  },
  "SessionManagement": {
    "InactivityTimeoutMinutes": 30,
    "CleanupIntervalMinutes": 10,
    "MaxMessagesPerSession": 20
  }
}
```

**Get Azure Credentials**:

```powershell
# Get Foundry Project API key
# (Navigate to Azure Portal → AI Foundry Project → Keys and Endpoint)
# Or use managed identity for production deployments
```

---

### Step 4: Run Application

**Terminal 1 - Backend API**:
```powershell
cd src/backend/MedInsuranceHelper.Api
dotnet run
# API runs at https://localhost:5001
```

**Terminal 2 - Frontend**:
```powershell
cd src/frontend
npm start
# UI runs at http://localhost:4200
```

---

## Development Workflow

### Workflow A: Document Upload (Automatic Indexing)

**Azure AI Search indexer handles processing automatically**:

1. **Upload PDF to Blob Storage**:
   ```powershell
   # Using Azure CLI
   az storage blob upload `
     --account-name medinsurancestorage `
     --container-name insurance-docs `
     --name alpha-health-plan.pdf `
     --file ./docs/samples/alpha-health-plan.pdf
   ```

2. **Indexer Automatically Processes**:
   - Indexer detects new blob (runs every 5-15 min or on change detection)
   - Executes skillset: extract text → chunk → embed → index
   - No backend API involvement needed

3. **Check Indexer Status** (Azure Portal or API):
   - Navigate to: Azure Portal → AI Search → Indexers → View execution history
   - Look for document in index: Azure Portal → AI Search → Indexes → Search explorer

4. **Query via Chat**:
   - Open frontend: http://localhost:4200
   - Ask: "What is the deductible for Alpha Health Plan?"
   - Backend queries search index and returns answer with citations

**No manual processing trigger needed** - everything is automatic!

---

## Testing Scenarios

### Test 1: Document Indexing

**Objective**: Verify Azure AI Search indexer processes uploaded PDFs automatically.

1. Upload `alpha-health-plan.pdf` to blob storage
2. Wait 5-15 minutes for indexer to run (or trigger manually in Azure Portal)
3. Check indexer execution history in Azure Portal → AI Search → Indexers
4. **Expected**: Indexer status shows "Success", document appears in search index

---

### Test 2: Document Deduplication

**Objective**: Verify indexer handles re-uploads correctly.

1. Upload `alpha-health-plan.pdf`
2. Wait for indexing to complete
3. Upload same file again with same name
4. **Expected**: Indexer updates existing document (same blob path); no duplicate chunks created

---

### Test 3: Session Expiration

**Objective**: Verify 30-minute sliding expiration.

1. Start chat session, send message "Hello"
2. Note `sessionId` and `expiresAt` from response
3. Wait 15 minutes, send another message
4. **Expected**: `expiresAt` updated to +30min from current time
5. Wait 35 minutes without activity
6. Send message with old session ID
7. **Expected**: `404 Session Expired`, new session created

---

### Test 4: Question Answering with Citations

**Objective**: Verify RAG pipeline returns accurate answers with search relevance scores.

1. Upload 3 sample insurance PDFs
2. Wait for indexer to process all documents
3. Verify documents in search index: Azure Portal → AI Search → Search explorer
4. Ask: "What are the coverage percentages for each plan?"
5. **Expected**:
   - Response includes coverage data from multiple documents
   - `sourceCitations` array lists chunks with file names and page numbers
   - Search relevance scores included (0.0 - 1.0)

---

## Debugging Tips

### Check Blob Storage

```powershell
# List blobs in container
az storage blob list `
  --account-name medinsurancestorage `
  --container-name insurance-docs `
  --output table
```

### Check Azure AI Search Index

```powershell
# Get document count in index
curl "https://{search-name}.search.windows.net/indexes/insurance-documents/docs/\$count?api-version=2023-11-01" `
  -H "api-key: {admin-key}"

# Query index directly
curl "https://{search-name}.search.windows.net/indexes/insurance-documents/docs/search?api-version=2023-11-01" `
  -H "Content-Type: application/json" `
  -H "api-key: {admin-key}" `
  -d '{"search": "*", "top": 5}'
```

### View Processing Logs

```powershell
# Watch API logs in real-time
cd src/backend/MedInsuranceHelper.Api
dotnet watch run

# Filter for Foundry RAG calls
dotnet watch run | Select-String "Foundry"
```

### Test Azure AI Foundry Connectivity

```powershell
# Test Foundry RAG endpoint (using Azure AI Projects SDK or REST API)
# See Azure Portal → AI Foundry Project → Playground for testing
# Or use backend /api/chat endpoint to test end-to-end
```

---

## Common Issues & Solutions

### Issue: Indexer Not Running

**Error**: Documents uploaded but not appearing in search index after 15+ minutes.

**Solution**: Check indexer status and execution history:

```powershell
# Check indexer status
az search indexer show `
  --name pdf-indexer `
  --service-name medinsurance-search `
  --resource-group med-insurance-rg

# View execution history (in Azure Portal)
# Navigate to: AI Search → Indexers → pdf-indexer → Execution History

# Manually trigger indexer
az search indexer run `
  --name pdf-indexer `
  --service-name medinsurance-search `
  --resource-group med-insurance-rg
```

Common causes:
- Indexer disabled (Status = "disabled")
- Blob storage connection string incorrect
- Skillset referencing wrong Azure OpenAI endpoint

---

### Issue: Skillset Embedding Failures

**Error**: Indexer runs but execution history shows "Skillset execution failed".

**Solution**: Verify Azure OpenAI configuration in skillset definition:

```powershell
# Check skillset definition
az search skillset show `
  --name pdf-skillset `
  --service-name medinsurance-search `
  --resource-group med-insurance-rg

# Verify Azure OpenAI deployment exists
az cognitiveservices account deployment show `
  --name medinsurance-openai `
  --resource-group med-insurance-rg `
  --deployment-name text-embedding-ada-002
```

Common causes:
- Azure OpenAI endpoint/API key incorrect in skillset
- Embedding model not deployed
- Azure OpenAI quota exceeded (check quota usage in Portal)

---

### Issue: OpenAI "429 Rate Limit Exceeded"

**Solution**:
- Reduce concurrent requests
- Check OpenAI deployment quota (tokens per minute)
- Upgrade to higher tier if needed

---

### Issue: Search Index "Not Found"

**Solution**: Create index via API on first run:

```csharp
// Auto-create in startup or service initialization
var indexClient = new SearchIndexClient(endpoint, credential);
if (!indexClient.GetIndex("insurance-documents").Exists())
{
    var index = new SearchIndex("insurance-documents")
    {
        Fields = { /* define schema */ }
    };
    indexClient.CreateIndex(index);
}
```

---

## Performance Benchmarks

**Expected Timings** (on Basic tier Azure services with indexer):

| Operation | Target | Notes |
|-----------|--------|-------|
| PDF Upload (5MB) | <5 seconds | To blob storage only |
| Indexer Run (detect new blob) | 5-15 minutes | Based on indexer schedule |
| Skillset Processing (20 pages) | <3 minutes | Extract → Chunk → Embed → Index |
| **Total Time to Searchable** | <20 minutes | From upload to queryable |
| Chat Query (5 doc retrieval) | <5 seconds | Vector search + LLM response |
| Document Comparison (3 docs) | <10 seconds | Multi-doc retrieval + LLM |
| Session Cleanup (100 sessions) | <1 second | Background worker |

**Note**: Manual indexer trigger reduces detection delay to <30 seconds.

---

## Sample cURL Commands

**Upload Document to Blob Storage** (indexer processes automatically):
```bash
# Use Azure CLI instead of API
az storage blob upload \
  --account-name medinsurancestorage \
  --container-name insurance-docs \
  --name alpha-health-plan.pdf \
  --file ./docs/samples/alpha-health-plan.pdf
```

**Send Chat Message**:
```bash
curl -X POST https://localhost:5001/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "What is the deductible?",
    "sessionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
  }'
```

**List Documents in Index**:
```bash
curl https://localhost:5001/api/search/documents?limit=10
```

**Request Document Summary**:
```bash
curl -X POST https://localhost:5001/api/search/summarize \
  -H "Content-Type: application/json" \
  -d '{
    "documentPath": "/insurance-docs/alpha-health-plan.pdf",
    "maxWords": 500
  }'
```

**Compare Documents**:
```bash
curl -X POST https://localhost:5001/api/search/compare \
  -H "Content-Type: application/json" \
  -d '{
    "documentPaths": [
      "/insurance-docs/alpha-health-plan.pdf",
      "/insurance-docs/beta-care-plan.pdf",
      "/insurance-docs/gamma-premium-plan.pdf"
    ],
    "criteria": ["deductible", "coverage", "premium"]
  }'
```

---

## Next Steps

1. **Create Azure AI Foundry Project**: Configure RAG endpoint with connection to AI Search index
2. **Create Azure AI Search Indexer + Skillset**: Follow `setup-search-indexer.ps1` script template
3. **Implement backend services**:
   - `FoundryRagService` (call Foundry RAG endpoint)
   - `SessionService` (in-memory with expiration and cleanup worker)
4. **Implement API endpoints** defined in `contracts/api.md`:
   - `/api/chat` (forward to Foundry RAG endpoint)
   - `/api/search/summarize` (retrieve chunks + call Foundry)
   - `/api/search/compare` (retrieve chunks + call Foundry)
   - `/api/search/documents` (diagnostic)
5. **Add background worker**: `SessionCleanupWorker` for expired session cleanup
6. **Test end-to-end**: Follow testing scenarios above
7. **Run `/speckit.tasks`** to generate detailed implementation tasks

---

## Resources

- [Azure AI Search Documentation](https://docs.microsoft.com/azure/search/)
- [Azure AI Foundry Documentation](https://docs.microsoft.com/azure/ai-services/)
- [Azure OpenAI Service Documentation](https://docs.microsoft.com/azure/ai-services/openai/)
- [Azure Blob Storage Documentation](https://docs.microsoft.com/azure/storage/blobs/)
- [Project README](../../README.md)
