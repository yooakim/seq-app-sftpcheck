using System.Reflection;
using Moq;
using Renci.SshNet;
using Seq.Apps;
using Xunit;

namespace Seq.App.SftpCheck.Tests;

/// <summary>
/// Helper class for loading test SSH keys from the docker folder.
/// </summary>
internal static class TestKeyHelper
{
    // Relative path from test output directory to the docker keys folder
    private const string TestKeyRelativePath = "../../../../../docker/sftp/keys/test_key";

    private static string? _cachedEd25519Key;
    private static string? _cachedRsaKey;

    // Fallback ED25519 key for CI environments where docker folder may not exist
    private const string FallbackEd25519Key = @"-----BEGIN OPENSSH PRIVATE KEY-----
b3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAAAMwAAAAtzc2gtZW
QyNTUxOQAAACAFcKazrSInxA4uUmoL+GMRH250CyyvG/mswvGJT5vEKwAAAJgJRbiCCUW4
ggAAAAtzc2gtZWQyNTUxOQAAACAFcKazrSInxA4uUmoL+GMRH250CyyvG/mswvGJT5vEKw
AAAEBp8URYlrAmw+WsjJAFPczbFcxq1qaXmCcGXzlvSgJXVAVwprOtIifEDi5Sagv4YxEf
bnQLLK8b+azC8YlPm8QrAAAADnRlc3RAc2Z0cGNoZWNrAQIDBAUGBw==
-----END OPENSSH PRIVATE KEY-----";

    // Fallback RSA key for CI environments (OpenSSH format)
    private const string FallbackRsaKey = @"-----BEGIN OPENSSH PRIVATE KEY-----
b3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAABFwAAAAdzc2gtcn
NhAAAAAwEAAQAAAQEAmNgNuJcibNawtYtnT22G0u7PPbwPrCF+GeH8f50YWkuZaILADos8
NvIzT/xxnynKYwHLXr6vk8/D2+PsWVGqovjItY9dl1GEGTp3N9qSvl7JJPL8N+z7IVZNU6
OHq7+uKlaPMUMOXldsbmLerLMrioOtJX3v4oo8IeKA9iEMkotpzbom8KgdpRP/QGNxDnkF
b+mBulRt2nyOvv5Zp1rQCbluNiHnE1Zf6PpnDF+LZginqt7emkJxHspA1JRS0S5ah7KwJ+
ELFRJ9A8/NszBi/7Otf2onW+X6T1BDfU/kq3PItiRIUiYGUWXS+oPuQr+y1j9Nm7odf3zS
2KiBh1wAuwAAA8g02olHNNqJRwAAAAdzc2gtcnNhAAABAQCY2A24lyJs1rC1i2dPbYbS7s
89vA+sIX4Z4fx/nRhaS5logsAOizw28jNP/HGfKcpjActevq+Tz8Pb4+xZUaqi+Mi1j12X
UYQZOnc32pK+Xskk8vw37PshVk1To4erv64qVo8xQw5eV2xuYt6ssyuKg60lfe/iijwh4o
D2IQySi2nNuibwqB2lE/9AY3EOeQVv6YG6VG3afI6+/lmnWtAJuW42IecTVl/o+mcMX4tm
CKeq3t6aQnEeykDUlFLRLlqHsrAn4QsVEn0Dz82zMGL/s61/aidb5fpPUEN9T+Src8i2JE
hSJgZRZdL6g+5Cv7LWP02buh1/fNLYqIGHXAC7AAAAAwEAAQAAAQBDZX3DaC1fZdnc68Ad
55N8fp52v+7/PXOP8TT4hqqe4lgenA0ZPK9MIUecHRpzDyf1uWxWdmoQmRxp4VquVhMSsv
Y6DSI9X84Km9vHDsQHWt+CQf0SohZosFf/qgvgoXCorauNkt6Kni0rjcBX0dfAx+h5MEuv
jroOTQUFwzP6h/2frocW6Xc3GqgF4uRfGxJJMo/bcIJv+uTkZerUlUOTXI7u5rW0x8Y+hG
5xNnadUWE37e7+pefTF2kZZ1/+U2FDw2DPLK8Z+G+tJtG1IuhGM4KWz34pfWo/2kScQEyc
duHakDk+INFHZpy5KlpMMPlqz6ScYakJgessX6gYR5u5AAAAgCnLZccwOzxQokbA/nXg6n
5dyPxQ41bfwvEa+rOK7qLcReLSPHVXdNdCBVNLgo3jOq4qCoYkYHq+D6SYoY6/EuFxTEoB
/QL9vDql8R3ive7jMvxpaaLHUi6Oa2/VOpeBG1WkuYY99VQ1jeR1Ywd7C04nFfjEWV5IYS
6HMYczo++6AAAAgQDJrnRzS8EdYyFcD7rw0IZGzkleQ2K6JX/nm8pOtPjduBSCHeVUjEYr
hsti8HW8izvTTNWVXWKfH3bg2SvWMwSc2D8nRSCvt8akstMMneAAy1G5MBUwuyYVIJXAbi
bjESNBlc7ceB8D1U5nvKj+ooEyEq8YWj0Bt6BOGOBrZg9lGQAAAIEAwgJY/kvI0HFXp8B6
apCl6kDLKy0PbHP3Yzh07OzPSWgYWx6oZxJlVL7tV97DZzPGno64xD9Z5Xf0mX+EPLt5Ct
H9XEcgzom9TxdxI8AqCRqcR/n4Q02555t/pZ3NUQ6+OtUI84NxZvc+nNaiXYr/tBaLrpZY
4Kp0qKjG/0MemvMAAAAQdGVzdEBleGFtcGxlLmNvbQECAw==
-----END OPENSSH PRIVATE KEY-----";

