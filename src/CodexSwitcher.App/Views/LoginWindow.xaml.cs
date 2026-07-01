using System.Text.RegularExpressions;
using CodexSwitcher.App.Localization;
using CodexSwitcher.Core.Abstractions;
using CodexSwitcher.Infra;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;

namespace CodexSwitcher.App.Views;

/// <summary>
/// Login efêmero: WebView2 com userDataFolder descartável e único, dirigindo o fluxo do
/// <c>codex login</c> num CODEX_HOME isolado. Ao fim, captura o auth.json gerado, descarta o
/// WebView2 e apaga as pastas temporárias. Ver BUSINESS_RULES.md §5 e pontos 7, 8.
/// </summary>
public sealed partial class LoginWindow : Window
{
    private static readonly Regex AnsiRegex = new(@"\x1B\[[0-9;]*m", RegexOptions.Compiled);
    private static readonly Regex UrlRegex = new(@"https?://[^\s'""]+", RegexOptions.Compiled);
    private static readonly Regex CodeRegex = new(@"\b[A-Z0-9]{4}-[A-Z0-9]{4,6}\b", RegexOptions.Compiled);

    private readonly ICodexCli _codex;
    private readonly AppPaths _paths;
    private readonly string _userDataFolder;
    private readonly string _codexHome;
    private readonly DispatcherQueue _dispatcher;
    private readonly TaskCompletionSource<byte[]?> _tcs = new();
    private readonly CancellationTokenSource _cts = new();

    private readonly Strings _loc = Strings.Current;
    private bool _navigated;
    private bool _completed;
    private string? _deviceCode;

    public LoginWindow(ICodexCli codex, AppPaths paths)
    {
        InitializeComponent();
        _codex = codex;
        _paths = paths;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _userDataFolder = Path.Combine(paths.TempRoot, "codex-webview-" + Guid.NewGuid().ToString("N"));
        _codexHome = Path.Combine(paths.TempRoot, "codex-login-" + Guid.NewGuid().ToString("N"));

        Title = _loc.LoginTitle;
        StatusText.Text = _loc.LoginPreparing;
        CleanNoteText.Text = _loc.LoginCleanNote;
        CodeLabel.Text = _loc.LoginCodeLabel;
        CancelButton.Content = _loc.Cancel;

        Closed += OnClosed;
    }

    public async Task<byte[]?> ShowAndWaitAsync()
    {
        Activate();
        await StartAsync();
        return await _tcs.Task;
    }

    private async Task StartAsync()
    {
        if (!_codex.IsAvailable)
        {
            SetStatus(_loc.LoginCodexNotFound);
            HintText.Text = _loc.LoginInstallCodex;
            return;
        }

        try
        {
            Directory.CreateDirectory(_userDataFolder);
            Directory.CreateDirectory(_codexHome);
            // Força file-store no login isolado (ponto 1) sem tocar o config real.
            await File.WriteAllTextAsync(Path.Combine(_codexHome, "config.toml"),
                "cli_auth_credentials_store = \"file\"\n");

            var env = await CoreWebView2Environment.CreateWithOptionsAsync(
                string.Empty, _userDataFolder, new CoreWebView2EnvironmentOptions());
            await Web.EnsureCoreWebView2Async(env);
            Web.CoreWebView2.NavigationStarting += OnNavigationStarting;

            SetStatus(_loc.LoginWaitingPage);
            _ = RunCodexLoginAsync();
        }
        catch (Exception ex)
        {
            SetStatus(_loc.LoginWebView2Failed);
            HintText.Text = _loc.LoginWebView2Hint + ex.Message;
        }
    }

    private async Task RunCodexLoginAsync()
    {
        try
        {
            var result = await _codex.LoginAsync(_codexHome, HandleOutputLine,
                TimeSpan.FromMinutes(15), _cts.Token);

            var authPath = Path.Combine(_codexHome, "auth.json");
            if (result.Success && File.Exists(authPath))
            {
                var bytes = await File.ReadAllBytesAsync(authPath);
                Complete(bytes);
            }
            else
            {
                Complete(null);
            }
        }
        catch (OperationCanceledException)
        {
            Complete(null);
        }
        catch (Exception)
        {
            Complete(null);
        }
    }

    private void HandleOutputLine(string rawLine)
    {
        var line = AnsiRegex.Replace(rawLine, string.Empty);

        // Captura o código de uso único do fluxo device-auth e o exibe para o usuário digitar.
        if (_deviceCode is null)
        {
            var codeMatch = CodeRegex.Match(line);
            if (codeMatch.Success)
            {
                _deviceCode = codeMatch.Value;
                _dispatcher.TryEnqueue(() =>
                {
                    CodeText.Text = _deviceCode;
                    CodePanel.Visibility = Visibility.Visible;
                });
            }
        }

        if (_navigated) return;
        var match = UrlRegex.Match(line);
        if (!match.Success) return;

        var url = match.Value.TrimEnd('.', ',', ')');
        if (!url.Contains("openai.com", StringComparison.OrdinalIgnoreCase)
            && !url.Contains("chatgpt.com", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _navigated = true;
        _dispatcher.TryEnqueue(() =>
        {
            try
            {
                Web.CoreWebView2.Navigate(url);
                SetStatus(_loc.LoginCleanOpened);
                HintText.Text = _loc.LoginCleanHint;
            }
            catch (Exception)
            {
                // Navegação falhou; o codex device-auth ainda aguarda a autorização.
            }
        });
    }

    private void OnNavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        if (args.Uri.Contains("localhost:1455", StringComparison.OrdinalIgnoreCase)
            || args.Uri.Contains("/auth/callback", StringComparison.OrdinalIgnoreCase))
        {
            _dispatcher.TryEnqueue(() => SetStatus(_loc.LoginCompleting));
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Complete(null);

    private void Complete(byte[]? result)
    {
        if (_completed) return;
        _completed = true;
        _dispatcher.TryEnqueue(() =>
        {
            _tcs.TrySetResult(result);
            Close();
        });
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _cts.Cancel();
        _tcs.TrySetResult(null);
        // Descartar handles do WebView2 antes de apagar (ponto 8).
        try { Web.Close(); } catch (Exception) { /* já fechando */ }
        _ = CleanupTempAsync();
    }

    private async Task CleanupTempAsync()
    {
        foreach (var dir in new[] { _userDataFolder, _codexHome })
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    if (Directory.Exists(dir))
                        Directory.Delete(dir, recursive: true);
                    break;
                }
                catch (IOException) { await Task.Delay(150); }
                catch (UnauthorizedAccessException) { await Task.Delay(150); }
            }
        }
    }

    private void SetStatus(string text)
    {
        StatusText.Text = text;
        Spinner.IsActive = !_completed;
    }
}
