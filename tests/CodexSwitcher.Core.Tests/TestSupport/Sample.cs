using System.Text;

namespace CodexSwitcher.Core.Tests.TestSupport;

/// <summary>Fábricas de dados de teste: JWTs e auth.json sintéticos (nunca credenciais reais).</summary>
public static class Sample
{
    /// <summary>Cria um id_token (JWT não assinado) com os claims dados. Só para teste local.</summary>
    public static string Jwt(string? sub = "user-abc-123", string? email = "user@example.com",
        DateTimeOffset? exp = null, string? plan = null)
    {
        var header = Base64Url("""{"alg":"none","typ":"JWT"}"""u8.ToArray());

        var payload = new StringBuilder("{");
        var first = true;
        void Add(string key, string value)
        {
            if (!first) payload.Append(',');
            payload.Append('"').Append(key).Append("\":\"").Append(value).Append('"');
            first = false;
        }
        if (sub is not null) Add("sub", sub);
        if (email is not null) Add("email", email);
        if (plan is not null) Add("plan_type", plan);
        if (exp is not null)
        {
            if (!first) payload.Append(',');
            payload.Append("\"exp\":").Append(exp.Value.ToUnixTimeSeconds());
            first = false;
        }
        payload.Append('}');

        var payloadB64 = Base64Url(Encoding.UTF8.GetBytes(payload.ToString()));
        return $"{header}.{payloadB64}.signaturenotvalidated";
    }

    /// <summary>
    /// auth.json sintético com a mesma forma do real (auth_mode, tokens.*, last_refresh) MAIS um
    /// campo desconhecido, para verificar preservação byte a byte (blob opaco).
    /// </summary>
    public static byte[] AuthJson(string? idToken = null, string accountId = "acct_1",
        string authMode = "chatgpt", string? lastRefresh = "2026-06-25T10:00:00Z",
        string refreshToken = "rt-synthetic-value")
    {
        idToken ??= Jwt();
        var json = $$"""
        {
          "auth_mode": "{{authMode}}",
          "OPENAI_API_KEY": null,
          "tokens": {
            "id_token": "{{idToken}}",
            "access_token": "at-synthetic-value",
            "refresh_token": "{{refreshToken}}",
            "account_id": "{{accountId}}"
          },
          "last_refresh": "{{lastRefresh}}",
          "future_unknown_field": { "nested": [1, 2, 3], "keep": true }
        }
        """;
        return Encoding.UTF8.GetBytes(json);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
