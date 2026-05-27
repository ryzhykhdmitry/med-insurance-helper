# Setup Azure AI Search Indexer with Skillset and Vector Index
# Configures AI Search to automatically process PDFs from blob storage

param(
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroup = "med-insurance-rg",
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "eastus",
    
    [Parameter(Mandatory=$false)]
    [string]$SearchServiceName = "medinsurance-search",
    
    [Parameter(Mandatory=$false)]
    [string]$StorageAccountName = "medinsurancestorage",
    
    [Parameter(Mandatory=$false)]
    [string]$ContainerName = "insurance-docs",
    
    [Parameter(Mandatory=$false)]
    [string]$OpenAIServiceName = "medinsurance-openai",
    
    [Parameter(Mandatory=$false)]
    [string]$EmbeddingDeployment = "text-embedding-ada-002",
    
    [Parameter(Mandatory=$false)]
    [string]$IndexName = "insurance-documents"
)

$ErrorActionPreference = "Stop"

Write-Host "🔍 Azure AI Search Indexer Setup" -ForegroundColor Cyan
Write-Host "=" * 50

# Check Azure CLI installation
try {
    $azVersion = az version --output json | ConvertFrom-Json
    Write-Host "✅ Azure CLI installed: $($azVersion.'azure-cli')" -ForegroundColor Green
} catch {
    Write-Host "❌ Azure CLI not found. Please install: https://aka.ms/installazurecliwindows" -ForegroundColor Red
    exit 1
}

# Check if logged in
$account = az account show --output json 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Host "⚠️  Not logged into Azure. Running 'az login'..." -ForegroundColor Yellow
    az login
    $account = az account show --output json | ConvertFrom-Json
}

Write-Host "✅ Logged in as: $($account.user.name)" -ForegroundColor Green

