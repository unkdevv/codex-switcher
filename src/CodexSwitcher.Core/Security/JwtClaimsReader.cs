using System.Text;
using System.Text.Json;
using CodexSwitcher.Core.Models;

namespace CodexSwitcher.Core.Security;

/// <summary>
/// Decodifica claims do id_token (JWT) <b>localmente</b>, sem rede, para identificar a conta.
/// Nunca valida assinatura (não é autenticação — é só exibição) e nunca loga o token.
/// Ver BUSINESS_RULES.md §7 e ponto 12.
/// </summary>
public static class JwtClaimsReader
{
    /// <summary>
    /// Extrai <c>sub</c>, <c>email</c>, expiração e plano do payload do JWT. Retorna claims
    /// vazios se o token for malformado (nunca lança por causa de conteúdo inválido).
    /// </summary>
    public static AccountClaims Read(string? idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
            return new AccountClaims(null, null, null, null);

        var parts = idToken.Split('.');
        if (parts.Length < 2)
            return new AccountClaims(null, null, null, null);

        try
        {
            var payloadJson = DecodeBase64Url(parts[1]);
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            var sub = GetString(root, "sub");
            var email = GetString(root, "email");
            var exp = GetUnixSeconds(root, "exp");
            var plan = ReadPlan(root);

            return new AccountClaims(sub, email, exp, plan);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return new AccountClaims(null, null, null, null);
        }
    }

    private static string? ReadPlan(JsonElement root)
    {
        // O plano pode aparecer em claims aninhados dependendo da versão; tentamos alguns
        // caminhos comuns sem depender de schema fixo (blob opaco).
        var direct = GetString(root, "plan") ?? GetString(root, "plan_type") ?? GetString(root, "chatgpt_plan_type");
        if (direct is not null)
            return direct;

        if (root.TryGetProperty("https://api.openai.com/auth", out var authClaim) &&
            authClaim.ValueKind == JsonValueKind.Object)
        {
            return GetString(authClaim, "chatgpt_plan_type") ?? GetString(authClaim, "plan_type");
        }

        return null;
    }

    private static string? GetString(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object &&
        obj.TryGetProperty(name, out var v) &&
        v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static DateTimeOffset? GetUnixSeconds(JsonElement obj, string name)
    {
        if (obj.ValueKind == JsonValueKind.Object &&
            obj.TryGetProperty(name, out var v) &&
            v.ValueKind == JsonValueKind.Number &&
            v.TryGetInt64(out var seconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }
        return null;
    }

    private static string DecodeBase64Url(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        var bytes = Convert.FromBase64String(s);
        return Encoding.UTF8.GetString(bytes);
    }
}
