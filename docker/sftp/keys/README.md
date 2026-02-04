# SSH Test Keys for SFTP Key-Based Authentication

This directory contains SSH key pairs used for testing the SFTP connectivity check app with key-based authentication.

## Files

- `test_key` - Private key (ED25519 format, no passphrase)
- `test_key.pub` - Public key

## ⚠️ Security Warning

**These keys are for LOCAL TESTING ONLY!**

- Do NOT use these keys in production
- Do NOT commit real/sensitive keys to version control
- These keys have no passphrase for ease of testing

## Usage

### Docker SFTP Server (sftp-keyauth)

The `sftp-keyauth` service in `docker-compose.yml` is configured to accept connections from users authenticating with these keys:

- **Host**: `localhost` (from host) or `sftp-keyauth` (from within Docker network)
- **Port**: `2223` (from host) or `22` (from within Docker network)
- **Username**: `keyuser`
- **Authentication**: SSH private key (`test_key`)

### Converting Private Key to Base64

The Seq app requires the private key to be Base64-encoded. Use one of these commands:

**PowerShell:**
```powershell
[Convert]::ToBase64String([System.IO.File]::ReadAllBytes("./docker/sftp/keys/test_key"))
```

**Linux/macOS:**
```bash
base64 -w 0 ./docker/sftp/keys/test_key
```

### Seq App Configuration

When configuring the SFTP Check app in Seq for key-based authentication:

| Setting | Value |
|---------|-------|
| Host Name | `sftp-keyauth` (Docker) or `localhost` (host) |
| Port | `22` (Docker) or `2223` (host) |
| Username | `keyuser` |
| Authentication Method | `PrivateKey` |
| Private Key (Base64) | Output from base64 command above |
| Private Key Passphrase | *(leave empty)* |

### Testing Connection Manually

From your host machine:
```bash
ssh -i ./docker/sftp/keys/test_key -p 2223 keyuser@localhost
```

Or using sftp:
```bash
sftp -i ./docker/sftp/keys/test_key -P 2223 keyuser@localhost
```

## Regenerating Keys

If you need to regenerate the test keys:

```bash
ssh-keygen -t ed25519 -f ./docker/sftp/keys/test_key -N "" -C "test@sftpcheck"
```

After regenerating, restart the Docker containers:
```bash
docker compose down
docker compose up -d
```
