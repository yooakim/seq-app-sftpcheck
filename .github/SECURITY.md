# Security Policy

## Supported Versions

The following versions of Seq.App.SftpCheck are currently being supported with security updates:

| Version | Supported          |
| ------- | ------------------ |
| 1.x.x   | :white_check_mark: |

## Reporting a Vulnerability

We take the security of Seq.App.SftpCheck seriously. If you believe you have found a security vulnerability, please report it to us as described below.

### How to Report

**Please do not report security vulnerabilities through public GitHub issues.**

Instead, please report them via one of the following methods:

1. **GitHub Security Advisories**: Use the [Security tab](https://github.com/yooakim/seq-app-sftpcheck/security/advisories/new) to privately report a vulnerability.

2. **Email**: Send an email to [yooakim@gmail.com](mailto:yooakim@gmail.com) with the subject line "Security Vulnerability Report: Seq.App.SftpCheck".

Please include the following information in your report:

- Type of vulnerability (e.g., credential exposure, authentication bypass, etc.)
- Full paths of source file(s) related to the vulnerability
- Location of the affected source code (tag/branch/commit or direct URL)
- Any special configuration required to reproduce the issue
- Step-by-step instructions to reproduce the issue
- Proof-of-concept or exploit code (if possible)
- Impact of the issue, including how an attacker might exploit it

### What to Expect

- **Acknowledgment**: We will acknowledge receipt of your vulnerability report within 48 hours.
- **Communication**: We will keep you informed of the progress towards a fix and full announcement.
- **Credit**: We will credit you in the release notes and security advisory (unless you prefer to remain anonymous).

### Response Timeline

- **Initial Response**: Within 48 hours
- **Status Update**: Within 7 days
- **Resolution Target**: Within 30 days (depending on complexity)

## Security Best Practices for Users

When using Seq.App.SftpCheck, please follow these security best practices:

### Credential Management

1. **Use strong passwords** for SFTP authentication
2. **Prefer private key authentication** over passwords when possible
3. **Protect private keys** with strong passphrases
4. **Rotate credentials** regularly

### Private Key Handling

When using private key authentication:

1. **Never commit private keys** to version control
2. **Use Base64 encoding** only for transport into Seq settings - the key is stored encrypted by Seq
3. **Use dedicated keys** for monitoring (don't reuse keys used for other purposes)
4. **Consider read-only accounts** on the SFTP server for monitoring

### Seq Configuration

1. **Restrict access** to Seq app settings (they contain credentials)
2. **Use Seq's API key system** to control who can view/modify app instances
3. **Enable audit logging** in Seq to track configuration changes
4. **Regularly review** installed app configurations

### Network Security

1. **Use firewalls** to restrict SFTP access to known IP addresses
2. **Consider VPN or private networks** for monitoring sensitive SFTP servers
3. **Monitor for unusual connection patterns**

## Known Security Considerations

### Credential Storage

- Passwords and private keys are stored in Seq's encrypted settings store
- Base64-encoded private keys in settings are encrypted at rest by Seq
- Credentials are never logged in plain text

### Logging

- The app logs connection success/failure events
- Sensitive data (passwords, private keys) are never included in log events
- Host, port, and username are logged for diagnostic purposes

## Security Updates

Security updates will be released as:

1. **Patch versions** for minor security fixes
2. **Minor versions** for significant security improvements
3. **Security advisories** published on GitHub for all security-related changes

To stay informed about security updates:

1. **Watch** this repository on GitHub
2. **Enable Dependabot alerts** for your forks
3. **Subscribe** to GitHub security advisories

## Dependencies

This project depends on:

- **Seq.Apps**: Seq app framework (maintained by Datalust)
- **SSH.NET**: SSH/SFTP library (maintained by SSH.NET community)

We use Dependabot to monitor for vulnerabilities in dependencies and will update promptly when security patches are available.