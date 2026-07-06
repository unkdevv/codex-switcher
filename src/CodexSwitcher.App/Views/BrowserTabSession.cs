using CodexSwitcher.Infra.Io;
using Microsoft.Web.WebView2.Core;

namespace CodexSwitcher.App.Views;

/// <summary>
/// Sessão descartável de navegação: pasta de perfil única (<c>codex-browser-*</c>) + ambiente
/// WebView2 próprios, nunca reaproveitados de execuções anteriores. Uma sessão nasce com cada aba
/// criada pelo usuário (botão "+") e é compartilhada apenas com as abas que as páginas dela abrirem
/// (links <c>target=_blank</c>/popups), que precisam da mesma sessão para funcionar (window.opener,
/// logins em popup). A pasta é apagada quando a última aba da sessão fecha; resíduos de sessões que
/// não puderam ser apagados são varridos pelo TempCleanup no próximo arranque.
/// </summary>
internal sealed class BrowserTabSession
{
    private int _refCount;

    public string UserDataFolder { get; }
    public Task<CoreWebView2Environment> Environment { get; }

    public BrowserTabSession(string tempRoot)
    {
        UserDataFolder = Path.Combine(tempRoot, "codex-browser-" + Guid.NewGuid().ToString("N"));
        Environment = CreateEnvironmentAsync();
    }

    private async Task<CoreWebView2Environment> CreateEnvironmentAsync()
    {
        Directory.CreateDirectory(UserDataFolder);
        return await CoreWebView2Environment.CreateWithOptionsAsync(
            string.Empty, UserDataFolder, new CoreWebView2EnvironmentOptions());
    }

    public void AddRef() => Interlocked.Increment(ref _refCount);

    /// <summary>Solta uma referência; ao fechar a última aba da sessão, apaga a pasta de perfil
    /// (com retentativas, enquanto o WebView2 solta os handles).</summary>
    public async Task ReleaseAsync()
    {
        if (Interlocked.Decrement(ref _refCount) > 0) return;

        for (var attempt = 0; attempt < 8; attempt++)
        {
            if (TempCleanup.TryForceDelete(UserDataFolder)) break;
            await Task.Delay(200);
        }
    }
}
