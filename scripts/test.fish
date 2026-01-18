#!/usr/bin/env fish

# Test runner script for PassMan project
# Usage: ./scripts/test.fish [docker|local|watch|clean]

set mode $argv[1]

switch $mode
    case docker
        echo "🐳 Running tests in Docker..."
        docker-compose run --rm test
        
    case watch
        echo "👀 Running tests in Docker with watch mode..."
        docker-compose run --rm test dotnet watch test PassManAPI.Tests/PassManAPI.Tests.csproj
        
    case local
        echo "🧪 Running tests locally..."
        echo "🧹 Cleaning build artifacts first..."
        rm -rf PassManAPI/obj PassManAPI/bin PassManAPI.Tests/obj PassManAPI.Tests/bin
        dotnet test PassManAPI.Tests/PassManAPI.Tests.csproj
        
    case clean
        echo "🧹 Cleaning all build artifacts..."
        find . -type d \( -name "obj" -o -name "bin" \) -exec rm -rf {} + 2>/dev/null
        echo "✅ Clean complete!"
        
    case '*'
        echo "📋 PassMan Test Runner"
        echo ""
        echo "Usage: ./scripts/test.fish [mode]"
        echo ""
        echo "Modes:"
        echo "  docker  - Run tests in Docker (recommended, no permission issues)"
        echo "  watch   - Run tests in Docker with watch mode (auto-rerun on changes)"
        echo "  local   - Run tests locally (cleans first to avoid permission issues)"
        echo "  clean   - Clean all build artifacts"
        echo ""
        echo "Examples:"
        echo "  ./scripts/test.fish docker"
        echo "  ./scripts/test.fish watch"
        echo "  ./scripts/test.fish local"
end
