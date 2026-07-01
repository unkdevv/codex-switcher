using System.Text.Json;
using CodexSwitcher.Core.Models;

namespace CodexSwitcher.Core.Security;

/// <summary>
/// Lê apenas os campos necessários do auth.json (blob opaco): auth_mode, last_refresh,
/// tokens.id_token, tokens.account_id. Nunca muta o arquivo; o restante é preservado byte a byte
/// pelo cofre. Ver BUSINESS_RULES.md §2.3 e ponto 11.
/// </summary>
public static class AuthJsonReader
{
    /// <summary>
    /// Lê os campos leves do auth.json. Retorna null se o conteúdo não for JSON válido
    /// (o chamador trata como <see cref="ErrorCategory.InvalidAuthFile"/>).
    /// </summary>
    public static AuthFileInfo? TryRead(byte[] authJsonBytes)
    {
        ArgumentNullException.ThrowIfNull(authJsonBytes);
        try
        {
            using var doc = JsonDocument.Parse(authJsonBytes);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            var authMode = GetString(root, "auth_mode");
            var lastRefresh = GetDateTime(root, "last_refresh");

            string? idToken = null;
            string? accountId = null;
            if (root.TryGetProperty("tokens", out var tokens) && tokens.ValueKind == JsonValueKind.Object)
            {
                idToken = GetString(tokens, "id_token");
                accountId = GetString(tokens, "account_id");
            }

            return new AuthFileInfo(authMode, lastRefresh, idToken, accountId);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Combina os campos do auth.json com os claims do id_token para identificar a conta.</summary>
    public static (AuthFileInfo? File, AccountClaims Claims) Identify(byte[] authJsonBytes)
    {
        var info = TryRead(authJsonBytes);
        var claims = JwtClaimsReader.Read(info?.IdToken);
        return (info, claims);
    }

    private static string? GetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static DateTimeOffset? GetDateTime(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var v))
            return null;

        return v.ValueKind switch
        {
            JsonValueKind.String when DateTimeOffset.TryParse(v.GetString(), out var dto) => dto,
            JsonValueKind.Number when v.TryGetInt64(out var unix) => DateTimeOffset.FromUnixTimeSeconds(unix),
            _ => null,
        };
    }
}
