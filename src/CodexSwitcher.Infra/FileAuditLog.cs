using System.Text;
using CodexSwitcher.Core.Abstractions;

namespace CodexSwitcher.Infra;

/// <summary>
/// Log de auditoria local (append-only). Registra ação/resultado/detalhe com timestamp UTC.
/// <b>Nunca</b> deve receber tokens (responsabilidade dos chamadores). Ver BUSINESS_RULES.md §7.
/// </summary>
public sealed class FileAuditLog : IAuditLog
{
    private readonly string _path;
    private readonly Lock _gate = new();

    public FileAuditLog(string path)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    public void Record(string action, string outcome, string? detail = null)
    {
        var line = $"{DateTimeOffset.UtcNow:O}\t{Sanitize(action)}\t{Sanitize(outcome)}\t{Sanitize(detail)}{Environment.NewLine}";
        lock (_gate)
        {
            File.AppendAllText(_path, line, Encoding.UTF8);
        }
    }

    private static string Sanitize(string? value) =>
        string.IsNullOrEmpty(value) ? "-" : value.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
}
