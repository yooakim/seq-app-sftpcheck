using System.Diagnostics;
using System.Timers;
using Renci.SshNet;
using Seq.Apps;
using Seq.Apps.LogEvents;
using Timer = System.Timers.Timer;

namespace Seq.App.SftpCheck;

/// <summary>
/// A Seq app that periodically checks SFTP host connectivity and logs the results.
/// </summary>
[SeqApp("SFTP Connectivity Check",
    Description = "Periodically checks SFTP host connectivity and logs results to Seq. " +
                  "Supports both password and private key authentication.")]
public class SftpCheckApp : SeqApp, ISubscribeToAsync<LogEventData>, IDisposable
{
    private Timer? _checkTimer;
    private bool _isChecking;
    private readonly object _checkLock = new();

    #region Settings

    [SeqAppSetting(
        DisplayName = "Host Name",
        HelpText = "The hostname or IP address of the SFTP server to check.")]
    public string SftpHost { get; set; } = null!;

    [SeqAppSetting(
        DisplayName = "Port",
        IsOptional = true,
        HelpText = "The SFTP port number. Default is 22.")]
    public int Port { get; set; } = 22;

    [SeqAppSetting(
        DisplayName = "Username",
        HelpText = "The username for SFTP authentication.")]
    public string Username { get; set; } = null!;

    [SeqAppSetting(
        DisplayName = "Authentication Method",
        IsOptional = true,
        HelpText = "Choose 'Password' or 'PrivateKey'. Default is 'Password'.")]
    public string AuthenticationMethod { get; set; } = "Password";

    [SeqAppSetting(
        DisplayName = "Password",
        InputType = SettingInputType.Password,
        IsOptional = true,
        HelpText = "The password for authentication. Required if Authentication Method is 'Password'.")]
    public string? Password { get; set; }

    [SeqAppSetting(
        DisplayName = "Private Key (Base64)",
        InputType = SettingInputType.LongText,
        IsOptional = true,
        HelpText = "The private key content encoded as Base64. Required if Authentication Method is 'PrivateKey'. " +
                   "To encode: [Convert]::ToBase64String([System.IO.File]::ReadAllBytes('path/to/key'))")]
    public string? PrivateKeyBase64 { get; set; }

    [SeqAppSetting(
        DisplayName = "Private Key Passphrase",
        InputType = SettingInputType.Password,
        IsOptional = true,
        HelpText = "The passphrase for the private key, if it is encrypted.")]
    public string? PrivateKeyPassphrase { get; set; }

    [SeqAppSetting(
        DisplayName = "Check Interval (seconds)",
        IsOptional = true,
        HelpText = "How often to check SFTP connectivity, in seconds. Default is 300 (5 minutes). Minimum is 30.")]
    public int CheckIntervalSeconds { get; set; } = 300;

    [SeqAppSetting(
        DisplayName = "Connection Timeout (seconds)",
        IsOptional = true,
        HelpText = "Timeout for the SFTP connection attempt, in seconds. Default is 30.")]
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    [SeqAppSetting(
        DisplayName = "Test Directory Path",
        IsOptional = true,
        HelpText = "Optional: A directory path on the SFTP server to list as an additional connectivity test. " +
                   "Leave empty to only test connection.")]
    public string? TestDirectoryPath { get; set; }

    [SeqAppSetting(
        DisplayName = "Friendly Name",
        IsOptional = true,
        HelpText = "A friendly name for this SFTP host, used in log messages. If not specified, the hostname will be used.")]
    public string? FriendlyName { get; set; }

    [SeqAppSetting(
        DisplayName = "Log Successful Checks",
        IsOptional = true,
        HelpText = "If true, logs an informational message on successful checks. If false, only failures are logged. Default is true.")]
    public bool LogSuccessfulChecks { get; set; } = true;

    #endregion

    /// <summary>
    /// Gets the display name for this SFTP host (friendly name or hostname).
    /// </summary>
    private string DisplayName => string.IsNullOrWhiteSpace(FriendlyName) ? SftpHost : FriendlyName;

    /// <summary>
    /// Called when the app is attached to Seq. Starts the scheduled checks.
    /// </summary>
    protected override void OnAttached()
    {
        base.OnAttached();

        // Validate settings
        ValidateSettings();

        // Ensure minimum check interval
        var intervalSeconds = Math.Max(30, CheckIntervalSeconds);
        var intervalMs = intervalSeconds * 1000;

        Log.Information(
            "SFTP Check starting for {SftpHost}:{SftpPort} ({DisplayName}). Check interval: {IntervalSeconds}s",
            SftpHost, Port, DisplayName, intervalSeconds);

        // Perform an initial check
        _ = Task.Run(PerformSftpCheckAsync);

        // Set up the timer for periodic checks
        _checkTimer = new Timer(intervalMs);
        _checkTimer.Elapsed += OnTimerElapsed;
        _checkTimer.AutoReset = true;
        _checkTimer.Start();
    }

