# Test-Local.ps1 - Local Docker-based testing workflow for Seq.App.SftpCheck
#
# This script helps you:
#   1. Start/stop the Docker Compose environment (Seq + SFTP server)
#   2. Build and package the Seq app
#   3. Test the app locally using seqcli
#
# Prerequisites:
#   - Docker Desktop (or Docker Engine + Compose)
#   - .NET 10 SDK
#   - seqcli (optional, for local app testing without installing in Seq)
#
# Usage:
#   .\Test-Local.ps1 -Start         # Start Docker containers
#   .\Test-Local.ps1 -Stop          # Stop Docker containers
#   .\Test-Local.ps1 -Build         # Build and package the app
#   .\Test-Local.ps1 -Test          # Run the app locally with seqcli
#   .\Test-Local.ps1 -All           # Start containers, build, and test
#   .\Test-Local.ps1 -Status        # Show container status
#   .\Test-Local.ps1 -Logs          # Show Seq logs
#   .\Test-Local.ps1 -Clean         # Stop containers and remove volumes

param(
    [switch]$Start,
    [switch]$Stop,
    [switch]$Build,
    [switch]$Test,
    [switch]$All,
    [switch]$Status,
    [switch]$Logs,
    [switch]$Clean,
    [switch]$Help
)

$ErrorActionPreference = "Stop"
Push-Location $PSScriptRoot

# Configuration
$SeqUrl = "http://localhost:5341"
$SftpHost = "localhost"
$SftpPort = 2222
$SftpUsername = "testuser"
$SftpPassword = "testpass"
$TestDirectory = "/upload"
$CheckIntervalSeconds = 30  # Faster for testing

function Write-Header {
    param([string]$Message)
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host " $Message" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
}

function Test-DockerRunning {
    try {
        $null = docker info 2>&1
        return $LASTEXITCODE -eq 0
    }
    catch {
        return $false
    }
}

function Start-Environment {
    Write-Header "Starting Docker Environment"

    if (-not (Test-DockerRunning)) {
        Write-Error "Docker is not running. Please start Docker Desktop and try again."
        return $false
    }

    Write-Host "Starting Seq and SFTP containers..." -ForegroundColor Yellow
    docker compose up -d

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to start Docker containers"
        return $false
    }

    Write-Host ""
    Write-Host "Waiting for services to be ready..." -ForegroundColor Yellow
    Start-Sleep -Seconds 5

    # Check if Seq is responding
    $maxAttempts = 30
    $attempt = 0
    while ($attempt -lt $maxAttempts) {
        try {
            $response = Invoke-WebRequest -Uri "$SeqUrl/api" -UseBasicParsing -TimeoutSec 2 -ErrorAction SilentlyContinue
            if ($response.StatusCode -eq 200) {
                Write-Host "Seq is ready!" -ForegroundColor Green
                break
            }
        }
        catch {
            # Ignore errors, keep trying
        }
        $attempt++
        Start-Sleep -Seconds 1
        Write-Host "." -NoNewline
    }

    if ($attempt -ge $maxAttempts) {
        Write-Warning "Seq may not be fully ready yet. Check http://localhost:5341"
    }

    Write-Host ""
    Write-Host "Environment is running:" -ForegroundColor Green
    Write-Host "  Seq UI:      $SeqUrl" -ForegroundColor White
    Write-Host "  SFTP Server: $SftpHost`:$SftpPort" -ForegroundColor White
    Write-Host "  SFTP User:   $SftpUsername / $SftpPassword" -ForegroundColor White
    Write-Host ""
    Write-Host "To install the app in Seq:" -ForegroundColor Yellow
    Write-Host "  1. Run: .\Test-Local.ps1 -Build" -ForegroundColor White
    Write-Host "  2. Open $SeqUrl in your browser" -ForegroundColor White
    Write-Host "  3. Go to Settings > Apps > Install from NuGet" -ForegroundColor White
    Write-Host "  4. Select the .nupkg from ./artifacts folder" -ForegroundColor White
    Write-Host ""

    return $true
}

function Stop-Environment {
    Write-Header "Stopping Docker Environment"

    docker compose down

    Write-Host "Environment stopped." -ForegroundColor Green
}

