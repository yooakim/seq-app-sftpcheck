using System.Reflection;
using Renci.SshNet;
using Xunit;

namespace Seq.App.SftpCheck.Tests;

/// <summary>
/// Integration tests for SSH key authentication.
/// These tests require Docker to be running with the SFTP containers.
/// Run: docker compose up -d
///
/// Two SFTP servers are available:
/// 1. sftp (localhost:2222) - Password auth: testuser/testpass
/// 2. sftp-keyauth (localhost:2223) - Key auth: keyuser with docker/sftp/keys/test_key
/// </summary>
[Trait("Category", "Integration")]
public class SftpKeyAuthIntegrationTests
{
    // Password auth SFTP server settings (from docker-compose.yml)
    private const string PasswordSftpHost = "localhost";
    private const int PasswordSftpPort = 2222;
    private const string PasswordUsername = "testuser";
    private const string PasswordPassword = "testpass";

    // Key auth SFTP server settings (from docker-compose.yml)
    private const string KeyAuthSftpHost = "localhost";
    private const int KeyAuthSftpPort = 2223;
    private const string KeyAuthUsername = "keyuser";

    // Relative path from test output directory to the docker keys folder
    private const string TestKeyRelativePath = "../../../../../docker/sftp/keys/test_key";

    private static string? _cachedPrivateKey;

    /// <summary>
    /// Reads the test private key from the docker/sftp/keys folder.
    /// </summary>
    private static string GetTestPrivateKey()
    {
        if (_cachedPrivateKey != null)
            return _cachedPrivateKey;

        // Get the path relative to the test assembly location
        var assemblyLocation = typeof(SftpKeyAuthIntegrationTests).Assembly.Location;
        var assemblyDirectory = Path.GetDirectoryName(assemblyLocation)!;
        var keyPath = Path.GetFullPath(Path.Combine(assemblyDirectory, TestKeyRelativePath));

        if (!File.Exists(keyPath))
        {
            throw new FileNotFoundException(
                $"Test SSH key not found at '{keyPath}'. " +
                "Please ensure the docker/sftp/keys/test_key file exists. " +
                "Generate it with: ssh-keygen -t ed25519 -f docker/sftp/keys/test_key -N \"\"");
        }

        _cachedPrivateKey = File.ReadAllText(keyPath);
        return _cachedPrivateKey;
    }