    /// <summary>
    /// Validates the app settings and throws if invalid.
    /// </summary>
    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(SftpHost))
            throw new SeqAppException("Host is required.");

        if (string.IsNullOrWhiteSpace(Username))
            throw new SeqAppException("Username is required.");

        var authMethod = AuthenticationMethod?.Trim().ToLowerInvariant() ?? "password";

        if (authMethod == "password")
        {
            if (string.IsNullOrWhiteSpace(Password))
                throw new SeqAppException("Password is required when Authentication Method is 'Password'.");
        }
        else if (authMethod == "privatekey")
        {
            if (string.IsNullOrWhiteSpace(PrivateKeyBase64))
                throw new SeqAppException("Private Key (Base64) is required when Authentication Method is 'PrivateKey'.");
        }
        else
        {
            throw new SeqAppException($"Invalid Authentication Method '{AuthenticationMethod}'. Use 'Password' or 'PrivateKey'.");
        }
    }

    /// <summary>
    /// Timer elapsed event handler.
    /// </summary>
    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        _ = Task.Run(PerformSftpCheckAsync);
    }

    /// <summary>
    /// Handles incoming events. Can be used to trigger on-demand checks.
    /// </summary>
    public async Task OnAsync(Event<LogEventData> evt)
    {
        // This allows the app to react to specific events if needed
        // For now, we just use the timer-based approach
        // But you could check for specific properties in the event to trigger a manual check
        await Task.CompletedTask;
    }

    /// <summary>
    /// Performs the SFTP connectivity check.
    /// </summary>
    private async Task PerformSftpCheckAsync()
    {
        // Prevent overlapping checks
        lock (_checkLock)
        {
            if (_isChecking)
            {
                Log.Debug("SFTP check for {DisplayName} skipped - previous check still in progress", DisplayName);
                return;
            }
            _isChecking = true;
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var connectionInfo = CreateConnectionInfo();
            using var client = new SftpClient(connectionInfo);

            // Connect
            await Task.Run(() => client.Connect()).ConfigureAwait(false);

            if (!client.IsConnected)
            {
                throw new InvalidOperationException("SFTP client reports not connected after Connect() call.");
            }

            stopwatch.Stop();
            var connectDurationMs = stopwatch.ElapsedMilliseconds;

            // Optional: Test directory listing
            int? fileCount = null;
            if (!string.IsNullOrWhiteSpace(TestDirectoryPath))
            {
                var listStopwatch = Stopwatch.StartNew();
                var files = await Task.Run(() => client.ListDirectory(TestDirectoryPath)).ConfigureAwait(false);
                listStopwatch.Stop();
                fileCount = files.Count();

                if (LogSuccessfulChecks)
                {
                    Log.Information(
                        "SFTP check succeeded for {DisplayName} ({SftpHost}:{SftpPort}). " +
                        "Connect: {ConnectDurationMs}ms, Listed {FileCount} items in {ListDurationMs}ms",
                        DisplayName, SftpHost, Port, connectDurationMs, fileCount, listStopwatch.ElapsedMilliseconds);
                }
            }
            else
            {
                if (LogSuccessfulChecks)
                {
                    Log.Information(
                        "SFTP check succeeded for {DisplayName} ({SftpHost}:{SftpPort}). Connect: {ConnectDurationMs}ms",
                        DisplayName, SftpHost, Port, connectDurationMs);
                }
            }

            // Disconnect gracefully
            if (client.IsConnected)
            {
                await Task.Run(() => client.Disconnect()).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            Log.Error(ex,
                "SFTP check failed for {DisplayName} ({SftpHost}:{SftpPort}) after {DurationMs}ms: {ErrorMessage}",
                DisplayName, SftpHost, Port, stopwatch.ElapsedMilliseconds, ex.Message);
        }
        finally
        {
            lock (_checkLock)
            {
                _isChecking = false;
            }
        }
    }

    /// <summary>
    /// Creates the SSH.NET ConnectionInfo based on the configured authentication method.
    /// </summary>
    private ConnectionInfo CreateConnectionInfo()
    {
        var timeout = TimeSpan.FromSeconds(ConnectionTimeoutSeconds);
        var authMethod = AuthenticationMethod?.Trim().ToLowerInvariant() ?? "password";

        AuthenticationMethod[] authMethods;

        if (authMethod == "privatekey")
        {
            var keyBytes = Convert.FromBase64String(PrivateKeyBase64!);
            using var keyStream = new MemoryStream(keyBytes);

            PrivateKeyFile privateKeyFile;
            if (!string.IsNullOrEmpty(PrivateKeyPassphrase))
            {
                privateKeyFile = new PrivateKeyFile(keyStream, PrivateKeyPassphrase);
            }
            else
            {
                privateKeyFile = new PrivateKeyFile(keyStream);
            }

            authMethods = [new PrivateKeyAuthenticationMethod(Username, privateKeyFile)];
        }
        else
        {
            authMethods = [new PasswordAuthenticationMethod(Username, Password!)];
        }

        var connectionInfo = new ConnectionInfo(SftpHost, Port, Username, authMethods)
        {
            Timeout = timeout
        };

        return connectionInfo;
    }

    /// <summary>
    /// Disposes resources when the app is detached.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_checkTimer != null)
            {
                _checkTimer.Stop();
                _checkTimer.Elapsed -= OnTimerElapsed;
                _checkTimer.Dispose();
                _checkTimer = null;
            }
        }
    }
}