    /// <summary>
    /// Gets the ED25519 test private key, reading from docker folder if available.
    /// </summary>
    public static string GetEd25519PrivateKey()
    {
        if (_cachedEd25519Key != null)
            return _cachedEd25519Key;

        var keyPath = GetTestKeyPath();
        if (keyPath != null && File.Exists(keyPath))
        {
            _cachedEd25519Key = File.ReadAllText(keyPath);
        }
        else
        {
            // Use fallback for CI or when docker folder doesn't exist
            _cachedEd25519Key = FallbackEd25519Key;
        }

        return _cachedEd25519Key;
    }

    /// <summary>
    /// Gets the RSA test private key (fallback only, no file version).
    /// </summary>
    public static string GetRsaPrivateKey()
    {
        if (_cachedRsaKey != null)
            return _cachedRsaKey;

        _cachedRsaKey = FallbackRsaKey;
        return _cachedRsaKey;
    }

    /// <summary>
    /// Converts a private key to Base64 encoding.
    /// </summary>
    public static string ToBase64(string privateKey)
    {
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(privateKey));
    }

    private static string? GetTestKeyPath()
    {
        var assemblyLocation = typeof(TestKeyHelper).Assembly.Location;
        var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
        if (assemblyDirectory == null)
            return null;

        return Path.GetFullPath(Path.Combine(assemblyDirectory, TestKeyRelativePath));
    }
}

public class SftpCheckAppTests
{
    #region Attribute and Structure Tests

    [Fact]
    public void App_HasCorrectSeqAppAttribute()
    {
        // Arrange & Act
        var attribute = typeof(SftpCheckApp)
            .GetCustomAttributes(typeof(SeqAppAttribute), false)
            .FirstOrDefault() as SeqAppAttribute;

        // Assert
        Assert.NotNull(attribute);
        Assert.Equal("SFTP Connectivity Check", attribute.Name);
        Assert.False(string.IsNullOrWhiteSpace(attribute.Description));
    }

