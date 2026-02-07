# Copilot Instructions for Seq.App.SftpCheck

This is a Seq app that periodically checks SFTP host connectivity and logs results. It's packaged as a NuGet package that gets installed into Seq instances.

## Build, Test, and Package

### Standard Commands
```bash
# Build the solution
dotnet build

# Run all tests
dotnet test

# Build and package
./build.sh

# Build, test, and package
./build.sh --test

# Format code (required before commit)
dotnet format
```

### Single Test Execution
```bash
# Run a specific test class
dotnet test --filter "FullyQualifiedName~SftpCheckAppTests"

# Run a specific test method
dotnet test --filter "FullyQualifiedName~SftpCheckAppTests.App_HasCorrectDefaultPort"
```

### Local Testing Environment
The project includes Docker Compose for end-to-end testing with real SFTP servers:

```bash
# Start Seq + 2 test SFTP servers (password & key auth)
./test-local.sh start

# Build and test
./test-local.sh build

# Stop environment
./test-local.sh stop

# Clean everything including volumes
./test-local.sh clean
```

Access Seq at http://localhost:5341 after starting the environment.

## Architecture

### Seq App Framework
This app uses the Seq.Apps framework which provides:
- `SeqApp` base class with lifecycle methods (`OnAttached()`, `OnDetached()`)
- `ISubscribeToAsync<LogEventData>` interface for event-driven triggers (though this app is primarily timer-based)
- `SeqAppSetting` attributes for configurable properties shown in Seq UI
- `Log` property for writing structured logs to Seq

### App Lifecycle
1. **OnAttached()**: Called when app instance is created/started in Seq
   - Validates settings (required fields, auth method)
   - Starts a `System.Timers.Timer` for periodic checks (default 5 minutes)
   - Performs an initial check immediately
2. **Timer-based checks**: `PerformSftpCheckAsync()` runs on each timer interval
   - Uses lock to prevent overlapping checks
   - Connects to SFTP host using SSH.NET library
   - Optionally lists a test directory if configured
   - Logs success (informational) or failure (error) with structured properties
3. **OnDetached()**: Called when app is stopped/removed

### Key Dependencies
- **SSH.NET**: Pure managed C# library for SSH/SFTP operations
  - Supports password and private key authentication
  - Private keys must be Base64-encoded for secure storage in Seq settings
- **Seq.Apps**: Framework for building Seq apps (netstandard2.0)

### Packaging
Seq apps have special packaging requirements:
- Target `netstandard2.0` for compatibility with Seq's runtime
- All dependencies must be included as **files** in the NuGet package, not as package references
- The `Seq.App.SftpCheck.csproj` uses `dotnet publish` output to include SSH.NET DLLs
- Package structure: `lib/netstandard2.0/` contains all runtime dependencies
- Version is managed via `Directory.Build.props` with optional suffixes for dev builds

## Code Conventions

### C# Style
- Target: C# 12+ with latest language features
- Nullable reference types are enabled and treated as errors
- Use `var` when type is obvious from right-hand side
- XML documentation comments required for public APIs
- Braces: Opening brace on new line for types/methods, same line for control flow

### Authentication Patterns
The app supports two auth methods distinguished by `AuthenticationMethod` setting:
- **Password**: Direct credential in `Password` setting
- **PrivateKey**: Base64-encoded key in `PrivateKeyBase64`, optional `PrivateKeyPassphrase`

When adding auth logic, follow the pattern in `CreateConnectionInfo()` method which branches on normalized auth method string.

### Settings Pattern
Use `SeqAppSetting` attribute with:
- `DisplayName`: User-friendly label shown in Seq UI
- `HelpText`: Detailed description with defaults and examples
- `IsOptional`: `true` for optional settings (required settings show validation errors in UI)
- `InputType`: Use `SettingInputType.Password` for secrets, `SettingInputType.LongText` for large inputs

### Structured Logging
Always use structured logging with named properties:
```csharp
Log.Information("SFTP check succeeded for {DisplayName} ({SftpHost}:{SftpPort}). Connect: {ConnectDurationMs}ms",
    DisplayName, SftpHost, Port, connectDuration.TotalMilliseconds);
```

Properties used in log events become searchable fields in Seq. Key properties:
- `DisplayName`: Friendly name for the host
- `SftpHost`: Hostname/IP
- `SftpPort`: Port number
- `ConnectDurationMs`: Connection time
- `FileCount`: Optional file count if directory listing enabled
- `ErrorMessage`: Error details on failure

## Testing

### Test Structure
- **Unit tests**: `SftpCheckAppTests.cs` - Basic property validation, settings validation
- **Integration tests**: `SftpKeyAuthIntegrationTests.cs` - Real SFTP connections to Docker containers

### Test Keys
Pre-generated ED25519 and RSA keys in `docker/sftp/keys/` for local testing only. Never use in production.

To regenerate test keys:
```bash
ssh-keygen -t ed25519 -f ./docker/sftp/keys/test_key -N "" -C "test@sftpcheck"
```

### Mocking
Use Moq for mocking external dependencies. Test framework: xUnit with Seq.Apps.Testing helpers.

## Branching & CI

### Branching Strategy
- `main`: Stable releases, maps to NuGet releases
- `dev`: Development branch, PRs target here
- `feature/*` or `fix/*`: Feature/fix branches

### CI Workflows
- **ci.yml**: Runs on push/PR to main/dev - builds, tests, packages
- **pr-validation.yml**: Enforces `dotnet format` and other checks
- **release.yml**: Triggered by version tags (v*) - publishes to NuGet
- **version-bump.yml**: Automated version management

### Version Format
- Release (main): `0.1.0`
- Dev builds: `0.1.0-dev-00123` (branch-buildnumber)
- Version prefix defined in `Directory.Build.props`

## Common Tasks

### Adding a New Setting
1. Add property with `SeqAppSetting` attribute in `SftpCheckApp.cs`
2. Update validation in `ValidateSettings()` if required
3. Use the setting in `PerformSftpCheckAsync()` or `CreateConnectionInfo()`
4. Update README.md configuration table
5. Add test coverage in `SftpCheckAppTests.cs`

### Modifying Authentication
Authentication logic is centralized in `CreateConnectionInfo()` method. When adding new auth methods:
1. Add new setting properties with appropriate `InputType`
2. Extend `ValidateSettings()` validation
3. Add branch in `CreateConnectionInfo()` for new `AuthenticationMethod` value
4. Update README.md with usage example and Base64 conversion commands

### Adding Directory Operations
Current implementation supports optional directory listing via `TestDirectoryPath`. To extend:
1. SFTP operations must run in `Task.Run()` since SSH.NET is synchronous
2. Use `ConfigureAwait(false)` for async/await patterns
3. Handle exceptions gracefully - connection errors should not crash the app
4. Log structured data with relevant file/directory properties