function Clear-Environment {
    Write-Header "Cleaning Docker Environment"

    Write-Host "Stopping containers and removing volumes..." -ForegroundColor Yellow
    docker compose down -v

    Write-Host ""
    Write-Host "Cleaning build artifacts..." -ForegroundColor Yellow
    if (Test-Path "./artifacts") {
        Remove-Item -Path "./artifacts" -Recurse -Force
    }
    if (Test-Path "./src/Seq.App.SftpCheck/bin") {
        Remove-Item -Path "./src/Seq.App.SftpCheck/bin" -Recurse -Force
    }
    if (Test-Path "./src/Seq.App.SftpCheck/obj") {
        Remove-Item -Path "./src/Seq.App.SftpCheck/obj" -Recurse -Force
    }

    Write-Host "Environment cleaned." -ForegroundColor Green
}

function Build-App {
    Write-Header "Building Seq App"

    # Check for .NET SDK
    try {
        $dotnetVersion = dotnet --version
        Write-Host "Using .NET SDK: $dotnetVersion" -ForegroundColor Gray
    }
    catch {
        Write-Error ".NET SDK is not installed or not in PATH"
        return $false
    }

    Write-Host "Restoring packages..." -ForegroundColor Yellow
    dotnet restore
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Package restore failed"
        return $false
    }

    Write-Host "Building solution..." -ForegroundColor Yellow
    dotnet build -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed"
        return $false
    }

    Write-Host "Running tests..." -ForegroundColor Yellow
    dotnet test -c Release --no-build
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Some tests failed, but continuing..."
    }

    Write-Host "Publishing for packaging..." -ForegroundColor Yellow
    Push-Location "./src/Seq.App.SftpCheck"
    dotnet publish -c Release -o ./obj/publish
    if ($LASTEXITCODE -ne 0) {
        Pop-Location
        Write-Error "Publish failed"
        return $false
    }

    Write-Host "Creating NuGet package..." -ForegroundColor Yellow
    dotnet pack -c Release -o ../../artifacts --no-build
    if ($LASTEXITCODE -ne 0) {
        Pop-Location
        Write-Error "Pack failed"
        return $false
    }
    Pop-Location

    Write-Host ""
    Write-Host "Build completed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Package location:" -ForegroundColor White
    Get-ChildItem "./artifacts/*.nupkg" | ForEach-Object {
        Write-Host "  $($_.FullName)" -ForegroundColor Cyan
    }
    Write-Host ""

    return $true
}

function Test-AppWithSeqCli {
    Write-Header "Testing App with seqcli"

    # Check for seqcli
    try {
        $null = Get-Command seqcli -ErrorAction Stop
    }
    catch {
        Write-Warning "seqcli is not installed. Install with: dotnet tool install -g seqcli"
        Write-Host ""
        Write-Host "Alternative: Install the app manually in Seq:" -ForegroundColor Yellow
        Write-Host "  1. Open $SeqUrl" -ForegroundColor White
        Write-Host "  2. Go to Settings > Apps" -ForegroundColor White
        Write-Host "  3. Click 'Install from NuGet'" -ForegroundColor White
        Write-Host "  4. Upload the .nupkg from ./artifacts" -ForegroundColor White
        return $false
    }

    $dllPath = "./src/Seq.App.SftpCheck/bin/Release/net10.0/Seq.App.SftpCheck.dll"

    if (-not (Test-Path $dllPath)) {
        # Try Debug build
        $dllPath = "./src/Seq.App.SftpCheck/bin/Debug/net10.0/Seq.App.SftpCheck.dll"
    }

    if (-not (Test-Path $dllPath)) {
        Write-Host "App not built yet. Building..." -ForegroundColor Yellow
        dotnet build "./src/Seq.App.SftpCheck" -c Debug
        $dllPath = "./src/Seq.App.SftpCheck/bin/Debug/net10.0/Seq.App.SftpCheck.dll"
    }

    if (-not (Test-Path $dllPath)) {
        Write-Error "Could not find built assembly"
        return $false
    }

    Write-Host "Running app locally with seqcli..." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Configuration:" -ForegroundColor White
    Write-Host "  Seq URL:        $SeqUrl" -ForegroundColor Gray
    Write-Host "  SFTP Host:      $SftpHost`:$SftpPort" -ForegroundColor Gray
    Write-Host "  SFTP User:      $SftpUsername" -ForegroundColor Gray
    Write-Host "  Check Interval: ${CheckIntervalSeconds}s" -ForegroundColor Gray
    Write-Host "  Test Directory: $TestDirectory" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Press Ctrl+C to stop" -ForegroundColor Yellow
    Write-Host ""

    & seqcli app run `
        -d $dllPath `
        --type "Seq.App.SftpCheck.SftpCheckApp" `
        -p "Host=$SftpHost" `
        -p "Port=$SftpPort" `
        -p "Username=$SftpUsername" `
        -p "AuthenticationMethod=Password" `
        -p "Password=$SftpPassword" `
        -p "CheckIntervalSeconds=$CheckIntervalSeconds" `
        -p "ConnectionTimeoutSeconds=30" `
        -p "TestDirectoryPath=$TestDirectory" `
        -p "FriendlyName=Docker Test SFTP" `
        -p "LogSuccessfulChecks=true" `
        --seq-server="$SeqUrl"
}

function Show-Status {
    Write-Header "Environment Status"

    docker compose ps

    Write-Host ""
    Write-Host "URLs:" -ForegroundColor Yellow
    Write-Host "  Seq UI: $SeqUrl" -ForegroundColor White
    Write-Host "  SFTP:   sftp://$SftpUsername@$SftpHost`:$SftpPort" -ForegroundColor White
}