    [Fact]
    public void App_ImplementsIDisposable()
    {
        // Assert
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(SftpCheckApp)));
    }

    [Fact]
    public void App_InheritsFromSeqApp()
    {
        // Assert
        Assert.True(typeof(SeqApp).IsAssignableFrom(typeof(SftpCheckApp)));
    }

    [Fact]
    public void App_HasRequiredSettings()
    {
        // Arrange
        var appType = typeof(SftpCheckApp);

        // Act & Assert - Check that required settings exist
        var hostProperty = appType.GetProperty("SftpHost");
        Assert.NotNull(hostProperty);
        var hostAttribute = hostProperty!.GetCustomAttributes(typeof(SeqAppSettingAttribute), false).FirstOrDefault() as SeqAppSettingAttribute;
        Assert.NotNull(hostAttribute);
        Assert.False(hostAttribute!.IsOptional);

        var usernameProperty = appType.GetProperty("Username");
        Assert.NotNull(usernameProperty);
        var usernameAttribute = usernameProperty!.GetCustomAttributes(typeof(SeqAppSettingAttribute), false).FirstOrDefault() as SeqAppSettingAttribute;
        Assert.NotNull(usernameAttribute);
        Assert.False(usernameAttribute!.IsOptional);
    }

    [Fact]
    public void App_HasOptionalSettings()
    {
        // Arrange
        var appType = typeof(SftpCheckApp);
        var optionalSettings = new[] { "Port", "Password", "PrivateKeyBase64", "PrivateKeyPassphrase",
            "CheckIntervalSeconds", "ConnectionTimeoutSeconds", "TestDirectoryPath", "FriendlyName", "LogSuccessfulChecks" };

        // Act & Assert
        foreach (var settingName in optionalSettings)
        {
            var property = appType.GetProperty(settingName);
            Assert.NotNull(property);
            var attribute = property!.GetCustomAttributes(typeof(SeqAppSettingAttribute), false).FirstOrDefault() as SeqAppSettingAttribute;
            Assert.NotNull(attribute);
            Assert.True(attribute!.IsOptional, $"Setting '{settingName}' should be optional");
        }
    }

    [Fact]
    public void App_PasswordSettingIsPassword()
    {
        // Arrange
        var passwordProperty = typeof(SftpCheckApp).GetProperty("Password");
        var attribute = passwordProperty!.GetCustomAttributes(typeof(SeqAppSettingAttribute), false).FirstOrDefault() as SeqAppSettingAttribute;

        // Assert
        Assert.NotNull(attribute);
        Assert.Equal(SettingInputType.Password, attribute!.InputType);
    }

    [Fact]
    public void App_PrivateKeyPassphraseSettingIsPassword()
    {
        // Arrange
        var property = typeof(SftpCheckApp).GetProperty("PrivateKeyPassphrase");
        var attribute = property!.GetCustomAttributes(typeof(SeqAppSettingAttribute), false).FirstOrDefault() as SeqAppSettingAttribute;

        // Assert
        Assert.NotNull(attribute);
        Assert.Equal(SettingInputType.Password, attribute!.InputType);
    }

    [Fact]
    public void App_PrivateKeyBase64SettingIsLongText()
    {
        // Arrange
        var property = typeof(SftpCheckApp).GetProperty("PrivateKeyBase64");
        var attribute = property!.GetCustomAttributes(typeof(SeqAppSettingAttribute), false).FirstOrDefault() as SeqAppSettingAttribute;

        // Assert
        Assert.NotNull(attribute);
        Assert.Equal(SettingInputType.LongText, attribute!.InputType);
    }

    #endregion

    #region Default Value Tests

    [Fact]
    public void App_DefaultPortIs22()
    {
        // Arrange
        var app = new SftpCheckApp();

        // Assert
        Assert.Equal(22, app.Port);
    }

    [Fact]
    public void App_DefaultCheckIntervalIs300Seconds()
    {
        // Arrange
        var app = new SftpCheckApp();

        // Assert
        Assert.Equal(300, app.CheckIntervalSeconds);
    }

    [Fact]
    public void App_DefaultConnectionTimeoutIs30Seconds()
    {
        // Arrange
        var app = new SftpCheckApp();

        // Assert
        Assert.Equal(30, app.ConnectionTimeoutSeconds);
    }

    [Fact]
    public void App_DefaultAuthenticationMethodIsPassword()
    {
        // Arrange
        var app = new SftpCheckApp();

        // Assert
        Assert.Equal("Password", app.AuthenticationMethod);
    }

    [Fact]
    public void App_DefaultLogSuccessfulChecksIsTrue()
    {
        // Arrange
        var app = new SftpCheckApp();

        // Assert
        Assert.True(app.LogSuccessfulChecks);
    }

    #endregion

    #region Instantiation and Disposal Tests

    [Fact]
    public void App_CanBeInstantiated()
    {
        // Act
        var app = new SftpCheckApp();

        // Assert
        Assert.NotNull(app);
    }

    [Fact]
    public void App_CanBeDisposed()
    {
        // Arrange
        var app = new SftpCheckApp();

        // Act & Assert - should not throw
        app.Dispose();
    }

    [Fact]
    public void App_CanBeDisposedMultipleTimes()
    {
        // Arrange
        var app = new SftpCheckApp();

        // Act & Assert - should not throw
        app.Dispose();
        app.Dispose();
    }

    #endregion

    #region SSH Key Authentication Tests

    // Use TestKeyHelper to get keys from docker folder or fallback
    private static string TestEd25519PrivateKeyPem => TestKeyHelper.GetEd25519PrivateKey();
    private static string TestRsaPrivateKeyPem => TestKeyHelper.GetRsaPrivateKey();

    private static string ToBase64(string pem) => TestKeyHelper.ToBase64(pem);

    [Fact]
    public void App_AuthenticationMethodSetting_AcceptsPrivateKey()
    {
        // Arrange
        var app = new SftpCheckApp();

        // Act
        app.AuthenticationMethod = "PrivateKey";

        // Assert
        Assert.Equal("PrivateKey", app.AuthenticationMethod);
    }

    [Fact]
    public void App_AuthenticationMethodSetting_AcceptsPassword()
    {
        // Arrange
        var app = new SftpCheckApp();

        // Act
        app.AuthenticationMethod = "Password";

        // Assert
        Assert.Equal("Password", app.AuthenticationMethod);
    }

    [Fact]
    public void App_PrivateKeyBase64_CanBeSet()
    {
        // Arrange
        var app = new SftpCheckApp();
        var base64Key = ToBase64(TestEd25519PrivateKeyPem);

        // Act
        app.PrivateKeyBase64 = base64Key;

        // Assert
        Assert.Equal(base64Key, app.PrivateKeyBase64);
    }

    [Fact]
    public void App_PrivateKeyPassphrase_CanBeSet()
    {
        // Arrange
        var app = new SftpCheckApp();
        var passphrase = "test-passphrase";

        // Act
        app.PrivateKeyPassphrase = passphrase;

        // Assert
        Assert.Equal(passphrase, app.PrivateKeyPassphrase);
    }

    [Fact]
    public void PrivateKeyFile_CanBeCreatedFromEd25519Key()
    {
        // Arrange
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(TestEd25519PrivateKeyPem);

        // Act
        using var keyStream = new MemoryStream(keyBytes);
        var privateKeyFile = new PrivateKeyFile(keyStream);

        // Assert
        Assert.NotNull(privateKeyFile);
    }

    [Fact]
    public void PrivateKeyFile_CanBeCreatedFromRsaKey()
    {
        // Arrange
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(TestRsaPrivateKeyPem);

        // Act
        using var keyStream = new MemoryStream(keyBytes);
        var privateKeyFile = new PrivateKeyFile(keyStream);

        // Assert
        Assert.NotNull(privateKeyFile);
    }

    [Fact]
    public void PrivateKeyFile_CanBeCreatedFromBase64EncodedKey()
    {
        // Arrange
        var base64Key = ToBase64(TestEd25519PrivateKeyPem);
        var keyBytes = Convert.FromBase64String(base64Key);

        // Act
        using var keyStream = new MemoryStream(keyBytes);
        var privateKeyFile = new PrivateKeyFile(keyStream);

        // Assert
        Assert.NotNull(privateKeyFile);
    }

    [Fact]
    public void PrivateKeyAuthenticationMethod_CanBeCreatedWithPrivateKeyFile()
    {
        // Arrange
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(TestEd25519PrivateKeyPem);
        using var keyStream = new MemoryStream(keyBytes);
        var privateKeyFile = new PrivateKeyFile(keyStream);
        var username = "testuser";

        // Act
        var authMethod = new PrivateKeyAuthenticationMethod(username, privateKeyFile);

        // Assert
        Assert.NotNull(authMethod);
        Assert.Equal(username, authMethod.Username);
    }

    [Fact]
    public void ConnectionInfo_CanBeCreatedWithPrivateKeyAuthentication()
    {
        // Arrange
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(TestEd25519PrivateKeyPem);
        using var keyStream = new MemoryStream(keyBytes);
        var privateKeyFile = new PrivateKeyFile(keyStream);
        var authMethod = new PrivateKeyAuthenticationMethod("testuser", privateKeyFile);

        // Act
        var connectionInfo = new ConnectionInfo("localhost", 22, "testuser", authMethod);

        // Assert
        Assert.NotNull(connectionInfo);
        Assert.Equal("localhost", connectionInfo.Host);
        Assert.Equal(22, connectionInfo.Port);
        Assert.Equal("testuser", connectionInfo.Username);
    }

    [Fact]
    public void ConnectionInfo_CanBeCreatedWithPasswordAuthentication()
    {
        // Arrange
        var authMethod = new PasswordAuthenticationMethod("testuser", "testpassword");

        // Act
        var connectionInfo = new ConnectionInfo("localhost", 22, "testuser", authMethod);

        // Assert
        Assert.NotNull(connectionInfo);
        Assert.Equal("localhost", connectionInfo.Host);
        Assert.Equal(22, connectionInfo.Port);
        Assert.Equal("testuser", connectionInfo.Username);
    }

    [Fact]
    public void PrivateKeyFile_ThrowsOnInvalidKeyFormat()
    {
        // Arrange
        var invalidKey = "not-a-valid-private-key";
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(invalidKey);

        // Act & Assert
        using var keyStream = new MemoryStream(keyBytes);
        Assert.ThrowsAny<Exception>(() => new PrivateKeyFile(keyStream));
    }

    [Fact]
    public void Base64Decode_WorksWithValidPrivateKey()
    {
        // Arrange
        var base64Key = ToBase64(TestRsaPrivateKeyPem);

        // Act
        var decodedBytes = Convert.FromBase64String(base64Key);
        var decodedString = System.Text.Encoding.UTF8.GetString(decodedBytes);

        // Assert
        Assert.Equal(TestRsaPrivateKeyPem, decodedString);
    }

    [Fact]
    public void Base64Decode_ThrowsOnInvalidBase64()
    {
        // Arrange
        var invalidBase64 = "not-valid-base64!!!";

        // Act & Assert
        Assert.ThrowsAny<FormatException>(() => Convert.FromBase64String(invalidBase64));
    }

    #endregion

    #region CreateConnectionInfo Tests via Reflection

    [Fact]
    public void CreateConnectionInfo_WithPasswordAuth_ReturnsValidConnectionInfo()
    {
        // Arrange
        var app = new SftpCheckApp
        {
            SftpHost = "test.example.com",
            Port = 2222,
            Username = "testuser",
            AuthenticationMethod = "Password",
            Password = "testpassword",
            ConnectionTimeoutSeconds = 60
        };

        // Act
        var method = typeof(SftpCheckApp).GetMethod("CreateConnectionInfo",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var connectionInfo = method!.Invoke(app, null) as ConnectionInfo;

        // Assert
        Assert.NotNull(connectionInfo);
        Assert.Equal("test.example.com", connectionInfo!.Host);
        Assert.Equal(2222, connectionInfo.Port);
        Assert.Equal("testuser", connectionInfo.Username);
        Assert.Equal(TimeSpan.FromSeconds(60), connectionInfo.Timeout);
    }

    [Fact]
    public void CreateConnectionInfo_WithPrivateKeyAuth_ReturnsValidConnectionInfo()
    {
        // Arrange
        var app = new SftpCheckApp
        {
            SftpHost = "test.example.com",
            Port = 22,
            Username = "keyuser",
            AuthenticationMethod = "PrivateKey",
            PrivateKeyBase64 = ToBase64(TestEd25519PrivateKeyPem),
            ConnectionTimeoutSeconds = 30
        };

        // Act
        var method = typeof(SftpCheckApp).GetMethod("CreateConnectionInfo",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var connectionInfo = method!.Invoke(app, null) as ConnectionInfo;

        // Assert
        Assert.NotNull(connectionInfo);
        Assert.Equal("test.example.com", connectionInfo!.Host);
        Assert.Equal(22, connectionInfo.Port);
        Assert.Equal("keyuser", connectionInfo.Username);
    }

    [Fact]
    public void CreateConnectionInfo_WithPrivateKeyAuth_CaseInsensitive()
    {
        // Arrange - Use lowercase "privatekey"
        var app = new SftpCheckApp
        {
            SftpHost = "test.example.com",
            Port = 22,
            Username = "keyuser",
            AuthenticationMethod = "privatekey",
            PrivateKeyBase64 = ToBase64(TestRsaPrivateKeyPem),
            ConnectionTimeoutSeconds = 30
        };

        // Act
        var method = typeof(SftpCheckApp).GetMethod("CreateConnectionInfo",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var connectionInfo = method!.Invoke(app, null) as ConnectionInfo;

        // Assert
        Assert.NotNull(connectionInfo);
        Assert.Equal("keyuser", connectionInfo!.Username);
    }

    [Fact]
    public void CreateConnectionInfo_WithRsaKey_ReturnsValidConnectionInfo()
    {
        // Arrange
        var app = new SftpCheckApp
        {
            SftpHost = "rsa.example.com",
            Port = 22,
            Username = "rsauser",
            AuthenticationMethod = "PrivateKey",
            PrivateKeyBase64 = ToBase64(TestRsaPrivateKeyPem),
            ConnectionTimeoutSeconds = 30
        };

        // Act
        var method = typeof(SftpCheckApp).GetMethod("CreateConnectionInfo",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var connectionInfo = method!.Invoke(app, null) as ConnectionInfo;

        // Assert
        Assert.NotNull(connectionInfo);
        Assert.Equal("rsa.example.com", connectionInfo!.Host);
        Assert.Equal("rsauser", connectionInfo.Username);
    }

    #endregion

    #region ValidateSettings Tests via Reflection

    [Fact]
    public void ValidateSettings_ThrowsWhenHostIsEmpty()
    {
        // Arrange
        var app = new SftpCheckApp
        {
            SftpHost = "",
            Username = "testuser",
            Password = "testpassword"
        };

        // Act
        var method = typeof(SftpCheckApp).GetMethod("ValidateSettings",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Assert
        var exception = Assert.ThrowsAny<TargetInvocationException>(() => method!.Invoke(app, null));
        Assert.IsType<SeqAppException>(exception.InnerException);
        Assert.Contains("Host", exception.InnerException!.Message);
    }

    [Fact]
    public void ValidateSettings_ThrowsWhenUsernameIsEmpty()
    {
        // Arrange
        var app = new SftpCheckApp
        {
            SftpHost = "test.example.com",
            Username = "",
            Password = "testpassword"
        };

        // Act
        var method = typeof(SftpCheckApp).GetMethod("ValidateSettings",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Assert
        var exception = Assert.ThrowsAny<TargetInvocationException>(() => method!.Invoke(app, null));
        Assert.IsType<SeqAppException>(exception.InnerException);
        Assert.Contains("Username", exception.InnerException!.Message);
    }

    [Fact]
    public void ValidateSettings_ThrowsWhenPasswordAuthWithoutPassword()
    {
        // Arrange
        var app = new SftpCheckApp
        {
            SftpHost = "test.example.com",
            Username = "testuser",
            AuthenticationMethod = "Password",
            Password = null
        };

        // Act
        var method = typeof(SftpCheckApp).GetMethod("ValidateSettings",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Assert
        var exception = Assert.ThrowsAny<TargetInvocationException>(() => method!.Invoke(app, null));
        Assert.IsType<SeqAppException>(exception.InnerException);
        Assert.Contains("Password", exception.InnerException!.Message);
    }

    [Fact]
    public void ValidateSettings_ThrowsWhenPrivateKeyAuthWithoutKey()
    {
        // Arrange
        var app = new SftpCheckApp
        {
            SftpHost = "test.example.com",
            Username = "testuser",
            AuthenticationMethod = "PrivateKey",
            PrivateKeyBase64 = null
        };

        // Act
        var method = typeof(SftpCheckApp).GetMethod("ValidateSettings",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Assert
        var exception = Assert.ThrowsAny<TargetInvocationException>(() => method!.Invoke(app, null));
        Assert.IsType<SeqAppException>(exception.InnerException);
        Assert.Contains("Private Key", exception.InnerException!.Message);
    }

    [Fact]
    public void ValidateSettings_ThrowsOnInvalidAuthenticationMethod()
    {
        // Arrange
        var app = new SftpCheckApp
        {
            SftpHost = "test.example.com",
            Username = "testuser",
            AuthenticationMethod = "InvalidMethod"
        };

        // Act
        var method = typeof(SftpCheckApp).GetMethod("ValidateSettings",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Assert
        var exception = Assert.ThrowsAny<TargetInvocationException>(() => method!.Invoke(app, null));
        Assert.IsType<SeqAppException>(exception.InnerException);
        Assert.Contains("Authentication Method", exception.InnerException!.Message);
    }

    [Fact]
    public void ValidateSettings_SucceedsWithValidPasswordConfig()
    {
        // Arrange
        var app = new SftpCheckApp
        {
            SftpHost = "test.example.com",
            Username = "testuser",
            AuthenticationMethod = "Password",
            Password = "testpassword"
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
    public void ValidateSettings_SucceedsWithValidPrivateKeyConfig()
    {
        // Arrange
        var app = new SftpCheckApp
        {
            SftpHost = "test.example.com",
            Username = "testuser",
            AuthenticationMethod = "PrivateKey",
            PrivateKeyBase64 = ToBase64(TestEd25519PrivateKeyPem)
        };

        // Act
        var method = typeof(SftpCheckApp).GetMethod("ValidateSettings",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Assert - should not throw
        var exception = Record.Exception(() => method!.Invoke(app, null));
        Assert.Null(exception);
    }

    #endregion

    #region Multiple Key Types Tests

    [Theory]
    [InlineData("Password")]
    [InlineData("password")]
    [InlineData("PASSWORD")]
    [InlineData("PrivateKey")]
    [InlineData("privatekey")]
    [InlineData("PRIVATEKEY")]
    public void ValidateSettings_AcceptsAuthMethodCaseInsensitive(string authMethod)
    {
        // Arrange
        var app = new SftpCheckApp
        {
            SftpHost = "test.example.com",
            Username = "testuser",
            AuthenticationMethod = authMethod
        };

        if (authMethod.Equals("password", StringComparison.OrdinalIgnoreCase))
        {
            app.Password = "testpassword";
        }
        else
        {
            app.PrivateKeyBase64 = ToBase64(TestEd25519PrivateKeyPem);
        }

        // Act
        var method = typeof(SftpCheckApp).GetMethod("ValidateSettings",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Assert - should not throw
        var exception = Record.Exception(() => method!.Invoke(app, null));
        Assert.Null(exception);
    }

    [Fact]
    public void SftpClient_CanBeInstantiatedWithPasswordConnectionInfo()
    {
        // Arrange
        var authMethod = new PasswordAuthenticationMethod("testuser", "testpassword");
        var connectionInfo = new ConnectionInfo("localhost", 22, "testuser", authMethod);

        // Act
        using var client = new SftpClient(connectionInfo);

        // Assert
        Assert.NotNull(client);
        Assert.False(client.IsConnected);
    }

    [Fact]
    public void SftpClient_CanBeInstantiatedWithPrivateKeyConnectionInfo()
    {
        // Arrange
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(TestEd25519PrivateKeyPem);
        using var keyStream = new MemoryStream(keyBytes);
        var privateKeyFile = new PrivateKeyFile(keyStream);
        var authMethod = new PrivateKeyAuthenticationMethod("testuser", privateKeyFile);
        var connectionInfo = new ConnectionInfo("localhost", 22, "testuser", authMethod);

        // Act
        using var client = new SftpClient(connectionInfo);

        // Assert
        Assert.NotNull(client);
        Assert.False(client.IsConnected);
    }

    [Fact]
    public void SftpClient_CanBeInstantiatedWithRsaKeyConnectionInfo()
    {
        // Arrange
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(TestRsaPrivateKeyPem);
        using var keyStream = new MemoryStream(keyBytes);
        var privateKeyFile = new PrivateKeyFile(keyStream);
        var authMethod = new PrivateKeyAuthenticationMethod("testuser", privateKeyFile);
        var connectionInfo = new ConnectionInfo("localhost", 22, "testuser", authMethod);

        // Act
        using var client = new SftpClient(connectionInfo);

        // Assert
        Assert.NotNull(client);
        Assert.False(client.IsConnected);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void App_PrivateKeyBase64_CanBeNull()
    {
        // Arrange
        var app = new SftpCheckApp();

        // Assert
        Assert.Null(app.PrivateKeyBase64);
    }

    [Fact]
    public void App_PrivateKeyPassphrase_CanBeNull()
    {
        // Arrange
        var app = new SftpCheckApp();

        // Assert
        Assert.Null(app.PrivateKeyPassphrase);
    }

    [Fact]
    public void App_PrivateKeyBase64_CanBeEmpty()
    {
        // Arrange
        var app = new SftpCheckApp();

        // Act
        app.PrivateKeyBase64 = "";

        // Assert
        Assert.Equal("", app.PrivateKeyBase64);
    }

    [Fact]
    public void CreateConnectionInfo_WithInvalidBase64_ThrowsFormatException()
    {
        // Arrange
        var app = new SftpCheckApp
        {
            SftpHost = "test.example.com",
            Port = 22,
            Username = "testuser",
            AuthenticationMethod = "PrivateKey",
            PrivateKeyBase64 = "not-valid-base64!!!"
        };

        // Act
        var method = typeof(SftpCheckApp).GetMethod("CreateConnectionInfo",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Assert
        var exception = Assert.ThrowsAny<TargetInvocationException>(() => method!.Invoke(app, null));
        Assert.IsType<FormatException>(exception.InnerException);
    }

    [Fact]
    public void CreateConnectionInfo_WithInvalidKeyContent_Throws()
    {
        // Arrange - Valid base64 but not a valid key
        var invalidKeyContent = "This is not a valid SSH private key";
        var app = new SftpCheckApp
        {
            SftpHost = "test.example.com",
            Port = 22,
            Username = "testuser",
            AuthenticationMethod = "PrivateKey",
            PrivateKeyBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(invalidKeyContent))
        };

        // Act
        var method = typeof(SftpCheckApp).GetMethod("CreateConnectionInfo",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Assert
        var exception = Assert.ThrowsAny<TargetInvocationException>(() => method!.Invoke(app, null));
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public void ConnectionInfo_TimeoutIsSetCorrectly()
    {
        // Arrange
        var app = new SftpCheckApp
        {
            SftpHost = "test.example.com",
            Port = 22,
            Username = "testuser",
            AuthenticationMethod = "Password",
            Password = "testpassword",
            ConnectionTimeoutSeconds = 120
        };

        // Act
        var method = typeof(SftpCheckApp).GetMethod("CreateConnectionInfo",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var connectionInfo = method!.Invoke(app, null) as ConnectionInfo;

        // Assert
        Assert.NotNull(connectionInfo);
        Assert.Equal(TimeSpan.FromSeconds(120), connectionInfo!.Timeout);
    }

    [Fact]
    public void ConnectionInfo_DefaultTimeoutIs30Seconds()
    {
        // Arrange
        var app = new SftpCheckApp
        {
            SftpHost = "test.example.com",
            Port = 22,
            Username = "testuser",
            AuthenticationMethod = "Password",
            Password = "testpassword"
            // ConnectionTimeoutSeconds defaults to 30
        };

        // Act
        var method = typeof(SftpCheckApp).GetMethod("CreateConnectionInfo",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var connectionInfo = method!.Invoke(app, null) as ConnectionInfo;

        // Assert
        Assert.NotNull(connectionInfo);
        Assert.Equal(TimeSpan.FromSeconds(30), connectionInfo!.Timeout);
    }

    #endregion
}
