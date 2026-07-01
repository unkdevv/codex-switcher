using System.Diagnostics;

namespace CodexSwitcher.Infra.Scheduling;

/// <summary>
/// Registra uma tarefa diária no Windows Task Scheduler que roda o app em modo headless
/// (<c>--refresh</c>) para renovar as contas mesmo com o app fechado, com margem antes dos ~8 dias.
/// Tarefa de usuário — não exige admin. Ver BUSINESS_RULES.md §6.1 e ponto 4.
/// </summary>
public sealed class RefreshScheduler
{
    private const string TaskName = "CodexSwitcherRefresh";

    /// <summary>Cria/atualiza a tarefa diária. Retorna true em sucesso. Best-effort.</summary>
    public bool EnsureDailyTask(string appExecutablePath, string startTime = "03:30")
    {
        var command = $"\"{appExecutablePath}\" --refresh";
        return Run(["/Create", "/TN", TaskName, "/TR", command, "/SC", "DAILY", "/ST", startTime, "/F"]);
    }

    public bool TaskExists() => Run(["/Query", "/TN", TaskName]);

    public bool RemoveTask() => Run(["/Delete", "/TN", TaskName, "/F"]);

    private static bool Run(IReadOnlyList<string> args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var proc = Process.Start(psi);
            if (proc is null) return false;
            proc.WaitForExit(15000);
            return proc.HasExited && proc.ExitCode == 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}
