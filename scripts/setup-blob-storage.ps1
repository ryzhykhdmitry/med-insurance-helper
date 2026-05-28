# Setup Azure Blob Storage for Insurance Documents
# Provisions Azure Blob Storage container with idempotent resource checks

param(
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroup = "med-insurance-rg",
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "eastus",
    
    [Parameter(Mandatory=$false)]
    [string]$StorageAccountName = "medinsurancestorage",
    
    [Parameter(Mandatory=$false)]
    [string]$ContainerName = "insurance-docs"
)

$ErrorActionPreference = "Stop"

Write-Host "[SETUP] Azure Blob Storage Setup" -ForegroundColor Cyan
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
Write-Host "[INFO] Subscription: $($account.name)" -ForegroundColor Green

# Check if resource group exists
Write-Host "`n[CHECK] Checking resource group: $ResourceGroup" -ForegroundColor Cyan
$rgExists = az group exists --name $ResourceGroup
if ($rgExists -eq "true") {
    Write-Host "[OK] Resource group already exists" -ForegroundColor Green
} else {
    Write-Host "[WARN] Creating resource group..." -ForegroundColor Yellow
    az group create --name $ResourceGroup --location $Location --output none
    Write-Host "[OK] Resource group created" -ForegroundColor Green
}

# Check if storage account exists
Write-Host "`n[CHECK] Checking storage account: $StorageAccountName" -ForegroundColor Cyan
$storageExists = az storage account show --name $StorageAccountName --resource-group $ResourceGroup --output json
if ($storageExists) {
    Write-Host "[OK] Storage account already exists" -ForegroundColor Green
    $storage = $storageExists | ConvertFrom-Json
} else {
    Write-Host "[WARN] Creating storage account..." -ForegroundColor Yellow
    Write-Host "       (This may take 1-2 minutes)" -ForegroundColor Gray
    
    az storage account create `
        --name $StorageAccountName `
        --resource-group $ResourceGroup `
        --location $Location `
        --sku Standard_LRS `
        --kind StorageV2 `
        --output none
    
    Write-Host "[OK] Storage account created" -ForegroundColor Green
    $storage = az storage account show --name $StorageAccountName --resource-group $ResourceGroup --output json | ConvertFrom-Json
}

# Get storage account key
$storageKey = az storage account keys list `
    --resource-group $ResourceGroup `
    --account-name $StorageAccountName `
    --query "[0].value" `
    --output tsv

# Check if container exists
Write-Host "`n[CHECK] Checking blob container: $ContainerName" -ForegroundColor Cyan
$containerExists = az storage container exists `
    --name $ContainerName `
    --account-name $StorageAccountName `
    --account-key $storageKey `
    --output json | ConvertFrom-Json

if ($containerExists.exists) {
    Write-Host "[OK] Container already exists" -ForegroundColor Green
} else {
    Write-Host "[WARN] Creating blob container..." -ForegroundColor Yellow
    
    az storage container create `
        --name $ContainerName `
        --account-name $StorageAccountName `
        --account-key $storageKey `
        --public-access off `
        --output none
    
    Write-Host "[OK] Container created" -ForegroundColor Green
}

# Enable change feed for efficient indexer monitoring
Write-Host "`n[CHECK] Enabling blob change feed (for indexer)..." -ForegroundColor Cyan
az storage account blob-service-properties update `
    --account-name $StorageAccountName `
    --resource-group $ResourceGroup `
    --enable-change-feed true `
    --output none
Write-Host "[OK] Change feed enabled" -ForegroundColor Green

# Output connection string
$connectionString = az storage account show-connection-string `
    --name $StorageAccountName `
    --resource-group $ResourceGroup `
    --output tsv

Write-Host "`n" -NoNewline
Write-Host "=" * 50 -ForegroundColor Cyan
Write-Host "[SUCCESS] Setup Complete!" -ForegroundColor Green
Write-Host "=" * 50 -ForegroundColor Cyan

Write-Host "`n[CONFIG] Configuration Details:" -ForegroundColor Cyan
Write-Host "         Storage Account: $StorageAccountName" -ForegroundColor White
Write-Host "         Container: $ContainerName" -ForegroundColor White
Write-Host "         Endpoint: https://$StorageAccountName.blob.core.windows.net/$ContainerName" -ForegroundColor White

Write-Host "`n[KEY] Connection String (add to appsettings.Development.json):" -ForegroundColor Yellow
Write-Host $connectionString -ForegroundColor Gray

Write-Host "`n[HELP] To upload test documents:" -ForegroundColor Cyan
Write-Host "       az storage blob upload --account-name $StorageAccountName --container-name $ContainerName --name sample.pdf --file ./docs/sample.pdf --auth-mode key" -ForegroundColor Gray

Write-Host "`n[NEXT] Next Step: Run setup-search-indexer.ps1" -ForegroundColor Magenta
