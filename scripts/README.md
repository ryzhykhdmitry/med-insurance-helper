# Azure Infrastructure Setup Scripts

This directory contains PowerShell scripts for provisioning Azure resources required for the medical insurance chatbot with document processing capabilities.

## Overview

The application uses a fully cloud-managed pipeline for document processing and RAG:

```
Azure Blob Storage (PDFs)
         ↓
   AI Search Indexer (monitors container)
         ↓
   Skillset (text extraction → chunking → embeddings)
         ↓
   Vector Search Index
         ↓
   Azure AI Foundry RAG Endpoint
         ↓
   .NET API (session management + forwarding)
```

## Prerequisites

- **Azure CLI**: [Install Azure CLI](https://aka.ms/installazurecliwindows)
- **Azure Subscription**: With permissions to create resources
- **PowerShell 5.1+**: Built into Windows

## Scripts Execution Order

Run scripts in this order for initial setup:

### 1. Blob Storage Setup

Creates Azure Storage Account and blob container for PDF documents.

```powershell
.\setup-blob-storage.ps1
```

**What it creates:**
- Azure Storage Account (`medinsurancestorage`)
- Blob container (`insurance-docs`)
- Enables change feed for indexer monitoring

**Output:**
- Storage connection string (add to `appsettings.Development.json`)

**Idempotent**: ✅ Safe to run multiple times (skips existing resources)

---

### 2. AI Search Indexer Setup

Configures Azure AI Search with indexer, skillset, and vector index.

```powershell
.\setup-search-indexer.ps1
```

**What it creates:**
- Azure AI Search service (Basic tier)
- Data source connection to blob storage
- Skillset (document cracking → text splitting → embedding generation)
- Vector search index (`insurance-documents`)
- Indexer (runs every 5 minutes, monitors blob container)

**Output:**
- Search endpoint and API key (add to `appsettings.Development.json`)
- Indexer automatically processes uploaded PDFs

**Idempotent**: ✅ Safe to run multiple times (skips existing resources)

---

### 3. Azure AI Foundry RAG Endpoint

Configures Azure AI Foundry project with RAG endpoint.

```powershell
.\setup-foundry-project.ps1
```

**What it provides:**
- Manual setup instructions for Azure AI Foundry (https://ai.azure.com)
- Configuration values for `appsettings.Development.json`
- Alternative approach: Direct Azure OpenAI integration (simpler)

**Note**: Azure AI Foundry setup requires Azure Portal. The script provides step-by-step instructions.

**Idempotent**: ✅ Informational script (no resource creation)

---

## Configuration Parameters

All scripts accept parameters for customization:

```powershell
# Custom resource names
.\setup-blob-storage.ps1 `
    -ResourceGroup "my-rg" `
    -Location "westus" `
    -StorageAccountName "mystorageaccount" `
    -ContainerName "my-docs"

.\setup-search-indexer.ps1 `
    -ResourceGroup "my-rg" `
    -SearchServiceName "my-search" `
    -StorageAccountName "mystorageaccount" `
    -OpenAIServiceName "my-openai"

.\setup-foundry-project.ps1 `
    -ResourceGroup "my-rg" `
    -FoundryProjectName "my-foundry" `
    -SearchServiceName "my-search"
```

**Default values** (if not specified):
- `ResourceGroup`: `med-insurance-rg`
- `Location`: `eastus`
- `StorageAccountName`: `medinsurancestorage`
- `ContainerName`: `insurance-docs`
- `SearchServiceName`: `medinsurance-search`
- `IndexName`: `insurance-documents`
- `OpenAIServiceName`: `medinsurance-openai`

---

## Azure CLI Commands Reference

### Login and Account Management

```powershell
# Login to Azure
az login

# List subscriptions
az account list --output table

# Set active subscription
az account set --subscription "My Subscription"

# Show current account
az account show
```

---

### Blob Storage Operations

```powershell
# Upload a PDF
az storage blob upload `
    --account-name medinsurancestorage `
    --container-name insurance-docs `
    --name alpha-health-plan.pdf `
    --file ./docs/alpha-health-plan.pdf `
    --auth-mode key

# List blobs
az storage blob list `
    --account-name medinsurancestorage `
    --container-name insurance-docs `
    --output table

# Download a blob
az storage blob download `
    --account-name medinsurancestorage `
    --container-name insurance-docs `
    --name alpha-health-plan.pdf `
    --file ./downloads/alpha-health-plan.pdf
```

---

### AI Search Indexer Operations

```powershell
# Check indexer status
az search indexer show `
    --name insurance-docs-indexer `
    --service-name medinsurance-search `
    --resource-group med-insurance-rg

# Run indexer manually
az search indexer run `
    --name insurance-docs-indexer `
    --service-name medinsurance-search `
    --resource-group med-insurance-rg

# Check indexer execution history
az search indexer show-status `
    --name insurance-docs-indexer `
    --service-name medinsurance-search `
    --resource-group med-insurance-rg

# Reset indexer (reprocess all documents)
az search indexer reset `
    --name insurance-docs-indexer `
    --service-name medinsurance-search `
    --resource-group med-insurance-rg
```

---

### Azure OpenAI Operations

```powershell
# List deployments
az cognitiveservices account deployment list `
    --name medinsurance-openai `
    --resource-group med-insurance-rg `
    --output table

# Get endpoint and key
az cognitiveservices account show `
    --name medinsurance-openai `
    --resource-group med-insurance-rg `
    --query "properties.endpoint"

az cognitiveservices account keys list `
    --name medinsurance-openai `
    --resource-group med-insurance-rg
```

---

## Testing the Pipeline

### 1. Upload Test Document

```powershell
# Convert sample .txt to PDF (or use real PDF)
# Sample files are in docs/samples/ but are .txt format

# Upload to blob storage
az storage blob upload `
    --account-name medinsurancestorage `
    --container-name insurance-docs `
    --name test-plan.pdf `
    --file ./docs/test-plan.pdf `
    --auth-mode key
```

### 2. Wait for Indexer Processing

```powershell
# Check indexer status (wait for success)
az search indexer show-status `
    --name insurance-docs-indexer `
    --service-name medinsurance-search `
    --resource-group med-insurance-rg `
    --query "lastResult" `
    --output table

# Typical processing time: 1-3 minutes for first document
```

### 3. Verify Index Population

```powershell
# Query index document count (via REST API)
$searchKey = az search admin-key show `
    --service-name medinsurance-search `
    --resource-group med-insurance-rg `
    --query "primaryKey" `
    --output tsv

Invoke-RestMethod `
    -Uri "https://medinsurance-search.search.windows.net/indexes/insurance-documents/docs/`$count?api-version=2023-11-01" `
    -Headers @{ "api-key" = $searchKey } `
    -Method Get

# Expected output: Number of indexed document chunks
```

### 4. Test Backend API

```powershell
# Start backend API (from repository root)
cd src/backend/MedInsuranceHelper.Api
dotnet run

# In another terminal, send chat request
Invoke-RestMethod `
    -Uri "https://localhost:5001/api/chat" `
    -Method Post `
    -ContentType "application/json" `
    -Body '{"message":"What is the deductible for Alpha Health Plan?"}' `
    -SkipCertificateCheck

# Expected: Response with answer and source citations
```

---

## Troubleshooting

### Indexer Not Running

**Symptom**: Uploaded PDFs not appearing in search index

**Solutions**:
1. Check indexer status for errors:
   ```powershell
   az search indexer show-status --name insurance-docs-indexer --service-name medinsurance-search --resource-group med-insurance-rg
   ```
2. Verify blob container connection string in data source
3. Manually trigger indexer:
   ```powershell
   az search indexer run --name insurance-docs-indexer --service-name medinsurance-search --resource-group med-insurance-rg
   ```

### Skillset Embedding Errors

**Symptom**: Indexer fails with "Azure OpenAI embedding error"

**Solutions**:
1. Verify Azure OpenAI deployment exists: `text-embedding-ada-002`
2. Check OpenAI quota and rate limits
3. Verify OpenAI endpoint and key in skillset configuration

### Search Service Not Found

**Symptom**: "Search service does not exist"

**Solutions**:
1. Verify search service name: `az search service list --resource-group med-insurance-rg`
2. Check resource group: `az group show --name med-insurance-rg`
3. Ensure you're logged into correct Azure subscription

---

## Cost Considerations

**Free tier options** (suitable for learning):
- **Storage Account**: Free tier (5 GB)
- **AI Search**: Basic tier ($75/month, no free tier for vector search)
- **Azure OpenAI**: Pay-per-token (use Azure free credits)

**Estimated monthly cost** (low usage):
- Storage: ~$0-5 (minimal data)
- AI Search Basic: ~$75
- OpenAI: ~$5-20 (depends on query volume)

**Total**: ~$80-100/month for development environment

**To minimize costs**:
- Delete resources when not in use
- Use Azure free credits (new accounts get $200)
- Scale down to Free tier for storage and OpenAI (not available for Search with vectors)

---

## Cleanup (Delete Resources)

To delete all resources and stop incurring charges:

```powershell
# Delete entire resource group (CAUTION: irreversible)
az group delete --name med-insurance-rg --yes --no-wait

# Or delete individual resources
az storage account delete --name medinsurancestorage --resource-group med-insurance-rg --yes
az search service delete --name medinsurance-search --resource-group med-insurance-rg --yes
az cognitiveservices account delete --name medinsurance-openai --resource-group med-insurance-rg --yes
```

---

## Next Steps

After running setup scripts:

1. **Update Configuration**: Add connection strings and keys to `src/backend/MedInsuranceHelper.Api/appsettings.Development.json`
2. **Upload Sample Documents**: Use Azure Portal or CLI to upload PDFs
3. **Test Indexer**: Wait for indexer to process documents (check status in Portal)
4. **Run Backend**: `dotnet run` from `src/backend/MedInsuranceHelper.Api`
5. **Test Chat API**: Send POST request to `/api/chat` endpoint

See [quickstart.md](../specs/003-foundry-integration/quickstart.md) for detailed testing instructions.
