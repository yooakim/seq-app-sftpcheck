# Contributing to Seq.App.SftpCheck

This document provides guidelines and information for contributors.

## Table of Contents

- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [How to Contribute](#how-to-contribute)
- [Pull Request Process](#pull-request-process)
- [Coding Standards](#coding-standards)
- [Testing Guidelines](#testing-guidelines)
- [Contact](#contact)

## Getting Started

1. Fork the repository on GitHub
2. Clone your fork locally
3. Set up the development environment (see below)
4. Create a feature branch for your changes
5. Make your changes and test them
6. Submit a pull request

## Development Setup

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) (for local testing) - Docker Engine on Linux or Docker Desktop on Windows/macOS(for local testing)
- [seqcli](https://github.com/datalust/seqcli) (optional): `dotnet tool install -g seqcli`
- A code editor (Zed, VS Code, Visual Studio, Rider, etc.)

### Setting Up the Development Environment

1. Clone the repository:
   ```bash
   git clone https://github.com/yooakim/seq-app-sftpcheck.git
   cd seq-app-sftpcheck
   ```

2. Restore dependencies:
   ```bash
   dotnet restore
   ```

3. Build the solution:
   ```bash
   dotnet build
   ```

4. Run tests:
   ```bash
   dotnet test
   ```

### Local Testing with Docker

The project includes a Docker Compose setup for local testing:

```bash
# Start the environment (Seq + test SFTP server)
./test-local.sh start

# Build the app
./build.sh --test

# View status
./test-local.sh status

# Stop the environment
./test-local.sh stop
```

**Note:** Make scripts executable first: `chmod +x *.sh`

## How to Contribute

### Reporting Bugs

- Use the [bug report template](https://github.com/yooakim/seq-app-sftpcheck/issues/new?template=bug_report.yml)
- Include as much detail as possible
- Provide steps to reproduce the issue
- Include relevant log output from Seq

### Suggesting Features

- Use the [feature request template](https://github.com/yooakim/seq-app-sftpcheck/issues/new?template=feature_request.yml)
- Describe your use case
- Explain why this feature would be useful

### Code Contributions

1. Check existing issues and PRs to avoid duplicate work
2. For significant changes, open an issue first to discuss
3. Follow the coding standards below
4. Include tests for new functionality
5. Update documentation as needed

## Pull Request Process

1. **Create a feature branch** from `dev`:
   ```bash
   git checkout dev
   git pull origin dev
   git checkout -b feature/your-feature-name
   ```

2. **Make your changes** following the coding standards

3. **Test your changes**:
   ```bash
   ./build.sh --test
   ```

4. **Commit with clear messages**:
   ```bash
   git commit -m "Add feature: description of changes"
   ```

5. **Push and create a PR**:
   ```bash
   git push origin feature/your-feature-name
   ```
   Then create a pull request on GitHub targeting the `dev` branch.

6. **Address review feedback** if requested

### PR Requirements

- [ ] All tests pass
- [ ] Code follows project style guidelines
- [ ] New features include tests
- [ ] Documentation is updated
- [ ] PR description clearly explains changes

## Coding Standards

### C# Style

- Use C# 12+ features where appropriate
- Enable nullable reference types
- Use `var` when the type is obvious
- Use meaningful names for variables, methods, and classes
- Keep methods focused and small
- Add XML documentation for public APIs

### Example

```csharp
/// <summary>
/// Checks connectivity to the configured SFTP host.
/// </summary>
/// <returns>A task representing the asynchronous operation.</returns>
private async Task CheckSftpConnectivityAsync()
{
    var stopwatch = Stopwatch.StartNew();
    
    try
    {
        using var client = new SftpClient(CreateConnectionInfo());
        await Task.Run(() => client.Connect()).ConfigureAwait(false);
        
        Log.Information("SFTP check succeeded for {Host}", Host);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "SFTP check failed for {Host}", Host);
    }
}
```

### Formatting

- Run `dotnet format` before committing
- Use 4 spaces for indentation
- Place opening braces on new lines for types, same line for statements

## Testing Guidelines

### Unit Tests

- Use xUnit for unit tests
- Follow the Arrange-Act-Assert pattern
- Test both success and failure cases
- Mock external dependencies (SFTP connections)

### Integration Tests

- Test against the Docker SFTP server
- Verify both password and private key authentication
- Test error handling scenarios

### Example Test

```csharp
[Fact]
public void App_HasCorrectDefaultPort()
{
    // Arrange
    var app = new SftpCheckApp();
    
    // Act & Assert
    Assert.Equal(22, app.Port);
}
```

## Branching Strategy

- `main` - Stable releases only
- `dev` - Development branch, PRs target here
- `feature/*` - Feature branches
- `fix/*` - Bug fix branches

## Release Process

Releases are automated through GitHub Actions:

1. Update version in `Directory.Build.props`
2. Merge to `main`
3. Create a tag: `git tag v1.0.0`
4. Push the tag: `git push origin v1.0.0`

## Contact

- **Author**: Joakim Westin
- **Email**: [yooakim@gmail.com](mailto:yooakim@gmail.com)
- **GitHub**: [@yooakim](https://github.com/yooakim)

For security-related issues, please see our [Security Policy](/.github/SECURITY.md).

## License

By contributing to Seq.App.SftpCheck, you agree that your contributions will be licensed under the [Apache License 2.0](LICENSE).

---

Thank you for contributing! Your help is greatly appreciated.
