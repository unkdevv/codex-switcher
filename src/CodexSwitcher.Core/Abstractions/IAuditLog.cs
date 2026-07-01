namespace CodexSwitcher.Core.Abstractions;

/// <summary>
/// Log de auditoria local (switch/refresh/add/remove/erros). <b>Nunca</b> registra tokens
/// nem o auth.json decifrado. Ver BUSINESS_RULES.md §7.
/// </summary>
public interface IAuditLog
{
    void Record(string action, string outcome, string? detail = null);
}
