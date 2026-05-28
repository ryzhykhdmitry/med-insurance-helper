# Setup Azure AI Foundry RAG Endpoint
# Configures Azure AI Foundry project with RAG endpoint connected to search index

param(
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroup = "med-insurance-rg",
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "eastus",
    
    [Parameter(Mandatory=$false)]
    [string]$FoundryProjectName = "medinsurance-foundry",
    
    [Parameter(Mandatory=$false)]
    [string]$SearchServiceName = "medinsurance-search",
    
    [Parameter(Mandatory=$false)]
    [string]$IndexName = "insurance-documents",
    
    [Parameter(Mandatory=$false)]
    [string]$OpenAIServiceName = "medinsurance-openai",
    
    [Parameter(Mandatory=$false)]
    [string]$ChatDeployment = "gpt-35-turbo",
    
    [Parameter(Mandatory=$false)]
    [string]$EmbeddingDeployment = "text-embedding-ada-002"
)

$ErrorActionPreference = "Stop"

Write-Host "[FOUNDRY] Azure AI Foundry RAG Endpoint Setup" -ForegroundColor Cyan
Write-Host "=" * 50

# Check Azure CLI installation
try {
    $azVersion = az version --output json | ConvertFrom-Json
    Write-Host "[OK] Azure CLI installed: $($azVersion.'azure-cli')" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] Azure CLI not found. Please install: https://aka.ms/installazurecliwindows" -ForegroundColor Red
    exit 1
}

# Check if logged in
$account = az account show --output json 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Host "[WARN] Not logged into Azure. Running 'az login'..." -ForegroundColor Yellow
    az login
    $account = az account show --output json | ConvertFrom-Json
}

Write-Host "[OK] Logged in as: $($account.user.name)" -ForegroundColor Green

# Note: Azure AI Foundry (formerly Azure AI Studio) setup typically requires Azure Portal
# or Azure AI CLI extension. This script provides manual configuration steps.

Write-Host "`n[WARN] Azure AI Foundry Configuration" -ForegroundColor Yellow
Write-Host "       Azure AI Foundry projects are best configured through Azure Portal:" -ForegroundColor Gray
Write-Host "       https://ai.azure.com" -ForegroundColor Cyan

Write-Host "`n[STEPS] Manual Setup Steps:" -ForegroundColor Cyan

Write-Host "`n[1] Create Foundry Project:" -ForegroundColor Yellow
Write-Host "    - Navigate to https://ai.azure.com" -ForegroundColor White
Write-Host "    - Click 'Create new project'" -ForegroundColor White
Write-Host "    - Project name: $FoundryProjectName" -ForegroundColor White
Write-Host "    - Resource group: $ResourceGroup" -ForegroundColor White
Write-Host "    - Location: $Location" -ForegroundColor White

Write-Host "`n[2] Connect Azure AI Search:" -ForegroundColor Yellow
Write-Host "    - In project settings, go to 'Connections'" -ForegroundColor White
Write-Host "    - Add connection -> Azure AI Search" -ForegroundColor White
Write-Host "    - Search service: $SearchServiceName" -ForegroundColor White
Write-Host "    - Index: $IndexName" -ForegroundColor White

Write-Host "`n[3] Connect Azure OpenAI:" -ForegroundColor Yellow
Write-Host "    - Add connection -> Azure OpenAI" -ForegroundColor White
Write-Host "    - Service: $OpenAIServiceName" -ForegroundColor White
Write-Host "    - Chat deployment: $ChatDeployment" -ForegroundColor White
Write-Host "    - Embedding deployment: $EmbeddingDeployment" -ForegroundColor White

Write-Host "`n[4] Configure RAG Endpoint:" -ForegroundColor Yellow
Write-Host "    - Go to 'Deployments' -> 'Create deployment'" -ForegroundColor White
Write-Host "    - Select 'Chat with data' (RAG)" -ForegroundColor White
Write-Host "    - Data source: Azure AI Search ($IndexName)" -ForegroundColor White
Write-Host "    - Search type: Hybrid (vector + keyword)" -ForegroundColor White
Write-Host "    - Top K: 5 documents" -ForegroundColor White
Write-Host "    - Strictness: 3 (moderate)" -ForegroundColor White
Write-Host "    - In-scope: Yes (restrict to document content)" -ForegroundColor White

