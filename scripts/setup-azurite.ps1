<#
.SYNOPSIS
    Azurite local blob storage setup script for Medical Insurance Helper.

.DESCRIPTION
    Manages the Azurite local Azure Blob Storage emulator.
    Supports start, stop, and reset commands.

.PARAMETER Command
    The command to execute: start, stop, or reset.

.EXAMPLE
    .\setup-azurite.ps1 -Command start
    .\setup-azurite.ps1 -Command stop
    .\setup-azurite.ps1 -Command reset
#>

param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("start", "stop", "reset")]
    [string]$Command = "start"
)

$AzuriteDataDir = "$PSScriptRoot\..\data\azurite"
$AzuritePort = 10000

function Start-Azurite {
    # Ensure data directory exists
    if (-not (Test-Path $AzuriteDataDir)) {
        New-Item -ItemType Directory -Path $AzuriteDataDir -Force | Out-Null
        Write-Host "Created Azurite data directory: $AzuriteDataDir" -ForegroundColor Green
    }

    # Check if azurite is installed
    $azurite = Get-Command azurite -ErrorAction SilentlyContinue
    if (-not $azurite) {
        Write-Host "Azurite not found. Installing via npm..." -ForegroundColor Yellow
        npm install -g azurite
    }

    # Check if already running
    $existing = Get-Process -Name "node" -ErrorAction SilentlyContinue | Where-Object {
        $_.CommandLine -like "*azurite*"
    }
    if ($existing) {
        Write-Host "Azurite is already running (PID: $($existing.Id))" -ForegroundColor Yellow
        return
    }

    Write-Host "Starting Azurite on port $AzuritePort..." -ForegroundColor Cyan
    $absDataDir = Resolve-Path $AzuriteDataDir
    Start-Process -FilePath "azurite" `
        -ArgumentList "--silent", "--location", $absDataDir, "--debug", "$absDataDir\debug.log" `
        -WindowStyle Hidden `
        -PassThru | Out-Null

    Start-Sleep -Seconds 2
    Write-Host "Azurite started. Blob endpoint: http://127.0.0.1:$AzuritePort/devstoreaccount1" -ForegroundColor Green
    Write-Host "Connection string:" -ForegroundColor Cyan
    Write-Host "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OGLjX+D4AZfluvY2IVKgJh5BGazG5dz6;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;" -ForegroundColor Gray
}

function Stop-Azurite {
    $procs = Get-Process -Name "node" -ErrorAction SilentlyContinue | Where-Object {
        try { $_.MainWindowTitle -like "*azurite*" } catch { $false }
    }

    if (-not $procs) {
        # Try finding by port
        $netstat = netstat -ano | Select-String ":$AzuritePort "
        if ($netstat) {
            $pid = ($netstat -split '\s+')[-1]
            Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue
            Write-Host "Azurite stopped (PID: $pid)" -ForegroundColor Green
        } else {
            Write-Host "Azurite does not appear to be running." -ForegroundColor Yellow
        }
    } else {
        foreach ($proc in $procs) {
            Stop-Process -Id $proc.Id -Force
            Write-Host "Stopped process PID: $($proc.Id)" -ForegroundColor Green
        }
    }
}

function Reset-Azurite {
    Stop-Azurite
    if (Test-Path $AzuriteDataDir) {
        Remove-Item -Recurse -Force $AzuriteDataDir
        Write-Host "Azurite data directory cleared." -ForegroundColor Yellow
    }
    Start-Azurite
}

switch ($Command) {
    "start"  { Start-Azurite }
    "stop"   { Stop-Azurite }
    "reset"  { Reset-Azurite }
}
