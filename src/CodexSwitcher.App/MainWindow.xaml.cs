using CodexSwitcher.App.Services;
using Microsoft.UI.Xaml;

namespace CodexSwitcher.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(Root.TitleBarElement);
        TrySetIcon();

        if (AppHost.Services.GetService(typeof(IUiInteraction)) is UiInteractionService ui)
            ui.Attach(this);
    }

    private void TrySetIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
            if (!File.Exists(iconPath))
                return;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id).SetIcon(iconPath);
        }
        catch (Exception)
        {
            // Ícone é cosmético; falha não deve impedir a janela de abrir.
        }
    }
}