    /// <summary>
    /// Gets the test private key as Base64 (as required by the app settings).
    /// </summary>
    private static string GetTestPrivateKeyBase64()
    {
        var keyContent = GetTestPrivateKey();
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(keyContent));
    }

    /// <summary>
    /// Creates a PrivateKeyFile from the test key.
    /// </summary>
    private static PrivateKeyFile CreateTestPrivateKeyFile()
    {
        var keyContent = GetTestPrivateKey();
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(keyContent);
        var keyStream = new MemoryStream(keyBytes);
        return new PrivateKeyFile(keyStream);
    }

    private static bool IsPasswordSftpServerAvailable()
    {
        try
        {
            using var client = new SftpClient(PasswordSftpHost, PasswordSftpPort, PasswordUsername, PasswordPassword);
            client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(5);
            client.Connect();
            var isConnected = client.IsConnected;
            client.Disconnect();
            return isConnected;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsKeyAuthSftpServerAvailable()
    {
        try
        {
            using var privateKeyFile = CreateTestPrivateKeyFile();
            var authMethod = new PrivateKeyAuthenticationMethod(KeyAuthUsername, privateKeyFile);
            var connectionInfo = new ConnectionInfo(KeyAuthSftpHost, KeyAuthSftpPort, KeyAuthUsername, authMethod)
            {
                Timeout = TimeSpan.FromSeconds(5)
            };

            using var client = new SftpClient(connectionInfo);
            client.Connect();
            var isConnected = client.IsConnected;
            client.Disconnect();
            return isConnected;
        }
        catch
        {
            return false;
        }
    }

    #region Password Authentication Tests

    [Fact]
    [Trait("Category", "Integration")]
    public void PasswordAuth_CanConnectToDockerSftpServer()
    {
        // Skip if SFTP server is not running
        if (!IsPasswordSftpServerAvailable())
        {
            return;
        }

        // Arrange
        var authMethod = new PasswordAuthenticationMethod(PasswordUsername, PasswordPassword);
        var connectionInfo = new ConnectionInfo(PasswordSftpHost, PasswordSftpPort, PasswordUsername, authMethod)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        // Act
        using var client = new SftpClient(connectionInfo);
        client.Connect();

        // Assert
        Assert.True(client.IsConnected);

        // Cleanup
        client.Disconnect();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void PasswordAuth_CanListDirectory()
    {
        // Skip if SFTP server is not running
        if (!IsPasswordSftpServerAvailable())
        {
            return;
        }

        // Arrange
        var authMethod = new PasswordAuthenticationMethod(PasswordUsername, PasswordPassword);
        var connectionInfo = new ConnectionInfo(PasswordSftpHost, PasswordSftpPort, PasswordUsername, authMethod)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        // Act
        using var client = new SftpClient(connectionInfo);
        client.Connect();

        // List the current working directory
        var files = client.ListDirectory(".");

        // Assert
        Assert.NotNull(files);
        Assert.NotEmpty(files);

        // Cleanup
        client.Disconnect();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SftpCheckApp_WithPasswordAuth_CanCreateValidConnectionInfo()
    {
        // Arrange
        var app = new SftpCheckApp
        {
            SftpHost = PasswordSftpHost,
            Port = PasswordSftpPort,
            Username = PasswordUsername,
            AuthenticationMethod = "Password",
            Password = PasswordPassword,
            ConnectionTimeoutSeconds = 10
        };

        // Act
        var method = typeof(SftpCheckApp).GetMethod("CreateConnectionInfo",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var connectionInfo = method!.Invoke(app, null) as ConnectionInfo;

        // Assert
        Assert.NotNull(connectionInfo);
        Assert.Equal(PasswordSftpHost, connectionInfo!.Host);
        Assert.Equal(PasswordSftpPort, connectionInfo.Port);
        Assert.Equal(PasswordUsername, connectionInfo.Username);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SftpCheckApp_WithPasswordAuth_CanConnectToDockerServer()
    {
        // Skip if SFTP server is not running
        if (!IsPasswordSftpServerAvailable())
        {
            return;
        }

        // Arrange
        var app = new SftpCheckApp
        {
            SftpHost = PasswordSftpHost,
            Port = PasswordSftpPort,
            Username = PasswordUsername,
            AuthenticationMethod = "Password",
            Password = PasswordPassword,
            ConnectionTimeoutSeconds = 10
        };

        // Act - Use reflection to get ConnectionInfo
        var method = typeof(SftpCheckApp).GetMethod("CreateConnectionInfo",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var connectionInfo = method!.Invoke(app, null) as ConnectionInfo;
        Assert.NotNull(connectionInfo);

        using var client = new SftpClient(connectionInfo!);
        client.Connect();

        // Assert
        Assert.True(client.IsConnected);

        // Cleanup
        client.Disconnect();
    }

    #endregion

    #region SSH Key Authentication Tests

    [Fact]
    [Trait("Category", "Integration")]
    public void KeyAuth_CanConnectToDockerSftpServer()
    {
        // Skip if SFTP server is not running
        if (!IsKeyAuthSftpServerAvailable())
        {
            return;
        }

        // Arrange
        using var privateKeyFile = CreateTestPrivateKeyFile();
        var authMethod = new PrivateKeyAuthenticationMethod(KeyAuthUsername, privateKeyFile);
        var connectionInfo = new ConnectionInfo(KeyAuthSftpHost, KeyAuthSftpPort, KeyAuthUsername, authMethod)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        // Act
        using var client = new SftpClient(connectionInfo);
        client.Connect();

        // Assert
        Assert.True(client.IsConnected);

        // Cleanup
        client.Disconnect();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void KeyAuth_CanListDirectory()
    {
        // Skip if SFTP server is not running
        if (!IsKeyAuthSftpServerAvailable())
        {
            return;
        }

        // Arrange
        using var privateKeyFile = CreateTestPrivateKeyFile();
        var authMethod = new PrivateKeyAuthenticationMethod(KeyAuthUsername, privateKeyFile);
        var connectionInfo = new ConnectionInfo(KeyAuthSftpHost, KeyAuthSftpPort, KeyAuthUsername, authMethod)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        // Act
        using var client = new SftpClient(connectionInfo);
        client.Connect();

        // List the current working directory
        var files = client.ListDirectory(".");

        // Assert
        Assert.NotNull(files);
        Assert.NotEmpty(files);

        // Cleanup
        client.Disconnect();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SftpCheckApp_WithKeyAuth_CanCreateValidConnectionInfo()
    {
        // Arrange
        var app = new SftpCheckApp
        {
            SftpHost = KeyAuthSftpHost,
            Port = KeyAuthSftpPort,
            Username = KeyAuthUsername,
            AuthenticationMethod = "PrivateKey",
            PrivateKeyBase64 = GetTestPrivateKeyBase64(),
            ConnectionTimeoutSeconds = 10
        };

        // Act
        var method = typeof(SftpCheckApp).GetMethod("CreateConnectionInfo",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var connectionInfo = method!.Invoke(app, null) as ConnectionInfo;

        // Assert
        Assert.NotNull(connectionInfo);
        Assert.Equal(KeyAuthSftpHost, connectionInfo!.Host);
        Assert.Equal(KeyAuthSftpPort, connectionInfo.Port);
        Assert.Equal(KeyAuthUsername, connectionInfo.Username);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SftpCheckApp_WithKeyAuth_CanConnectToDockerServer()
    {
        // Skip if SFTP server is not running
        if (!IsKeyAuthSftpServerAvailable())
        {
            return;
        }

        // Arrange
        var app = new SftpCheckApp
        {
            SftpHost = KeyAuthSftpHost,
            Port = KeyAuthSftpPort,
            Username = KeyAuthUsername,
            AuthenticationMethod = "PrivateKey",
            PrivateKeyBase64 = GetTestPrivateKeyBase64(),
            ConnectionTimeoutSeconds = 10
        };

        // Act - Use reflection to get ConnectionInfo
        var method = typeof(SftpCheckApp).GetMethod("CreateConnectionInfo",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var connectionInfo = method!.Invoke(app, null) as ConnectionInfo;
        Assert.NotNull(connectionInfo);

        using var client = new SftpClient(connectionInfo!);
        client.Connect();

        // Assert
        Assert.True(client.IsConnected);

        // Cleanup
        client.Disconnect();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SftpCheckApp_WithKeyAuth_CaseInsensitiveAuthMethod()
    {
        // Skip if SFTP server is not running
        if (!IsKeyAuthSftpServerAvailable())
        {
            return;
        }

        // Arrange - use lowercase "privatekey"
        var app = new SftpCheckApp
        {
            SftpHost = KeyAuthSftpHost,
            Port = KeyAuthSftpPort,
            Username = KeyAuthUsername,
            AuthenticationMethod = "privatekey", // lowercase
            PrivateKeyBase64 = GetTestPrivateKeyBase64(),
            ConnectionTimeoutSeconds = 10
        };

        // Act - Use reflection to get ConnectionInfo
        var method = typeof(SftpCheckApp).GetMethod("CreateConnectionInfo",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var connectionInfo = method!.Invoke(app, null) as ConnectionInfo;
        Assert.NotNull(connectionInfo);

        using var client = new SftpClient(connectionInfo!);
        client.Connect();

        // Assert
        Assert.True(client.IsConnected);

        // Cleanup
        client.Disconnect();
    }

    #endregion

    #region Validation Tests

    [Fact]
    [Trait("Category", "Integration")]
    public void ValidateSettings_RequiresPrivateKeyForPrivateKeyAuth()
    {
        // Arrange
        var app = new SftpCheckApp
        {
            SftpHost = KeyAuthSftpHost,
            Port = KeyAuthSftpPort,
            Username = KeyAuthUsername,
            AuthenticationMethod = "PrivateKey",
            PrivateKeyBase64 = null // Missing key
        };

        // Act
        var method = typeof(SftpCheckApp).GetMethod("ValidateSettings",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Assert - should throw because PrivateKeyBase64 is required
        var exception = Assert.ThrowsAny<TargetInvocationException>(() => method!.Invoke(app, null));
        Assert.Contains("Private Key", exception.InnerException?.Message ?? "");
    }

    [Theory]
    [InlineData("PrivateKey")]
    [InlineData("privatekey")]
    [InlineData("PRIVATEKEY")]
    [Trait("Category", "Integration")]
    public void ValidateSettings_AcceptsPrivateKeyAuthMethodCaseInsensitive(string authMethod)
    {
        // Arrange
        var app = new SftpCheckApp
        {
            SftpHost = KeyAuthSftpHost,
            Port = KeyAuthSftpPort,
            Username = KeyAuthUsername,
            AuthenticationMethod = authMethod,
            PrivateKeyBase64 = GetTestPrivateKeyBase64()
        };

        // Act
        var method = typeof(SftpCheckApp).GetMethod("ValidateSettings",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Assert - should not throw
        var exception = Record.Exception(() => method!.Invoke(app, null));
        Assert.Null(exception);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void CreateConnectionInfo_WithPassphrase_HandlesUnencryptedKey()
    {
        // Arrange - Key is not encrypted, so passphrase should be ignored
        var app = new SftpCheckApp
        {
            SftpHost = KeyAuthSftpHost,
            Port = KeyAuthSftpPort,
            Username = KeyAuthUsername,
            AuthenticationMethod = "PrivateKey",
            PrivateKeyBase64 = GetTestPrivateKeyBase64(),
            PrivateKeyPassphrase = "unused-passphrase", // Will be ignored for unencrypted key
            ConnectionTimeoutSeconds = 10
        };

        // Act
        var method = typeof(SftpCheckApp).GetMethod("CreateConnectionInfo",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var connectionInfo = method!.Invoke(app, null) as ConnectionInfo;

        // Assert - Should work even with passphrase set on unencrypted key
        Assert.NotNull(connectionInfo);
    }

    #endregion

    #region Full Flow Tests

    /// <summary>
    /// This test demonstrates the full flow of creating a ConnectionInfo with private key auth
    /// and using it to create an SftpClient, exactly as the app does.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void FullFlow_KeyAuth_CreateConnectionInfoAndConnect()
    {
        // Skip if SFTP server is not running
        if (!IsKeyAuthSftpServerAvailable())
        {
            return;
        }

        // Arrange - Similar to SftpCheckApp.CreateConnectionInfo()
        var base64Key = GetTestPrivateKeyBase64();
        var keyBytes = Convert.FromBase64String(base64Key);
        using var keyStream = new MemoryStream(keyBytes);
        var privateKeyFile = new PrivateKeyFile(keyStream);
        var authMethod = new PrivateKeyAuthenticationMethod(KeyAuthUsername, privateKeyFile);
        var connectionInfo = new ConnectionInfo(KeyAuthSftpHost, KeyAuthSftpPort, KeyAuthUsername, authMethod)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        using var client = new SftpClient(connectionInfo);
        client.Connect();

        // Assert
        Assert.True(client.IsConnected);
        Assert.Equal(KeyAuthSftpHost, connectionInfo.Host);
        Assert.Equal(KeyAuthSftpPort, connectionInfo.Port);
        Assert.Equal(KeyAuthUsername, connectionInfo.Username);

        // Cleanup
        client.Disconnect();
    }

    /// <summary>
    /// Test that verifies both authentication methods work side by side.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void BothAuthMethods_CanConnectSimultaneously()
    {
        // Skip if either SFTP server is not running
        if (!IsPasswordSftpServerAvailable() || !IsKeyAuthSftpServerAvailable())
        {
            return;
        }

        // Arrange - Password auth
        var passwordAuthMethod = new PasswordAuthenticationMethod(PasswordUsername, PasswordPassword);
        var passwordConnectionInfo = new ConnectionInfo(PasswordSftpHost, PasswordSftpPort, PasswordUsername, passwordAuthMethod)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        // Arrange - Key auth
        using var privateKeyFile = CreateTestPrivateKeyFile();
        var keyAuthMethod = new PrivateKeyAuthenticationMethod(KeyAuthUsername, privateKeyFile);
        var keyConnectionInfo = new ConnectionInfo(KeyAuthSftpHost, KeyAuthSftpPort, KeyAuthUsername, keyAuthMethod)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        // Act & Assert - Both should connect successfully
        using var passwordClient = new SftpClient(passwordConnectionInfo);
        using var keyClient = new SftpClient(keyConnectionInfo);

        passwordClient.Connect();
        keyClient.Connect();

        Assert.True(passwordClient.IsConnected, "Password auth client should be connected");
        Assert.True(keyClient.IsConnected, "Key auth client should be connected");

        // Cleanup
        passwordClient.Disconnect();
        keyClient.Disconnect();
    }

    #endregion
}