Write-Host "`n[5] Deploy and Get Endpoint:" -ForegroundColor Yellow
Write-Host "    - Deploy the RAG configuration" -ForegroundColor White
Write-Host "    - Copy the endpoint URL (e.g., https://your-project.api.azureml.ms/chat)" -ForegroundColor White
Write-Host "    - Copy the API key from 'Keys and endpoints'" -ForegroundColor White

# Try to get OpenAI endpoint and key as fallback
Write-Host "`n[CONFIG] Configuration Values (add to appsettings.Development.json):" -ForegroundColor Yellow

$openAIEndpoint = az cognitiveservices account show `
    --name $OpenAIServiceName `
    --resource-group $ResourceGroup `
    --query "properties.endpoint" `
    --output tsv

$openAIKey = az cognitiveservices account keys list `
    --name $OpenAIServiceName `
    --resource-group $ResourceGroup `
    --query "key1" `
    --output tsv

$searchEndpoint = "https://$SearchServiceName.search.windows.net"

$searchKey = az search admin-key show `
    --resource-group $ResourceGroup `
    --service-name $SearchServiceName `
    --query "primaryKey" `
    --output tsv

Write-Host "`n  ""AzureFoundry"": {" -ForegroundColor Gray
Write-Host "    ""ProjectEndpoint"": ""<YOUR-FOUNDRY-PROJECT-ENDPOINT>""," -ForegroundColor Gray
Write-Host "    ""ApiKey"": ""<YOUR-FOUNDRY-API-KEY>""," -ForegroundColor Gray
Write-Host "    ""RagDeploymentName"": ""chat-with-data""," -ForegroundColor Gray
Write-Host "    ""OpenAIEndpoint"": ""$openAIEndpoint""," -ForegroundColor Gray
Write-Host "    ""OpenAIKey"": ""$openAIKey""," -ForegroundColor Gray
Write-Host "    ""SearchEndpoint"": ""$searchEndpoint""," -ForegroundColor Gray
Write-Host "    ""SearchKey"": ""$searchKey""," -ForegroundColor Gray
Write-Host "    ""SearchIndexName"": ""$IndexName""," -ForegroundColor Gray
Write-Host "    ""ChatDeployment"": ""$ChatDeployment""," -ForegroundColor Gray
Write-Host "    ""EmbeddingDeployment"": ""$EmbeddingDeployment""" -ForegroundColor Gray
Write-Host "  }" -ForegroundColor Gray

Write-Host "`n" -NoNewline
Write-Host "=" * 50 -ForegroundColor Cyan
Write-Host "[SUCCESS] Configuration Instructions Complete!" -ForegroundColor Green
Write-Host "=" * 50 -ForegroundColor Cyan

Write-Host "`n[NOTE] Alternative: Use Azure OpenAI Directly (Simpler for Learning)" -ForegroundColor Magenta
Write-Host "       If Azure AI Foundry setup is too complex, you can implement RAG" -ForegroundColor Gray
Write-Host "       orchestration in the backend using Azure OpenAI directly:" -ForegroundColor Gray
Write-Host "       - Use Azure.AI.OpenAI SDK" -ForegroundColor Gray
Write-Host "       - Backend generates embeddings for queries" -ForegroundColor Gray
Write-Host "       - Backend searches Azure AI Search" -ForegroundColor Gray
Write-Host "       - Backend assembles prompt with retrieved context" -ForegroundColor Gray
Write-Host "       - Backend calls Azure OpenAI for response generation" -ForegroundColor Gray

Write-Host "`n[NEXT] Next Step: Update appsettings.Development.json with Foundry endpoint" -ForegroundColor Magenta