# Check if search service exists
Write-Host "`n🔎 Checking Azure AI Search service: $SearchServiceName" -ForegroundColor Cyan
$searchExists = az search service show --name $SearchServiceName --resource-group $ResourceGroup --output json 2>$null
if ($searchExists) {
    Write-Host "✅ Search service already exists" -ForegroundColor Green
    $search = $searchExists | ConvertFrom-Json
} else {
    Write-Host "⚠️  Creating Azure AI Search service (Basic tier)..." -ForegroundColor Yellow
    Write-Host "   (This may take 3-5 minutes)" -ForegroundColor Gray
    
    az search service create `
        --name $SearchServiceName `
        --resource-group $ResourceGroup `
        --location $Location `
        --sku basic `
        --output none
    
    Write-Host "✅ Search service created" -ForegroundColor Green
    $search = az search service show --name $SearchServiceName --resource-group $ResourceGroup --output json | ConvertFrom-Json
}

# Get search admin key
$searchKey = az search admin-key show `
    --resource-group $ResourceGroup `
    --service-name $SearchServiceName `
    --query "primaryKey" `
    --output tsv

# Get storage connection string
$storageConnectionString = az storage account show-connection-string `
    --name $StorageAccountName `
    --resource-group $ResourceGroup `
    --output tsv

# Get OpenAI endpoint and key
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

Write-Host "`n📋 Creating index schema with vector fields..." -ForegroundColor Cyan

# Create index with vector field
$indexSchema = @{
    name = $IndexName
    fields = @(
        @{ name = "id"; type = "Edm.String"; key = $true; searchable = $false }
        @{ name = "content"; type = "Edm.String"; searchable = $true; filterable = $false }
        @{ name = "fileName"; type = "Edm.String"; searchable = $true; filterable = $true; facetable = $true }
        @{ name = "blobUri"; type = "Edm.String"; searchable = $false; filterable = $true }
        @{ name = "pageNumber"; type = "Edm.Int32"; searchable = $false; filterable = $true; sortable = $true }
        @{ name = "chunkIndex"; type = "Edm.Int32"; searchable = $false; filterable = $true; sortable = $true }
        @{ name = "contentVector"; type = "Collection(Edm.Single)"; searchable = $true; dimensions = 1536; vectorSearchProfile = "vector-profile" }
        @{ name = "lastModified"; type = "Edm.DateTimeOffset"; searchable = $false; filterable = $true; sortable = $true }
    )
    vectorSearch = @{
        algorithms = @(
            @{
                name = "hnsw-algorithm"
                kind = "hnsw"
                hnswParameters = @{
                    metric = "cosine"
                    m = 4
                    efConstruction = 400
                    efSearch = 500
                }
            }
        )
        profiles = @(
            @{
                name = "vector-profile"
                algorithm = "hnsw-algorithm"
            }
        )
    }
    semantic = @{
        configurations = @(
            @{
                name = "semantic-config"
                prioritizedFields = @{
                    contentFields = @(
                        @{ fieldName = "content" }
                    )
                    keywordsFields = @(
                        @{ fieldName = "fileName" }
                    )
                }
            }
        )
    }
} | ConvertTo-Json -Depth 10

# Check if index exists
$indexExists = Invoke-RestMethod -Uri "$searchEndpoint/indexes/$IndexName`?api-version=2023-11-01" `
    -Headers @{ "api-key" = $searchKey } `
    -Method Get `
    -ErrorAction SilentlyContinue

if ($indexExists) {
    Write-Host "✅ Index already exists" -ForegroundColor Green
} else {
    Write-Host "⚠️  Creating index..." -ForegroundColor Yellow
    
    Invoke-RestMethod -Uri "$searchEndpoint/indexes?api-version=2023-11-01" `
        -Headers @{ "api-key" = $searchKey; "Content-Type" = "application/json" } `
        -Method Post `
        -Body $indexSchema `
        -ErrorAction Stop | Out-Null
    
    Write-Host "✅ Index created" -ForegroundColor Green
}

Write-Host "`n🔗 Creating data source connection..." -ForegroundColor Cyan

# Create data source
$dataSource = @{
    name = "insurance-docs-datasource"
    type = "azureblob"
    credentials = @{
        connectionString = $storageConnectionString
    }
    container = @{
        name = $ContainerName
        query = $null
    }
    dataDeletionDetectionPolicy = @{
        "@odata.type" = "#Microsoft.Azure.Search.SoftDeleteColumnDeletionDetectionPolicy"
        softDeleteColumnName = "IsDeleted"
        softDeleteMarkerValue = "true"
    }
} | ConvertTo-Json -Depth 10

$dataSourceExists = Invoke-RestMethod -Uri "$searchEndpoint/datasources/insurance-docs-datasource?api-version=2023-11-01" `
    -Headers @{ "api-key" = $searchKey } `
    -Method Get `
    -ErrorAction SilentlyContinue

if ($dataSourceExists) {
    Write-Host "✅ Data source already exists" -ForegroundColor Green
} else {
    Invoke-RestMethod -Uri "$searchEndpoint/datasources?api-version=2023-11-01" `
        -Headers @{ "api-key" = $searchKey; "Content-Type" = "application/json" } `
        -Method Post `
        -Body $dataSource `
        -ErrorAction Stop | Out-Null
    
    Write-Host "✅ Data source created" -ForegroundColor Green
}

Write-Host "`n⚙️  Creating skillset (document cracking + chunking + embedding)..." -ForegroundColor Cyan

# Create skillset with splitting and embedding
$skillset = @{
    name = "insurance-docs-skillset"
    description = "Extract text, split into chunks, and generate embeddings"
    skills = @(
        @{
            "@odata.type" = "#Microsoft.Skills.Text.SplitSkill"
            context = "/document"
            textSplitMode = "pages"
            maximumPageLength = 2000
            pageOverlapLength = 200
            inputs = @(
                @{ name = "text"; source = "/document/content" }
            )
            outputs = @(
                @{ name = "textItems"; targetName = "pages" }
            )
        },
        @{
            "@odata.type" = "#Microsoft.Skills.Text.AzureOpenAIEmbeddingSkill"
            context = "/document/pages/*"
            resourceUri = $openAIEndpoint
            apiKey = $openAIKey
            deploymentId = $EmbeddingDeployment
            inputs = @(
                @{ name = "text"; source = "/document/pages/*" }
            )
            outputs = @(
                @{ name = "embedding"; targetName = "contentVector" }
            )
        }
    )
    indexProjections = @{
        selectors = @(
            @{
                targetIndexName = $IndexName
                parentKeyFieldName = "id"
                sourceContext = "/document/pages/*"
                mappings = @(
                    @{ name = "content"; source = "/document/pages/*"; }
                    @{ name = "contentVector"; source = "/document/pages/*/contentVector" }
                    @{ name = "fileName"; source = "/document/metadata_storage_name" }
                    @{ name = "blobUri"; source = "/document/metadata_storage_path" }
                    @{ name = "lastModified"; source = "/document/metadata_storage_last_modified" }
                )
            }
        )
    }
} | ConvertTo-Json -Depth 10

$skillsetExists = Invoke-RestMethod -Uri "$searchEndpoint/skillsets/insurance-docs-skillset?api-version=2023-11-01" `
    -Headers @{ "api-key" = $searchKey } `
    -Method Get `
    -ErrorAction SilentlyContinue

if ($skillsetExists) {
    Write-Host "✅ Skillset already exists" -ForegroundColor Green
} else {
    Invoke-RestMethod -Uri "$searchEndpoint/skillsets?api-version=2023-11-01" `
        -Headers @{ "api-key" = $searchKey; "Content-Type" = "application/json" } `
        -Method Post `
        -Body $skillset `
        -ErrorAction Stop | Out-Null
    
    Write-Host "✅ Skillset created" -ForegroundColor Green
}

Write-Host "`n🤖 Creating indexer (monitors blob container)..." -ForegroundColor Cyan

# Create indexer
$indexer = @{
    name = "insurance-docs-indexer"
    dataSourceName = "insurance-docs-datasource"
    targetIndexName = $IndexName
    skillsetName = "insurance-docs-skillset"
    schedule = @{
        interval = "PT5M"  # Run every 5 minutes
    }
    parameters = @{
        batchSize = 10
        maxFailedItems = 5
        maxFailedItemsPerBatch = 5
        configuration = @{
            dataToExtract = "contentAndMetadata"
            parsingMode = "default"
            imageAction = "none"
        }
    }
    fieldMappings = @()
} | ConvertTo-Json -Depth 10

$indexerExists = Invoke-RestMethod -Uri "$searchEndpoint/indexers/insurance-docs-indexer?api-version=2023-11-01" `
    -Headers @{ "api-key" = $searchKey } `
    -Method Get `
    -ErrorAction SilentlyContinue

if ($indexerExists) {
    Write-Host "✅ Indexer already exists" -ForegroundColor Green
} else {
    Invoke-RestMethod -Uri "$searchEndpoint/indexers?api-version=2023-11-01" `
        -Headers @{ "api-key" = $searchKey; "Content-Type" = "application/json" } `
        -Method Post `
        -Body $indexer `
        -ErrorAction Stop | Out-Null
    
    Write-Host "✅ Indexer created" -ForegroundColor Green
}

# Run indexer immediately
Write-Host "`n▶️  Running indexer now (initial run)..." -ForegroundColor Cyan
Invoke-RestMethod -Uri "$searchEndpoint/indexers/insurance-docs-indexer/run?api-version=2023-11-01" `
    -Headers @{ "api-key" = $searchKey } `
    -Method Post `
    -ErrorAction SilentlyContinue | Out-Null
Write-Host "✅ Indexer started" -ForegroundColor Green

Write-Host "`n" -NoNewline
Write-Host "=" * 50 -ForegroundColor Cyan
Write-Host "✅ Setup Complete!" -ForegroundColor Green
Write-Host "=" * 50 -ForegroundColor Cyan

Write-Host "`n📋 Configuration Details:" -ForegroundColor Cyan
Write-Host "   Search Service: $SearchServiceName" -ForegroundColor White
Write-Host "   Index: $IndexName" -ForegroundColor White
Write-Host "   Endpoint: $searchEndpoint" -ForegroundColor White

Write-Host "`n🔑 Search Key (add to appsettings.Development.json):" -ForegroundColor Yellow
Write-Host $searchKey -ForegroundColor Gray

Write-Host "`n🔍 To check indexer status:" -ForegroundColor Cyan
Write-Host "   az search indexer show --name insurance-docs-indexer --service-name $SearchServiceName --resource-group $ResourceGroup" -ForegroundColor Gray

Write-Host "`n⏭️  Next Step: Run setup-foundry-project.ps1" -ForegroundColor Magenta
