#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodexBarWin.Core.Providers;

internal interface ISecretProtector
{
    byte[] Protect(byte[] plaintext);
    byte[] Unprotect(byte[] ciphertext);
}

internal sealed class WindowsUserProtector : ISecretProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("CodexBarWin/ClaudeCache/v1");
    public byte[] Protect(byte[] plaintext) => ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);
    public byte[] Unprotect(byte[] ciphertext) => ProtectedData.Unprotect(ciphertext, Entropy, DataProtectionScope.CurrentUser);
}

internal sealed class ClaudeCredentialStore
{
    private const int MaximumBytes = 256 * 1024;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("CBW-DPAPI-1\n");
    private readonly string _credentialsPath;
    private readonly string _storePath;
    private readonly string _legacyPath;
    private readonly bool _retireLegacy;
    private readonly ISecretProtector _protector;
    private string? _warning;
    private string? _retirementWarning;
    public string? Warning { get => _retirementWarning ?? _warning; private set => _warning = value; }

    public ClaudeCredentialStore(string claudeDir, string storeDir, string? legacyStoreDir = null,
        bool retireLegacy = true, ISecretProtector? protector = null)
    {
        _credentialsPath = Path.Combine(claudeDir, ".credentials.json");
        _storePath = Path.Combine(storeDir, "claude-token.dpapi");
        _legacyPath = Path.Combine(legacyStoreDir ?? storeDir, "claude-token.json");
        // Never delete another instance's cache or the CLI's login file.
        _retireLegacy = retireLegacy && string.Equals(Path.GetFullPath(storeDir),
            Path.GetFullPath(legacyStoreDir ?? storeDir), StringComparison.OrdinalIgnoreCase);
        _protector = protector ?? new WindowsUserProtector();
    }

    public ClaudeCredentials? ReadFromCli()
    {
        byte[]? bytes = null;
        try
        {
            bytes = ReadBounded(_credentialsPath);
            if (bytes == null) return null;
            using var document = JsonDocument.Parse(bytes);
            return document.RootElement.TryGetProperty("claudeAiOauth", out var oauth) && oauth.ValueKind == JsonValueKind.Object
                ? FromToken(JsonSerializer.Deserialize<StoredToken>(oauth.GetRawText())) : null;
        }
        catch (Exception ex) when (IsCacheException(ex)) { return null; }
        finally { if (bytes != null) CryptographicOperations.ZeroMemory(bytes); }
    }

    public ClaudeCredentials? ReadFromStore()
    {
        Warning = null;
        var encrypted = ReadProtected();
        var legacy = ReadLegacy();
        var selected = Freshest(encrypted, legacy);
        bool protectedCopyExists = encrypted != null && SameCredentials(selected, encrypted);
        if (selected != null && !protectedCopyExists) protectedCopyExists = WriteToStore(selected);
        if (legacy != null && protectedCopyExists && _retireLegacy)
        {
            try { File.Delete(_legacyPath); _retirementWarning = null; }
            catch (Exception ex) when (IsCacheException(ex)) { _retirementWarning = "The old plaintext widget cache could not be removed."; }
        }
        return selected;
    }

    private ClaudeCredentials? ReadProtected()
    {
        byte[]? plaintext = null;
        try
        {
            var bytes = ReadBounded(_storePath);
            if (bytes == null) return null;
            if (!bytes.AsSpan().StartsWith(Magic)) throw new CryptographicException("Unknown protected cache format.");
            plaintext = _protector.Unprotect(bytes[Magic.Length..]);
            return FromToken(JsonSerializer.Deserialize<StoredToken>(plaintext));
        }
        catch (Exception ex) when (IsCacheException(ex))
        {
            Warning = "The protected widget cache could not be read; using another available login.";
            return null;
        }
        finally { if (plaintext != null) CryptographicOperations.ZeroMemory(plaintext); }
    }

