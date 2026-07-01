using CodexSwitcher.App.Services;
using CodexSwitcher.Core.Models;
using CodexSwitcher.Core.Services;
using CodexSwitcher.Infra.Scheduling;
using Microsoft.UI.Xaml;

namespace CodexSwitcher.App;

public partial class App : Application
{
    private Window? _window;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppHost.Build();

        var cmdline = Environment.GetCommandLineArgs();
        if (cmdline.Any(a => string.Equals(a, "--refresh", StringComparison.OrdinalIgnoreCase)))
        {
            _ = RunHeadlessRefreshAsync();
            return;
        }

        RegisterRefreshTask();

        _window = new MainWindow();
        _window.Activate();
    }

    /// <summary>Modo headless disparado pelo Task Scheduler: renova contas due e sai. Ver §6.1.</summary>
    private async Task RunHeadlessRefreshAsync()
    {
        try
        {
            var profiles = Resolve<ProfileService>();
            var refresh = Resolve<RefreshService>();
            var settings = Resolve<AppSettings>();

            profiles.Load();
            foreach (var p in profiles.Profiles.ToList())
            {
                if (refresh.IsDue(p, settings.RefreshDueDays))
                    await refresh.RefreshAsync(p, TimeSpan.FromMinutes(2));
            }
            profiles.Save();
        }
        catch (Exception)
        {
            // Modo headless: sem UI para reportar; a auditoria já registrou.
        }
        finally
        {
            Exit();
        }
    }

    private void RegisterRefreshTask()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
                Resolve<RefreshScheduler>().EnsureDailyTask(exe);
        }
        catch (Exception)
        {
            // Agendamento é best-effort; o timer em app cobre enquanto aberto.
        }
    }

    private static T Resolve<T>() where T : class =>
        AppHost.Services.GetService(typeof(T)) as T
        ?? throw new InvalidOperationException($"Serviço {typeof(T).Name} não registrado.");
}
