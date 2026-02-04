#!/bin/bash
# test-local.sh - Local Docker-based testing workflow for Seq.App.SftpCheck
#
# This script helps you:
#   1. Start/stop the Docker Compose environment (Seq + SFTP server)
#   2. Build and package the Seq app
#   3. Test the app locally using seqcli
#
# Prerequisites:
#   - Docker with Docker Compose
#   - .NET 10 SDK
#   - seqcli (optional, for local app testing without installing in Seq)
#
# Usage:
#   ./test-local.sh start     # Start Docker containers
#   ./test-local.sh stop      # Stop Docker containers
#   ./test-local.sh build     # Build and package the app
#   ./test-local.sh test      # Run the app locally with seqcli
#   ./test-local.sh all       # Start containers, build, and test
#   ./test-local.sh status    # Show container status
#   ./test-local.sh logs      # Show Seq logs
#   ./test-local.sh clean     # Stop containers and remove volumes
#   ./test-local.sh help      # Show this help message

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
WHITE='\033[1;37m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

# Configuration
SEQ_URL="http://localhost:5341"
SFTP_HOST="localhost"
SFTP_PORT=2222
SFTP_USERNAME="testuser"
SFTP_PASSWORD="testpass"
TEST_DIRECTORY="/upload"
CHECK_INTERVAL_SECONDS=30  # Faster for testing

# Get script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Helper functions
print_header() {
    echo ""
    echo -e "${CYAN}========================================${NC}"
    echo -e "${CYAN} $1${NC}"
    echo -e "${CYAN}========================================${NC}"
    echo ""
}

print_success() {
    echo -e "${GREEN}$1${NC}"
}

print_warning() {
    echo -e "${YELLOW}$1${NC}"
}

print_error() {
    echo -e "${RED}$1${NC}"
}

print_info() {
    echo -e "${WHITE}$1${NC}"
}

print_gray() {
    echo -e "${GRAY}$1${NC}"
}

# Check if Docker is running
check_docker() {
    if ! docker info &> /dev/null; then
        print_error "Docker is not running. Please start Docker and try again."
        exit 1
    fi
}

# Start the Docker environment
start_environment() {
    print_header "Starting Docker Environment"

    check_docker

    print_warning "Starting Seq and SFTP containers..."
    docker compose up -d

    echo ""
    print_warning "Waiting for services to be ready..."
    sleep 5

    # Check if Seq is responding
    local max_attempts=30
    local attempt=0
    while [ $attempt -lt $max_attempts ]; do
        if curl -s -o /dev/null -w "%{http_code}" "$SEQ_URL/api" 2>/dev/null | grep -q "200"; then
            print_success "Seq is ready!"
            break
        fi
        attempt=$((attempt + 1))
        echo -n "."
        sleep 1
    done
    echo ""

    if [ $attempt -ge $max_attempts ]; then
        print_warning "Seq may not be fully ready yet. Check $SEQ_URL"
    fi

    # Add local NuGet feed if it doesn't exist
    print_warning "Configuring local NuGet feed..."
    local feed_exists=$(curl -s "$SEQ_URL/api/feeds" | grep -c "Local Packages" || true)
    if [ "$feed_exists" = "0" ]; then
        curl -s -X POST "$SEQ_URL/api/feeds" \
            -H "Content-Type: application/json" \
            -d '{"Name":"Local Packages","Location":"/packages"}' > /dev/null
        print_success "Local NuGet feed added (/packages)"
    else
        print_gray "Local NuGet feed already configured"
    fi

    echo ""
    print_success "Environment is running:"
    print_info "  Seq UI:      $SEQ_URL"
    print_info "  SFTP Server: $SFTP_HOST:$SFTP_PORT"
    print_info "  SFTP User:   $SFTP_USERNAME / $SFTP_PASSWORD"
    echo ""
    print_warning "To install the app in Seq:"
    print_info "  1. Run: ./test-local.sh build"
    print_info "  2. Install via CLI: seqcli app install --package-id=Seq.App.SftpCheck --feed-id=\$(seqcli feed list -s $SEQ_URL --json | grep -o 'nugetfeed-[0-9]*' | tail -1) -s $SEQ_URL"
    print_info "  Or via UI:"
    print_info "  3. Open $SEQ_URL -> Settings -> Apps -> Install"
    print_info "  4. Select 'Local Packages' feed and search for Seq.App.SftpCheck"
    echo ""
}

# Stop the Docker environment
stop_environment() {
    print_header "Stopping Docker Environment"

    docker compose down

    print_success "Environment stopped."
}

