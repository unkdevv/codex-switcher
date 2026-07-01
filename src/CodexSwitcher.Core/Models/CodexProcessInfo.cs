namespace CodexSwitcher.Core.Models;

/// <summary>
/// Instantâneo de um processo do Codex em execução, capturado ANTES de encerrar,
/// para permitir a reabertura. Ver BUSINESS_RULES.md §4.5 e ponto 23.
/// </summary>
public sealed record CodexProcessInfo(
    int Pid,
    string ProcessName,
    string? ExecutablePath,
    string? Arguments,
    CodexProcessKind Kind)
{
    /// <summary>Só o app desktop é reabrível; CLI não; IDE nunca é tocada.</summary>
    public bool IsReopenable => Kind == CodexProcessKind.DesktopApp && !string.IsNullOrEmpty(ExecutablePath);

    /// <summary>Deve ser encerrado num switch? IDE host nunca; desconhecido nunca.</summary>
    public bool IsClosable => Kind is CodexProcessKind.DesktopApp or CodexProcessKind.Cli;
}