function Show-Logs {
    Write-Header "Seq Container Logs"

    docker compose logs -f seq
}

function Show-Help {
    Write-Host ""
    Write-Host "Seq.App.SftpCheck - Local Testing Script" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Usage:" -ForegroundColor Yellow
    Write-Host "  .\Test-Local.ps1 -Start    Start Docker containers (Seq + SFTP)"
    Write-Host "  .\Test-Local.ps1 -Stop     Stop Docker containers"
    Write-Host "  .\Test-Local.ps1 -Build    Build and package the Seq app"
    Write-Host "  .\Test-Local.ps1 -Test     Run the app locally with seqcli"
    Write-Host "  .\Test-Local.ps1 -All      Start, build, and test (full workflow)"
    Write-Host "  .\Test-Local.ps1 -Status   Show container status"
    Write-Host "  .\Test-Local.ps1 -Logs     Follow Seq container logs"
    Write-Host "  .\Test-Local.ps1 -Clean    Stop containers and remove all data"
    Write-Host ""
    Write-Host "Quick Start:" -ForegroundColor Yellow
    Write-Host "  1. .\Test-Local.ps1 -Start    # Start the environment"
    Write-Host "  2. .\Test-Local.ps1 -Build    # Build the app"
    Write-Host "  3. Open http://localhost:5341 and install the app from ./artifacts"
    Write-Host "  4. Configure an instance with:"
    Write-Host "       Host: localhost (or 'sftp' if app runs in Seq container)"
    Write-Host "       Port: 2222"
    Write-Host "       Username: testuser"
    Write-Host "       Password: testpass"
    Write-Host ""
    Write-Host "Or use seqcli for quick testing:" -ForegroundColor Yellow
    Write-Host "  .\Test-Local.ps1 -Test"
    Write-Host ""
}

# Main execution
try {
    if ($Help -or (-not ($Start -or $Stop -or $Build -or $Test -or $All -or $Status -or $Logs -or $Clean))) {
        Show-Help
        exit 0
    }

    if ($All) {
        $Start = $true
        $Build = $true
        $Test = $true
    }

    if ($Start) {
        if (-not (Start-Environment)) {
            exit 1
        }
    }

    if ($Build) {
        if (-not (Build-App)) {
            exit 1
        }
    }

    if ($Test) {
        Test-AppWithSeqCli
    }

    if ($Stop) {
        Stop-Environment
    }

    if ($Clean) {
        Clear-Environment
    }

    if ($Status) {
        Show-Status
    }

    if ($Logs) {
        Show-Logs
    }
}
finally {
    Pop-Location
}
