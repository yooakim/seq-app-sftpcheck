#!/bin/bash
# run.sh - Local testing script for Seq.App.SftpCheck
#
# This script uses seqcli to run the app locally for testing.
# Requires: seqcli on PATH (install via: dotnet tool install -g seqcli)
#
# Usage:
#   ./run.sh              # Run with default settings
#   ./run.sh --debug      # Run with debug build
#   ./run.sh --help       # Show help
#
# Configuration:
#   Edit the variables below to match your test environment

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

# Get script directory and change to it
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# ==============================================================================
# Configuration - Edit these settings for your test environment
# ==============================================================================

# Seq server URL
SEQ_URL="http://localhost:5341"

# SFTP server settings
SFTP_HOST="localhost"
SFTP_PORT="2222"
SFTP_USERNAME="testuser"
SFTP_PASSWORD="testpass"

# Authentication method: "Password" or "PrivateKey"
AUTH_METHOD="Password"

# For private key authentication, set these:
# PRIVATE_KEY_BASE64="[your-base64-encoded-private-key]"
# PRIVATE_KEY_PASSPHRASE="[your-passphrase-if-any]"

# App settings
CHECK_INTERVAL="60"        # Check interval in seconds (shorter for testing)
CONNECTION_TIMEOUT="30"    # Connection timeout in seconds
FRIENDLY_NAME="Test SFTP Server"
TEST_DIRECTORY="/upload"   # Optional: directory to list for testing (empty to skip)
LOG_SUCCESS="true"         # Log successful checks

# Seq API key (optional)
# SEQ_API_KEY=""

# ==============================================================================
# End of configuration
# ==============================================================================

# Parse arguments
BUILD_CONFIG="Release"

for arg in "$@"; do
    case $arg in
        --debug)
            BUILD_CONFIG="Debug"
            ;;
        --help|-h)
            echo "Usage: ./run.sh [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --debug    Use debug build instead of release"
            echo "  --help     Show this help message"
            echo ""
            echo "Configuration:"
            echo "  Edit the variables at the top of this script to configure:"
            echo "  - Seq server URL"
            echo "  - SFTP server connection details"
            echo "  - Authentication method and credentials"
            echo "  - Check interval and timeout"
            echo ""
            echo "Prerequisites:"
            echo "  - .NET 10 SDK"
            echo "  - seqcli (install: dotnet tool install -g seqcli)"
            echo "  - Running Seq instance (use: ./test-local.sh start)"
            exit 0
            ;;
    esac
done

echo -e "${CYAN}========================================"
echo -e " Seq.App.SftpCheck - Local Testing"
echo -e "========================================${NC}"
echo ""

# Check for seqcli
if ! command -v seqcli &> /dev/null; then
    echo -e "${RED}Error: seqcli is not installed or not in PATH${NC}"
    echo ""
    echo "Install seqcli with:"
    echo "  dotnet tool install -g seqcli"
    echo ""
    echo "Make sure ~/.dotnet/tools is in your PATH:"
    echo "  export PATH=\"\$PATH:\$HOME/.dotnet/tools\""
    exit 1
fi

# Build the project
echo -e "${YELLOW}run: Building project (${BUILD_CONFIG})...${NC}"
dotnet build src/Seq.App.SftpCheck -c "$BUILD_CONFIG"
echo ""

# Find the built DLL
DLL_PATH="src/Seq.App.SftpCheck/bin/${BUILD_CONFIG}/net10.0/Seq.App.SftpCheck.dll"

if [ ! -f "$DLL_PATH" ]; then
    echo -e "${RED}Error: Could not find built assembly at ${DLL_PATH}${NC}"
    exit 1
fi

echo -e "${GREEN}run: Found assembly: ${DLL_PATH}${NC}"
echo ""

# Build seqcli arguments
SEQCLI_ARGS=(
    "app" "run"
    "-d" "$DLL_PATH"
    "--type" "Seq.App.SftpCheck.SftpCheckApp"
    "-p" "Host=$SFTP_HOST"
    "-p" "Port=$SFTP_PORT"
    "-p" "Username=$SFTP_USERNAME"
    "-p" "AuthenticationMethod=$AUTH_METHOD"
    "-p" "CheckIntervalSeconds=$CHECK_INTERVAL"
    "-p" "ConnectionTimeoutSeconds=$CONNECTION_TIMEOUT"
    "-p" "FriendlyName=$FRIENDLY_NAME"
    "-p" "LogSuccessfulChecks=$LOG_SUCCESS"
    "--seq-server=$SEQ_URL"
)

# Add authentication credentials
if [ "$AUTH_METHOD" = "Password" ]; then
    SEQCLI_ARGS+=("-p" "Password=$SFTP_PASSWORD")
elif [ "$AUTH_METHOD" = "PrivateKey" ]; then
    if [ -n "$PRIVATE_KEY_BASE64" ]; then
        SEQCLI_ARGS+=("-p" "PrivateKeyBase64=$PRIVATE_KEY_BASE64")
    fi
    if [ -n "$PRIVATE_KEY_PASSPHRASE" ]; then
        SEQCLI_ARGS+=("-p" "PrivateKeyPassphrase=$PRIVATE_KEY_PASSPHRASE")
    fi
fi

# Add optional test directory
if [ -n "$TEST_DIRECTORY" ]; then
    SEQCLI_ARGS+=("-p" "TestDirectoryPath=$TEST_DIRECTORY")
fi

# Add Seq API key if set
if [ -n "$SEQ_API_KEY" ]; then
    SEQCLI_ARGS+=("--seq-apikey=$SEQ_API_KEY")
fi

# Display configuration
echo -e "${CYAN}Configuration:${NC}"
echo -e "${GRAY}  Seq URL:          ${SEQ_URL}${NC}"
echo -e "${GRAY}  SFTP Host:        ${SFTP_HOST}:${SFTP_PORT}${NC}"
echo -e "${GRAY}  Username:         ${SFTP_USERNAME}${NC}"
echo -e "${GRAY}  Auth Method:      ${AUTH_METHOD}${NC}"
echo -e "${GRAY}  Check Interval:   ${CHECK_INTERVAL}s${NC}"
echo -e "${GRAY}  Test Directory:   ${TEST_DIRECTORY:-"(none)"}${NC}"
echo ""

echo -e "${YELLOW}run: Starting Seq app locally...${NC}"
echo -e "${YELLOW}     Press Ctrl+C to stop${NC}"
echo ""
echo -e "${GRAY}-------------------------------------------${NC}"
echo ""

# Run the app
seqcli "${SEQCLI_ARGS[@]}"
