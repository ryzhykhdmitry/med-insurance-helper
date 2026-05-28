<#
.SYNOPSIS
    Loads sample insurance documents into the Medical Insurance Helper API.

.DESCRIPTION
    Uploads sample documents to Azurite blob storage, registers them via POST /api/ingest,
    and triggers processing via POST /api/process/{offerId}.

.PARAMETER ApiBaseUrl
    Base URL of the backend API. Defaults to http://localhost:5000.
#>

param(
    [string]$ApiBaseUrl = "http://localhost:5000"
)

$SamplesDir = "$PSScriptRoot\..\docs\samples"

$samples = @(
    @{ File = "https://medinsurancestorage.blob.core.windows.net/insurance-docs/ComplexGoldENG.pdf"; Insurer = "Luxmed"; Title = "Complex Gold Plan" }
    #@{ File = "alpha-health-plan.txt"; Insurer = "Alpha Health Insurance"; Title = "Alpha Health Standard Plan" },
    #@{ File = "beta-care-plan.txt";    Insurer = "Beta Care Insurance";    Title = "Beta Care Comprehensive Plan" },
    #@{ File = "gamma-premium-plan.txt";Insurer = "Gamma Premium Insurance";Title = "Gamma Premium Elite Plan" }
)

function Invoke-ApiPost {
    param([string]$Url, [hashtable]$Body)
    $json = $Body | ConvertTo-Json -Depth 3
    try {
        $response = Invoke-RestMethod -Method POST -Uri $Url -Body $json -ContentType "application/json" -ErrorAction Stop
        return $response
    } catch {
        Write-Error "POST $Url failed: $_"
        return $null
    }
}

Write-Host "=== Medical Insurance Helper - Sample Loader ===" -ForegroundColor Cyan
Write-Host "API: $ApiBaseUrl" -ForegroundColor Gray
Write-Host ""

$offerIds = @()

foreach ($sample in $samples) {
    # $filePath = Join-Path $SamplesDir $sample.File
    # if (-not (Test-Path $filePath)) {
    #     Write-Warning "Sample file not found: $filePath"
    #     continue
    # }

    Write-Host "Ingesting '$($sample.Title)' from $($sample.Insurer)..." -ForegroundColor Yellow

    # For local dev: use a mock blob URI pointing to the local file path
    # In a real deployment, upload to blob first then pass the real URI
    $blobUri = "$($sample.File)"

    $ingestResult = Invoke-ApiPost "$ApiBaseUrl/api/ingest" @{
        blobUri     = $blobUri
        insurerName = $sample.Insurer
        title       = $sample.Title
    }

    if ($null -eq $ingestResult) {
        Write-Warning "Failed to ingest $($sample.Title)"
        continue
    }

    $offerId = $ingestResult.offerId
    $offerIds += $offerId
    Write-Host "  [OK] Registered: offerId=$offerId" -ForegroundColor Green

    # Trigger processing
    $processResult = Invoke-ApiPost "$ApiBaseUrl/api/process/$offerId" @{}
    if ($processResult -and $processResult.status -eq "processing") {
        Write-Host "  [OK] Processing started for offerId=$offerId" -ForegroundColor Green
    } else {
        Write-Warning "  Processing trigger may have failed for offerId=$offerId"
    }

    Start-Sleep -Milliseconds 200
}

Write-Host ""
Write-Host "=== Loaded $($offerIds.Count) offer(s) ===" -ForegroundColor Cyan
Write-Host "Offer IDs:" -ForegroundColor Gray
$offerIds | ForEach-Object { Write-Host "  - $_" -ForegroundColor White }

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Wait 10-30s for processing to complete (embeddings are generated asynchronously)"
Write-Host "  2. Open http://localhost:4200 in your browser"
Write-Host "  3. Ask: 'What is the annual coverage limit for Alpha Health?'"
