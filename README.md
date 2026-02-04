# Seq.App.SftpCheck

[![CI/CD](https://github.com/yooakim/seq-app-sftpcheck/actions/workflows/ci.yml/badge.svg)](https://github.com/yooakim/seq-app-sftpcheck/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Seq.App.SftpCheck.svg)](https://www.nuget.org/packages/Seq.App.SftpCheck)
[![License](https://img.shields.io/github/license/yooakim/seq-app-sftpcheck)](LICENSE)

A [Seq](https://datalust.co/seq) app that periodically checks SFTP host connectivity and logs the results. Perfect for monitoring SFTP server availability and health.

## Features

- **Scheduled Connectivity Checks**: Automatically checks SFTP host connectivity at configurable intervals
- **Multiple Authentication Methods**: Supports both password and private key authentication
- **Connection Metrics**: Logs connection duration for performance monitoring
- **Directory Listing Tests**: Optionally verify read access by listing a directory
- **Flexible Logging**: Configure whether to log successful checks or only failures
- **Friendly Names**: Assign readable names to hosts for clearer log messages

## Installation

### From NuGet

1. In Seq, navigate to **Settings** → **Apps**
2. Click **Install from NuGet**
3. Enter the package ID: `Seq.App.SftpCheck`
4. Click **Install**

### From Source

1. Clone the repository
2. Run `.\Build.ps1` to build and package
3. Upload the `.nupkg` from the `artifacts` folder to your Seq instance

## Configuration

Create an app instance for each SFTP host you want to monitor. Each instance supports the following settings:

### Required Settings

| Setting | Description |
|---------|-------------|
| **Host Name** | The hostname or IP address of the SFTP server |
| **Username** | The username for SFTP authentication |

### Authentication Settings

| Setting | Description | Default |
|---------|-------------|---------|
| **Authentication Method** | `Password` or `PrivateKey` | `Password` |
| **Password** | Password for authentication (required if using password auth) | - |
| **Private Key (Base64)** | Base64-encoded private key content (required if using key auth) | - |
| **Private Key Passphrase** | Passphrase for encrypted private keys | - |

### Optional Settings

| Setting | Description | Default |
|---------|-------------|---------|
| **Port** | SFTP port number | `22` |
| **Check Interval (seconds)** | How often to perform connectivity checks (minimum: 30) | `300` (5 min) |
| **Connection Timeout (seconds)** | Timeout for connection attempts | `30` |
| **Test Directory Path** | Directory to list as an additional connectivity test | - |
| **Friendly Name** | A readable name for this host in log messages | Host name |
| **Log Successful Checks** | Whether to log informational messages on success | `true` |

## Usage Examples

### Password Authentication

1. Add a new instance of **SFTP Connectivity Check**
2. Configure:
   - **Host Name**: `sftp.example.com`
   - **Username**: `myuser`
   - **Authentication Method**: `Password`
   - **Password**: `mypassword`
   - **Friendly Name**: `Production SFTP Server`

### Private Key Authentication

1. Convert your private key to Base64:
   ```powershell
   [Convert]::ToBase64String([System.IO.File]::ReadAllBytes("C:\path\to\private_key"))
   ```
   Or on Linux/macOS:
   ```bash
   base64 -w 0 /path/to/private_key
   ```

2. Add a new instance of **SFTP Connectivity Check**
3. Configure:
   - **Host Name**: `sftp.example.com`
   - **Username**: `myuser`
   - **Authentication Method**: `PrivateKey`
   - **Private Key (Base64)**: `[paste your base64 key]`
   - **Private Key Passphrase**: `[if your key is encrypted]`
   - **Friendly Name**: `Backup SFTP Server`

### Setting Up Alerts

Since the app logs error events when connectivity checks fail, you can create Seq alerts:

1. Create a new Signal with the filter:
   ```
   @Level = 'Error' and SftpHost is not null
   ```

2. Set up notifications (email, Slack, etc.) for this signal

## Log Events

### Successful Check

```
SFTP check succeeded for Production SFTP (sftp.example.com:22). Connect: 245ms
```

Properties:
- `DisplayName`: Friendly name or hostname
- `SftpHost`: Hostname
- `SftpPort`: Port number
- `ConnectDurationMs`: Connection time in milliseconds
- `FileCount`: Number of files (if directory listing enabled)

### Failed Check

```
SFTP check failed for Production SFTP (sftp.example.com:22) after 30000ms: Connection timed out
```

Properties:
- `DisplayName`: Friendly name or hostname
- `SftpHost`: Hostname
- `SftpPort`: Port number
- `DurationMs`: Time before failure
- `ErrorMessage`: Error description
- Exception details

## Development

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [seqcli](https://github.com/datalust/seqcli) for local testing
- A running Seq instance (for testing)

### Building

```bash
# Restore and build
dotnet build

# Run tests
dotnet test

# Create NuGet package
./build.sh

# Or with tests
./build.sh --test
```

### Local Testing with Docker (Recommended)

The project includes a Docker Compose setup with Seq and two test SFTP servers (one for password auth, one for SSH key auth):

1. Start the environment:
   ```bash
   ./test-local.sh start
   ```

2. Build the app:
   ```bash
   ./build.sh --test
   ```

3. Open Seq at http://localhost:5341

4. Install the app:
   - Go to **Settings** → **Apps** → **Install from NuGet**
   - Upload the `.nupkg` from the `./artifacts` folder

5. Add app instances for testing:

#### Password Authentication Instance

- **Host Name**: `sftp` (container name, or `localhost` if using seqcli)
- **Port**: `22` (internal) or `2222` (external)
- **Username**: `testuser`
- **Authentication Method**: `Password`
- **Password**: `testpass`
- **Test Directory Path**: `upload`

#### SSH Key Authentication Instance

- **Host Name**: `sftp-keyauth` (container name, or `localhost` if using seqcli)
- **Port**: `22` (internal) or `2223` (external)
- **Username**: `keyuser`
- **Authentication Method**: `PrivateKey`
- **Private Key (Base64)**: See below
- **Test Directory Path**: `upload`

To get the Base64-encoded private key for testing:
```bash
# Linux/macOS
base64 -w 0 ./docker/sftp/keys/test_key

# PowerShell
[Convert]::ToBase64String([System.IO.File]::ReadAllBytes("./docker/sftp/keys/test_key"))
```

#### Docker Services

| Service | Description | Access |
|---------|-------------|--------|
| **seq** | Seq log server | http://localhost:5341 |
| **sftp** | Test SFTP server (password auth) | localhost:2222 |
| **sftp-keyauth** | Test SFTP server (SSH key auth) | localhost:2223 |

#### Quick Commands

```bash
# Start environment
docker compose up -d

# View Seq logs
docker compose logs -f seq

# View SFTP server logs
docker compose logs -f sftp sftp-keyauth

# Stop environment
docker compose down

# Clean everything (including data)
docker compose down -v
```

#### SSH Key Test Setup

The project includes pre-generated test keys in `docker/sftp/keys/`:
- `test_key` - ED25519 private key (no passphrase)
- `test_key.pub` - Public key

**⚠️ These keys are for LOCAL TESTING ONLY - never use in production!**

To regenerate the test keys:
```bash
ssh-keygen -t ed25519 -f ./docker/sftp/keys/test_key -N "" -C "test@sftpcheck"
docker compose down && docker compose up -d
```

### Local Testing with seqcli

If you prefer testing without Docker or want faster iteration:

1. Edit `run.sh` with your test SFTP server details
2. Run:
   ```bash
   ./run.sh
   ```
3. View logs in your local Seq instance

### Available Scripts

| Script | Description |
|--------|-------------|
| `./build.sh` | Build and create NuGet package |
| `./build.sh --test` | Build, run tests, and package |
| `./run.sh` | Run app locally with seqcli |
| `./test-local.sh start` | Start Docker environment |
| `./test-local.sh build` | Build the app |
| `./test-local.sh test` | Test with seqcli against Docker |
| `./test-local.sh stop` | Stop Docker environment |
| `./test-local.sh clean` | Stop and remove all data |

**Note:** Make scripts executable with `chmod +x *.sh`

## Requirements

- Seq 2021.4 or later
- .NET 10.0 runtime (provided by Seq)

### Development Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (for local testing)
- [seqcli](https://github.com/datalust/seqcli) (optional): `dotnet tool install -g seqcli`

## Dependencies

- [Seq.Apps](https://www.nuget.org/packages/Seq.Apps/) - Seq app development framework
- [SSH.NET](https://www.nuget.org/packages/SSH.NET/) - Pure managed C# SSH/SFTP library

## License

Apache 2.0 - see [LICENSE](LICENSE) for details.

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

Please ensure your PR:
- Follows the existing code style
- Includes appropriate tests
- Updates documentation as needed

## Acknowledgements

- [Datalust](https://datalust.co/) for Seq and the Seq.Apps framework
- [SSH.NET](https://github.com/sshnet/SSH.NET) for the excellent SFTP library
- Inspired by [Seq.App.HttpRequest](https://github.com/datalust/seq-app-httprequest)

## Author

**Joakim Westin** ([@yooakim](https://github.com/yooakim))  
📧 [yooakim@gmail.com](mailto:yooakim@gmail.com)
