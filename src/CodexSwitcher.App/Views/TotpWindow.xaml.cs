using CodexSwitcher.App.Localization;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace CodexSwitcher.App.Views;

/// <summary>
/// Janela cheia do gerador de código 2FA, aberta a partir da tela principal (AccountsView). O conteúdo
/// (colar segredo/otpauth, ver código + contagem) mora em <see cref="TotpPanel"/>, reaproveitado também
/// pelo popup preso ao botão "2FA" da <see cref="LoginWindow"/>.
/// </summary>
public sealed partial class TotpWindow : Window
{
    public TotpWindow()
    {
        InitializeComponent();

        Title = Strings.Current.TotpTitle;
        ConfigureWindow();
        Closed += (_, _) => Panel.StopTimer();
    }

    private void ConfigureWindow()
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(id);

            appWindow.Resize(new SizeInt32(460, 560));

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
            }

            var work = DisplayArea.GetFromWindowId(id, DisplayAreaFallback.Nearest).WorkArea;
            appWindow.Move(new PointInt32(
                work.X + (work.Width - appWindow.Size.Width) / 2,
                work.Y + (work.Height - appWindow.Size.Height) / 2));

            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
            if (File.Exists(iconPath)) appWindow.SetIcon(iconPath);
        }
        catch (Exception)
        {
            // Ajustes de janela são cosméticos; falha não impede o gerador de funcionar.
        }
    }
}
