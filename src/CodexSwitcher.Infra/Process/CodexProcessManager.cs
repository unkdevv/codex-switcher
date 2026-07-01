using System.Diagnostics;
using CodexSwitcher.Core.Abstractions;
using CodexSwitcher.Core.Models;

namespace CodexSwitcher.Infra.Processes;

using SysProcess = System.Diagnostics.Process;

/// <summary>
/// Descobre e controla processos do Codex do usuário/sessão atual. Classifica por caminho do
/// executável (curado) para evitar falsos positivos. Nunca toca editores/IDE. O app desktop é
/// empacotado (MSIX, em WindowsApps) e é reaberto via <c>codex app</c>. Ver §4.5, pontos 19–27.
/// </summary>
public sealed class CodexProcessManager : IProcessManager
{
    private readonly ICodexCli _codex;

    public CodexProcessManager(ICodexCli codex) => _codex = codex ?? throw new ArgumentNullException(nameof(codex));

    public IReadOnlyList<CodexProcessInfo> FindRunningCodexProcesses()
    {
        var currentSession = SafeSessionId(SysProcess.GetCurrentProcess());
        var found = new List<CodexProcessInfo>();

        // GetProcessesByName é case-insensitive no Windows: pega "Codex" (app) e "codex" (CLI).
        foreach (var proc in SysProcess.GetProcessesByName("codex"))
        {
            try
            {
                if (SafeSessionId(proc) != currentSession)
                    continue; // só a sessão atual (ponto 26)

                var path = SafeExecutablePath(proc);
                if (path is null)
                    continue;

                var kind = Classify(path.ToLowerInvariant());
                if (kind == CodexProcessKind.Unknown)
                    continue;

                found.Add(new CodexProcessInfo(proc.Id, proc.ProcessName, path, null, kind));
            }
            catch (InvalidOperationException) { /* processo terminou */ }
            finally { proc.Dispose(); }
        }

        return found;
    }

    public bool AnyCodexCliRunning() =>
        FindRunningCodexProcesses().Any(p => p.Kind == CodexProcessKind.Cli);

    public async Task<IReadOnlyList<CodexProcessInfo>> CloseGracefullyThenKillAsync(
        IReadOnlyList<CodexProcessInfo> targets, TimeSpan gracefulTimeout,
        CancellationToken cancellationToken = default)
    {
        var closable = targets.Where(t => t.IsClosable).ToList();
        var live = new List<(CodexProcessInfo Info, SysProcess Proc)>();

        // (1) Fechamento gracioso: pedir para a janela fechar (dá chance de salvar).
        foreach (var t in closable)
        {
            var proc = TryGetProcess(t.Pid);
            if (proc is null) continue;
            try
            {
                if (proc.MainWindowHandle != IntPtr.Zero)
                    proc.CloseMainWindow();
                live.Add((t, proc));
            }
            catch (InvalidOperationException) { proc.Dispose(); }
        }

        // (2) Aguardar saída até o timeout.
        var deadline = DateTimeOffset.UtcNow + gracefulTimeout;
        var closed = new List<CodexProcessInfo>();
        while (live.Count > 0 && DateTimeOffset.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            for (var i = live.Count - 1; i >= 0; i--)
            {
                if (live[i].Proc.HasExited)
                {
                    closed.Add(live[i].Info);
                    live[i].Proc.Dispose();
                    live.RemoveAt(i);
                }
            }
            if (live.Count > 0)
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        // (3) Forçar (Kill) o que sobrou.
        foreach (var (info, proc) in live)
        {
            try
            {
                if (!proc.HasExited)
                    proc.Kill(entireProcessTree: true);
                closed.Add(info);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
            finally { proc.Dispose(); }
        }

        return closed;
    }

    public void Relaunch(CodexProcessInfo process)
    {
        if (process.Kind != CodexProcessKind.DesktopApp)
            throw new InvalidOperationException("Apenas o app desktop é reabrível.");

        // App empacotado (WindowsApps) → reabrir via `codex app`, não pelo caminho do .exe.
        _codex.LaunchDesktopApp();
    }

    private static CodexProcessKind Classify(string lowerPath)
    {
        // App desktop empacotado (MSIX): ...\WindowsApps\OpenAI.Codex_<ver>_..\app\Codex.exe
        // (inclui os processos auxiliares tipo Electron e o codex.exe em \app\resources).
        if (lowerPath.Contains("openai.codex") || lowerPath.Contains(@"\windowsapps\openai"))
            return CodexProcessKind.DesktopApp;

        // CLI standalone (npm global ou binário em AppData\Local\OpenAI\Codex\bin).
        if (lowerPath.Contains(@"\npm\")
            || lowerPath.Contains(@"\openai\codex\bin")
            || lowerPath.Contains(@"node_modules\@openai\codex"))
            return CodexProcessKind.Cli;

        return CodexProcessKind.Unknown;
    }

    private static SysProcess? TryGetProcess(int pid)
    {
        try { return SysProcess.GetProcessById(pid); }
        catch (ArgumentException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    private static string? SafeExecutablePath(SysProcess proc)
    {
        try { return proc.MainModule?.FileName; }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return null; // acesso negado (outro usuário) ou processo protegido → ignorar
        }
    }

    private static int SafeSessionId(SysProcess proc)
    {
        try { return proc.SessionId; }
        catch (InvalidOperationException) { return -1; }
    }
}
