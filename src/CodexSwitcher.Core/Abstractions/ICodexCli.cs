namespace CodexSwitcher.Core.Abstractions;

/// <summary>Resultado bruto de uma invocação do binário codex.</summary>
public sealed record CodexCliResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Success => ExitCode == 0;
}

/// <summary>
/// Executa o binário <c>codex</c>. Permite definir <c>CODEX_HOME</c> <b>apenas</b> no processo
/// filho — nunca globalmente — para isolar login/refresh do slot ativo real.
/// Ver BUSINESS_RULES.md §5, §6 e ponto 7.
/// </summary>
public interface ICodexCli
{
    /// <summary>O binário codex foi localizado (PATH ou override configurado)?</summary>
    bool IsAvailable { get; }

    /// <summary>Caminho resolvido do binário, ou null se não encontrado.</summary>
    string? ResolvedPath { get; }

    /// <summary>
    /// Executa <c>codex exec "&lt;prompt&gt;"</c> com CODEX_HOME isolado, disparando o refresh
    /// se o token estiver velho. Ver §6.2.
    /// </summary>
    Task<CodexCliResult> ExecAsync(
        string prompt,
        string codexHome,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>Executa <c>codex login status</c> com CODEX_HOME dado (exit 0 = logado).</summary>
    Task<CodexCliResult> LoginStatusAsync(
        string codexHome,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Relança o app desktop do Codex via <c>codex app</c> (fire-and-forget). É o modo correto de
    /// reabrir o app empacotado (o .exe fica em WindowsApps, protegido). Ver §4.5 e ponto 20.
    /// </summary>
    void LaunchDesktopApp();

    /// <summary>
    /// Executa <c>codex login</c> com CODEX_HOME isolado, transmitindo cada linha de saída para
    /// <paramref name="onOutputLine"/> (permite capturar a URL de autorização ao vivo e abri-la no
    /// WebView2 efêmero). Retorna quando o processo termina. Ver §5.2.
    /// </summary>
    Task<CodexCliResult> LoginAsync(
        string codexHome,
        Action<string>? onOutputLine,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
