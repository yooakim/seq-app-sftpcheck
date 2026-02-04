# Run.ps1 - Local testing script for Seq.App.SftpCheck
# This script uses seqcli to run the app locally for testing.
# Requires: seqcli on PATH (install via dotnet tool install -g seqcli)

Write-Output "run: Building project..."

Push-Location $PSScriptRoot

& dotnet build src/Seq.App.SftpCheck -c Debug
if ($LASTEXITCODE -ne 0) {
    Pop-Location
    throw "Build failed"
}

$dllPath = "src/Seq.App.SftpCheck/bin/Debug/net10.0/Seq.App.SftpCheck.dll"

if (-not (Test-Path $dllPath)) {
    Pop-Location
    throw "Could not find built assembly at $dllPath"
}

Write-Output "run: Starting Seq app locally..."
Write-Output "run: Press Ctrl+C to stop"
Write-Output ""

# Configure these settings for your test environment
$seqUrl = "http://localhost:5341"      # URL of your local Seq instance
$sftpHost = "localhost"                 # SFTP host to test
$sftpPort = "22"                        # SFTP port
$sftpUsername = "testuser"              # SFTP username
$sftpPassword = "testpassword"          # SFTP password (for password auth)
$authMethod = "Password"                # "Password" or "PrivateKey"
$checkInterval = "60"                   # Check interval in seconds (for testing)
$connectionTimeout = "30"               # Connection timeout in seconds
$friendlyName = "Test SFTP Server"      # Friendly name for logs

# For private key authentication, set these instead:
# $authMethod = "PrivateKey"
# $privateKeyBase64 = "[your-base64-encoded-private-key]"
# $privateKeyPassphrase = "[your-passphrase-if-any]"

# Build the seqcli command
$seqcliArgs = @(
    "app", "run",
    "-d", $dllPath,
    "--type", "Seq.App.SftpCheck.SftpCheckApp",
    "-p", "Host=$sftpHost",
    "-p", "Port=$sftpPort",
    "-p", "Username=$sftpUsername",
    "-p", "AuthenticationMethod=$authMethod",
    "-p", "Password=$sftpPassword",
    "-p", "CheckIntervalSeconds=$checkInterval",
    "-p", "ConnectionTimeoutSeconds=$connectionTimeout",
    "-p", "FriendlyName=$friendlyName",
    "-p", "LogSuccessfulChecks=true",
    "--seq-server=$seqUrl"
)

# Uncomment to add a test directory path
# $seqcliArgs += "-p", "TestDirectoryPath=/home/testuser"

# Uncomment to use an API key for Seq
# $seqcliArgs += "--seq-apikey=YOUR_API_KEY_HERE"

Write-Output "run: Configuration:"
Write-Output "     Seq URL: $seqUrl"
Write-Output "     SFTP Host: $sftpHost`:$sftpPort"
Write-Output "     Username: $sftpUsername"
Write-Output "     Auth Method: $authMethod"
Write-Output "     Check Interval: ${checkInterval}s"
Write-Output ""

& seqcli @seqcliArgs

Pop-Location