# Clean the Docker environment (including volumes)
clean_environment() {
    print_header "Cleaning Docker Environment"

    print_warning "Stopping containers and removing volumes..."
    docker compose down -v

    echo ""
    print_warning "Cleaning build artifacts..."
    rm -rf ./artifacts
    rm -rf ./src/Seq.App.SftpCheck/bin
    rm -rf ./src/Seq.App.SftpCheck/obj
    rm -rf ./test/Seq.App.SftpCheck.Tests/bin
    rm -rf ./test/Seq.App.SftpCheck.Tests/obj

    print_success "Environment cleaned."
}

# Build the app
build_app() {
    print_header "Building Seq App"

    # Check for .NET SDK
    if ! command -v dotnet &> /dev/null; then
        print_error ".NET SDK is not installed or not in PATH"
        exit 1
    fi

    local dotnet_version=$(dotnet --version)
    print_gray "Using .NET SDK: $dotnet_version"

    print_warning "Restoring packages..."
    dotnet restore

    print_warning "Building solution..."
    dotnet build -c Release

    print_warning "Running tests..."
    if ! dotnet test -c Release --no-build; then
        print_warning "Some tests failed, but continuing..."
    fi

    print_warning "Publishing for packaging..."
    pushd ./src/Seq.App.SftpCheck > /dev/null
    dotnet publish -c Release -o ./obj/publish

    print_warning "Creating NuGet package..."
    dotnet pack -c Release -o ../../artifacts --no-build
    popd > /dev/null

    echo ""
    print_success "Build completed successfully!"
    echo ""
    print_info "Package location:"
    for pkg in ./artifacts/*.nupkg; do
        echo -e "  ${CYAN}$(realpath "$pkg")${NC}"
    done
    echo ""
}

# Test the app with seqcli
test_app() {
    print_header "Testing App with seqcli"

    # Check for seqcli
    if ! command -v seqcli &> /dev/null; then
        print_warning "seqcli is not installed. Install with: dotnet tool install -g seqcli"
        echo ""
        print_warning "Alternative: Install the app manually in Seq:"
        print_info "  1. Open $SEQ_URL"
        print_info "  2. Go to Settings > Apps"
        print_info "  3. Click 'Install from NuGet'"
        print_info "  4. Upload the .nupkg from ./artifacts"
        exit 1
    fi

    local app_dir="./src/Seq.App.SftpCheck/bin/Release/net6.0"
    local dll_file="$app_dir/Seq.App.SftpCheck.dll"

    if [ ! -f "$dll_file" ]; then
        # Try Debug build
        app_dir="./src/Seq.App.SftpCheck/bin/Debug/net6.0"
        dll_file="$app_dir/Seq.App.SftpCheck.dll"
    fi

    if [ ! -f "$dll_file" ]; then
        print_warning "App not built yet. Building..."
        dotnet build ./src/Seq.App.SftpCheck -c Debug
        app_dir="./src/Seq.App.SftpCheck/bin/Debug/net6.0"
        dll_file="$app_dir/Seq.App.SftpCheck.dll"
    fi

    if [ ! -f "$dll_file" ]; then
        print_error "Could not find built assembly"
        exit 1
    fi

    print_warning "Running app locally with seqcli..."
    echo ""
    print_info "Configuration:"
    print_gray "  Seq URL:        $SEQ_URL"
    print_gray "  SFTP Host:      $SFTP_HOST:$SFTP_PORT"
    print_gray "  SFTP User:      $SFTP_USERNAME"
    print_gray "  Check Interval: ${CHECK_INTERVAL_SECONDS}s"
    print_gray "  Test Directory: $TEST_DIRECTORY"
    echo ""
    print_warning "Press Ctrl+C to stop"
    echo ""

    seqcli app run \
        -d "$app_dir" \
        --type "Seq.App.SftpCheck.SftpCheckApp" \
        -p "SftpHost=$SFTP_HOST" \
        -p "Port=$SFTP_PORT" \
        -p "Username=$SFTP_USERNAME" \
        -p "AuthenticationMethod=Password" \
        -p "Password=$SFTP_PASSWORD" \
        -p "CheckIntervalSeconds=$CHECK_INTERVAL_SECONDS" \
        -p "ConnectionTimeoutSeconds=30" \
        -p "TestDirectoryPath=$TEST_DIRECTORY" \
        -p "FriendlyName=Docker Test SFTP" \
        -p "LogSuccessfulChecks=true" \
        -s "$SEQ_URL"
}

# Install the app in Seq
install_app() {
    print_header "Installing App in Seq"

    # Check if seqcli is available
    if ! command -v seqcli &> /dev/null; then
        print_error "seqcli is not installed. Install with: dotnet tool install -g seqcli"
        exit 1
    fi

    # Check if package exists
    local pkg_file=$(ls -t ./artifacts/Seq.App.SftpCheck.*.nupkg 2>/dev/null | head -1)
    if [ -z "$pkg_file" ]; then
        print_error "No package found in ./artifacts. Run './test-local.sh build' first."
        exit 1
    fi

    print_info "Package: $pkg_file"

    # Restart Seq container to refresh bind mount (ensures it sees the latest package)
    print_warning "Restarting Seq container to refresh package feed..."
    docker compose restart seq > /dev/null 2>&1
    sleep 3

    # Get the local feed ID (find the "Local Packages" feed)
    local feed_id=$(seqcli feed list -s "$SEQ_URL" --json 2>/dev/null | grep "Local Packages" | grep -o '"Id": *"nugetfeed-[0-9]*"' | grep -o 'nugetfeed-[0-9]*')

    if [ -z "$feed_id" ]; then
        print_error "Local Packages feed not found. Run './test-local.sh start' first."
        exit 1
    fi

    print_info "Using feed: $feed_id"

    # Extract version from package filename
    local version=$(echo "$pkg_file" | sed -n 's/.*Seq\.App\.SftpCheck\.\(.*\)\.nupkg/\1/p')
    print_info "Version: $version"

    # Check if already installed
    local installed=$(seqcli app list -s "$SEQ_URL" 2>/dev/null | grep -c "SFTP Connectivity Check" || true)
    if [ "$installed" != "0" ]; then
        print_warning "App already installed. Updating..."
        seqcli app update -n "SFTP Connectivity Check" --version="$version" --force -s "$SEQ_URL"
    else
        print_warning "Installing app..."
        seqcli app install --package-id="Seq.App.SftpCheck" --version="$version" --feed-id="$feed_id" -s "$SEQ_URL"
    fi

    print_success "App installed successfully!"
    seqcli app list -s "$SEQ_URL"
}

# Create an app instance in Seq
setup_instance() {
    print_header "Setting Up App Instances"

    # Check if seqcli is available
    if ! command -v seqcli &> /dev/null; then
        print_error "seqcli is not installed. Install with: dotnet tool install -g seqcli"
        exit 1
    fi

    # Get the app ID
    local app_id=$(seqcli app list -s "$SEQ_URL" --json 2>/dev/null | grep -o '"Id": *"hostedapp-[0-9]*"' | head -1 | grep -o 'hostedapp-[0-9]*')

    if [ -z "$app_id" ]; then
        print_error "App not installed. Run './test-local.sh install' first."
        exit 1
    fi

    print_info "App ID: $app_id"

    # Setup password-based authentication instance
    setup_password_instance "$app_id"

    # Setup SSH key-based authentication instance
    setup_keyauth_instance "$app_id"

    echo ""
    seqcli appinstance list -s "$SEQ_URL"
}

# Create password-based authentication instance
setup_password_instance() {
    local app_id="$1"

    echo ""
    print_warning "Setting up password authentication instance..."

    # Check if instance already exists
    local existing=$(seqcli appinstance list -s "$SEQ_URL" 2>/dev/null | grep -c "Test SFTP Check (Password)" || true)
    if [ "$existing" != "0" ]; then
        print_gray "Instance 'Test SFTP Check (Password)' already exists."
        return 0
    fi

    # Note: Inside the Seq container, we need to use 'sftp' as the hostname (docker network)
    # and port 22 (internal port), not localhost:2222
    seqcli appinstance create \
        -t "Test SFTP Check (Password)" \
        --app "$app_id" \
        -p "SftpHost=sftp" \
        -p "Port=22" \
        -p "Username=$SFTP_USERNAME" \
        -p "AuthenticationMethod=Password" \
        -p "Password=$SFTP_PASSWORD" \
        -p "CheckIntervalSeconds=$CHECK_INTERVAL_SECONDS" \
        -p "ConnectionTimeoutSeconds=30" \
        -p "TestDirectoryPath=$TEST_DIRECTORY" \
        -p "FriendlyName=Docker Test SFTP (Password)" \
        -p "LogSuccessfulChecks=true" \
        -s "$SEQ_URL"

    print_success "Password auth instance created!"
    print_info "  SFTP Host:      sftp:22 (Docker internal)"
    print_info "  Username:       $SFTP_USERNAME"
    print_info "  Auth Method:    Password"
}

# Create SSH key-based authentication instance
setup_keyauth_instance() {
    local app_id="$1"

    echo ""
    print_warning "Setting up SSH key authentication instance..."

    # Check if instance already exists
    local existing=$(seqcli appinstance list -s "$SEQ_URL" 2>/dev/null | grep -c "Test SFTP Check (SSH Key)" || true)
    if [ "$existing" != "0" ]; then
        print_gray "Instance 'Test SFTP Check (SSH Key)' already exists."
        return 0
    fi

    # Check if private key file exists
    local key_file="./docker/sftp/keys/test_key"
    if [ ! -f "$key_file" ]; then
        print_error "SSH private key not found at $key_file"
        print_warning "Skipping SSH key authentication instance setup."
        return 1
    fi

    # Convert private key to Base64
    local private_key_base64=$(base64 "$key_file" | tr -d '\n')

    # Note: Inside the Seq container, we need to use 'sftp-keyauth' as the hostname (docker network)
    # and port 22 (internal port), not localhost:2223
    seqcli appinstance create \
        -t "Test SFTP Check (SSH Key)" \
        --app "$app_id" \
        -p "SftpHost=sftp-keyauth" \
        -p "Port=22" \
        -p "Username=keyuser" \
        -p "AuthenticationMethod=PrivateKey" \
        -p "PrivateKeyBase64=$private_key_base64" \
        -p "CheckIntervalSeconds=$CHECK_INTERVAL_SECONDS" \
        -p "ConnectionTimeoutSeconds=30" \
        -p "TestDirectoryPath=$TEST_DIRECTORY" \
        -p "FriendlyName=Docker Test SFTP (SSH Key)" \
        -p "LogSuccessfulChecks=true" \
        -s "$SEQ_URL"

    print_success "SSH key auth instance created!"
    print_info "  SFTP Host:      sftp-keyauth:22 (Docker internal)"
    print_info "  Username:       keyuser"
    print_info "  Auth Method:    PrivateKey"
}

# Full setup: install app and create instance
full_setup() {
    print_header "Full Setup"

    install_app
    echo ""
    setup_instance

    echo ""
    print_success "Setup complete! Check $SEQ_URL for log events."
}

# Show container status
show_status() {
    print_header "Environment Status"

    docker compose ps

    echo ""
    print_warning "URLs:"
    print_info "  Seq UI: $SEQ_URL"
    print_info "  SFTP:   sftp://$SFTP_USERNAME@$SFTP_HOST:$SFTP_PORT"
}

# Show logs
show_logs() {
    print_header "Seq Container Logs"

    docker compose logs -f seq
}

# Show help
show_help() {
    echo ""
    echo -e "${CYAN}Seq.App.SftpCheck - Local Testing Script${NC}"
    echo ""
    echo -e "${YELLOW}Usage:${NC}"
    echo "  ./test-local.sh start    Start Docker containers (Seq + SFTP)"
    echo "  ./test-local.sh stop     Stop Docker containers"
    echo "  ./test-local.sh build    Build and package the Seq app"
    echo "  ./test-local.sh install  Install the app in Seq"
    echo "  ./test-local.sh setup    Create a test app instance in Seq"
    echo "  ./test-local.sh test     Run the app locally with seqcli (outside Seq)"
    echo "  ./test-local.sh all      Full workflow: start, build, install, setup"
    echo "  ./test-local.sh status   Show container status"
    echo "  ./test-local.sh logs     Follow Seq container logs"
    echo "  ./test-local.sh clean    Stop containers and remove all data"
    echo ""
    echo -e "${YELLOW}Quick Start (automated):${NC}"
    echo "  ./test-local.sh all      # Does everything automatically"
    echo ""
    echo -e "${YELLOW}Quick Start (manual):${NC}"
    echo "  1. ./test-local.sh start    # Start the environment"
    echo "  2. ./test-local.sh build    # Build the app"
    echo "  3. ./test-local.sh install  # Install app in Seq"
    echo "  4. ./test-local.sh setup    # Create test instance"
    echo "  5. Open http://localhost:5341 to view logs"
    echo ""
}

# Main execution
case "${1:-help}" in
    start)
        start_environment
        ;;
    stop)
        stop_environment
        ;;
    build)
        build_app
        ;;
    install)
        install_app
        ;;
    setup)
        setup_instance
        ;;
    test)
        test_app
        ;;
    all)
        start_environment
        build_app
        full_setup
        ;;
    status)
        show_status
        ;;
    logs)
        show_logs
        ;;
    clean)
        clean_environment
        ;;
    help|--help|-h)
        show_help
        ;;
    *)
        print_error "Unknown command: $1"
        show_help
        exit 1
        ;;
esac
