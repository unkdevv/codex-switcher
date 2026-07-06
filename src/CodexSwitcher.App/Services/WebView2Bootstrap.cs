using System.Diagnostics;
using System.Net.Http;
using Microsoft.Web.WebView2.Core;

namespace CodexSwitcher.App.Services;

/// <summary>
/// Checagem/instalação do WebView2 Runtime, compartilhada entre <see cref="Views.LoginWindow"/> e
/// <see cref="Views.PrivateBrowserWindow"/> (ambas hospedam WebView2 e precisam do mesmo fallback em
/// Windows 10, que não vem com o runtime pré-instalado).
/// </summary>
public static class WebView2Bootstrap
{
    // Fwlink fixo e documentado pela Microsoft para o Evergreen Bootstrapper do WebView2.
    private const string BootstrapperUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

    public static bool IsRuntimeInstalled()
    {
        try
        {
            return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString(null));
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Baixa o bootstrapper oficial (Evergreen, ~2 MB) e o executa. Só o bootstrapper é obtido
    /// em tempo real (empacotar o runtime completo, ~150 MB e sem auto-atualização, infla o instalador
    /// à toa).</summary>
    public static async Task<bool> TryInstallRuntimeAsync(string tempRoot, CancellationToken ct)
    {
        var bootstrapperPath = Path.Combine(tempRoot, $"MicrosoftEdgeWebview2Setup-{Guid.NewGuid():N}.exe");

        try
        {
            Directory.CreateDirectory(tempRoot);

            using (var http = new HttpClient())
            {
                var bytes = await http.GetByteArrayAsync(BootstrapperUrl, ct);
                await File.WriteAllBytesAsync(bootstrapperPath, bytes, ct);
            }

            using var proc = Process.Start(new ProcessStartInfo(bootstrapperPath) { UseShellExecute = true });
            if (proc is null) return false;

            await proc.WaitForExitAsync(ct);
            return proc.ExitCode == 0 && IsRuntimeInstalled();
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            try { if (File.Exists(bootstrapperPath)) File.Delete(bootstrapperPath); } catch (Exception) { /* best-effort */ }
        }
    }
}