    private ClaudeCredentials? ReadLegacy()
    {
        byte[]? bytes = null;
        try
        {
            bytes = ReadBounded(_legacyPath);
            return bytes == null ? null : FromToken(JsonSerializer.Deserialize<StoredToken>(bytes));
        }
        catch (Exception ex) when (IsCacheException(ex)) { return null; }
        finally { if (bytes != null) CryptographicOperations.ZeroMemory(bytes); }
    }

    public bool WriteToStore(ClaudeCredentials credentials)
    {
        string temporary = _storePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        byte[]? plaintext = null;
        byte[]? verified = null;
        try
        {
            plaintext = JsonSerializer.SerializeToUtf8Bytes(new StoredToken
            {
                AccessToken = credentials.AccessToken,
                RefreshToken = credentials.RefreshToken,
                ExpiresAt = credentials.ExpiresAt?.ToUnixTimeMilliseconds(),
                Scopes = credentials.Scopes,
                SubscriptionType = credentials.SubscriptionType
            });
            var ciphertext = _protector.Protect(plaintext);
            // Verify encryption/decryption before replacing any good cache or retiring legacy data.
            verified = _protector.Unprotect(ciphertext);
            if (!CryptographicOperations.FixedTimeEquals(plaintext, verified)) throw new CryptographicException("Cache verification failed.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_storePath))!);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(Magic);
                stream.Write(ciphertext);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, _storePath, overwrite: true);
            Warning = null;
            return true;
        }
        catch (Exception ex) when (IsCacheException(ex))
        {
            Warning = "Login is usable in memory, but Windows could not save its protected widget cache.";
            return false; // Never fall back to writing plaintext.
        }
        finally
        {
            if (plaintext != null) CryptographicOperations.ZeroMemory(plaintext);
            if (verified != null) CryptographicOperations.ZeroMemory(verified);
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception ex) when (IsCacheException(ex)) { }
        }
    }

    internal static ClaudeCredentials? Freshest(ClaudeCredentials? first, ClaudeCredentials? second)
    {
        if (first == null) return second;
        if (second == null) return first;
        if (first.HasProfileScope != second.HasProfileScope) return first.HasProfileScope ? first : second;
        return (first.ExpiresAt ?? DateTimeOffset.MinValue) >= (second.ExpiresAt ?? DateTimeOffset.MinValue) ? first : second;
    }

    internal static bool SameCredentials(ClaudeCredentials? first, ClaudeCredentials? second) => first != null && second != null &&
        first.AccessToken == second.AccessToken && first.RefreshToken == second.RefreshToken && first.ExpiresAt == second.ExpiresAt &&
        first.SubscriptionType == second.SubscriptionType && first.Scopes.SequenceEqual(second.Scopes, StringComparer.Ordinal);

    private static byte[]? ReadBounded(string path)
    {
        if (!File.Exists(path)) return null;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length <= 0 || stream.Length > MaximumBytes) return null;
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    private static bool IsCacheException(Exception ex) => ex is IOException or UnauthorizedAccessException or
        CryptographicException or JsonException or ArgumentException or NotSupportedException or System.Security.SecurityException;

    private static ClaudeCredentials? FromToken(StoredToken? token)
    {
        if (token == null || string.IsNullOrWhiteSpace(token.AccessToken)) return null;
        DateTimeOffset? expires = token.ExpiresAt.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(token.ExpiresAt.Value) : null;
        return new ClaudeCredentials(token.AccessToken, token.RefreshToken, expires,
            (token.Scopes ?? Array.Empty<string>()).Where(s => !string.IsNullOrEmpty(s)).ToArray(), token.SubscriptionType);
    }

    private sealed class StoredToken
    {
        public StoredToken() { }
        [JsonPropertyName("accessToken")] public string? AccessToken { get; set; }
        [JsonPropertyName("refreshToken")] public string? RefreshToken { get; set; }
        [JsonPropertyName("expiresAt")] public long? ExpiresAt { get; set; }
        [JsonPropertyName("scopes")] public string[]? Scopes { get; set; }
        [JsonPropertyName("subscriptionType")] public string? SubscriptionType { get; set; }
    }
}
