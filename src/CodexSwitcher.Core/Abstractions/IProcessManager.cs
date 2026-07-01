using CodexSwitcher.Core.Models;

namespace CodexSwitcher.Core.Abstractions;

/// <summary>
/// Descoberta e controle de processos do Codex do usuário atual. Ver BUSINESS_RULES.md §4.5
/// e pontos 19–27. Só atua sobre processos do próprio usuário; nunca eleva privilégio.
/// </summary>
public interface IProcessManager
{
    /// <summary>Enumera processos do Codex em execução do usuário atual, já classificados.</summary>
    IReadOnlyList<CodexProcessInfo> FindRunningCodexProcesses();

    /// <summary>Existe alguma CLI codex viva? Usado na verificação anti-corrida (§4.3 passo 4).</summary>
    bool AnyCodexCliRunning();

    /// <summary>
    /// Fecha graciosamente (CloseMainWindow) e, após timeout, mata o que sobrar.
    /// Retorna a lista efetivamente encerrada. Só processos <see cref="CodexProcessInfo.IsClosable"/>.
    /// </summary>
    Task<IReadOnlyList<CodexProcessInfo>> CloseGracefullyThenKillAsync(
        IReadOnlyList<CodexProcessInfo> targets,
        TimeSpan gracefulTimeout,
        CancellationToken cancellationToken = default);

    /// <summary>Relança um processo reabrível (só app desktop). Lança em falha para o chamador tratar.</summary>
    void Relaunch(CodexProcessInfo process);
}
