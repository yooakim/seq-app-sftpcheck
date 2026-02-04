#!/bin/bash
# build.sh - Build and package Seq.App.SftpCheck
#
# Usage:
#   ./build.sh          # Build and package
#   ./build.sh --test   # Build, test, and package
#   ./build.sh --publish # Build, test, package, and publish to NuGet
#
# Environment variables:
#   NUGET_API_KEY    - NuGet API key for publishing
#   CI_TARGET_BRANCH - Target branch (for CI builds)
#   CI_BUILD_NUMBER  - Build number (for CI builds)

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Get script directory and change to it
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Parse arguments
RUN_TESTS=false
PUBLISH=false

for arg in "$@"; do
    case $arg in
        --test)
            RUN_TESTS=true
            ;;
        --publish)
            RUN_TESTS=true
            PUBLISH=true
            ;;
        --help|-h)
            echo "Usage: ./build.sh [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --test      Run tests after building"
            echo "  --publish   Build, test, and publish to NuGet"
            echo "  --help      Show this help message"
            exit 0
            ;;
    esac
done

echo -e "${CYAN}========================================"
echo -e " Build started"
echo -e "========================================${NC}"
echo ""

echo -e "${YELLOW}build: Tool versions${NC}"
dotnet --version
dotnet --list-sdks
echo ""

# Clean artifacts directory
if [ -d "./artifacts" ]; then
    echo -e "${YELLOW}build: Cleaning ./artifacts${NC}"
    rm -rf ./artifacts
fi

# Restore packages
echo -e "${YELLOW}build: Restoring packages${NC}"
dotnet restore --no-cache
echo ""

# Read version prefix from Directory.Build.props
VERSION_PREFIX=$(grep -oP '(?<=<VersionPrefix>)[^<]+' Directory.Build.props)
echo -e "${YELLOW}build: Package version prefix is ${VERSION_PREFIX}${NC}"

# Calculate version suffix
if [ -n "$CI_TARGET_BRANCH" ]; then
    BRANCH="$CI_TARGET_BRANCH"
else
    BRANCH=$(git symbolic-ref --short -q HEAD 2>/dev/null || echo "unknown")
fi

if [ -n "$CI_BUILD_NUMBER" ]; then
    REVISION=$(printf "%05d" "$CI_BUILD_NUMBER")
else
    REVISION="local"
fi

# Determine version suffix
if [ "$BRANCH" = "main" ] && [ "$REVISION" != "local" ]; then
    VERSION_SUFFIX=""
else
    # Sanitize branch name: remove non-alphanumeric chars except hyphen, limit to 10 chars
    BRANCH_SANITIZED=$(echo "$BRANCH" | sed 's/[^a-zA-Z0-9-]//g' | cut -c1-10)
    VERSION_SUFFIX="${BRANCH_SANITIZED}-${REVISION}"
fi

if [ -n "$VERSION_SUFFIX" ]; then
    FULL_VERSION="${VERSION_PREFIX}-${VERSION_SUFFIX}"
    echo -e "${YELLOW}build: Package version suffix is ${VERSION_SUFFIX}${NC}"
else
    FULL_VERSION="${VERSION_PREFIX}"
    echo -e "${YELLOW}build: No version suffix (release build)${NC}"
fi

echo -e "${GREEN}build: Full version is ${FULL_VERSION}${NC}"
echo ""

# Build and package each project in src/
for src_dir in src/*/; do
    if [ -d "$src_dir" ]; then
        echo -e "${YELLOW}build: Packaging project in ${src_dir}${NC}"

        pushd "$src_dir" > /dev/null

        if [ -n "$VERSION_SUFFIX" ]; then
            dotnet publish -c Release -o ./obj/publish --version-suffix "$VERSION_SUFFIX"
            dotnet pack -c Release -o ../../artifacts --no-build --version-suffix "$VERSION_SUFFIX"
        else
            dotnet publish -c Release -o ./obj/publish
            dotnet pack -c Release -o ../../artifacts --no-build
        fi

        popd > /dev/null
        echo ""
    fi
done

# Build complete solution
echo -e "${YELLOW}build: Building complete solution${NC}"
dotnet build -c Release
echo ""

# Run tests if requested
if [ "$RUN_TESTS" = true ]; then
    for test_dir in test/*.Tests/; do
        if [ -d "$test_dir" ]; then
            echo -e "${YELLOW}build: Testing project in ${test_dir}${NC}"

            pushd "$test_dir" > /dev/null
            dotnet test -c Release --no-build
            popd > /dev/null
            echo ""
        fi
    done
fi

echo -e "${GREEN}========================================"
echo -e " Build completed successfully!"
echo -e "========================================${NC}"
echo ""
echo -e "${CYAN}Package(s) available in ./artifacts:${NC}"
ls -la ./artifacts/*.nupkg 2>/dev/null || echo "  No packages found"
echo ""

# Publish to NuGet if requested and API key is available
if [ "$PUBLISH" = true ]; then
    if [ -n "$NUGET_API_KEY" ]; then
        echo -e "${YELLOW}build: Publishing NuGet packages${NC}"

        for nupkg in ./artifacts/*.nupkg; do
            if [ -f "$nupkg" ]; then
                echo "build: Pushing $nupkg"
                dotnet nuget push "$nupkg" \
                    --api-key "$NUGET_API_KEY" \
                    --source https://api.nuget.org/v3/index.json \
                    --skip-duplicate
            fi
        done

        echo -e "${GREEN}build: Publishing completed${NC}"
    else
        echo -e "${RED}build: NUGET_API_KEY not set, skipping publish${NC}"
        exit 1
    fi
fi
