using CodexSwitcher.App.Localization;
using CodexSwitcher.App.Services;
using CodexSwitcher.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CodexSwitcher.App.Views;

public sealed partial class AccountsView : UserControl
{
    public MainViewModel ViewModel { get; }
    public Strings Loc => Strings.Current;

    /// <summary>Elemento usado como região de arraste da barra de título (Mica).</summary>
    public UIElement TitleBarElement => AppTitleBar;

    public AccountsView()
    {
        InitializeComponent();
        ViewModel = AppHost.Services.GetService(typeof(MainViewModel)) as MainViewModel
                    ?? throw new InvalidOperationException("MainViewModel não registrado.");
        TrySetTitleBarIcon();
        Loaded += OnLoaded;
    }

    private void TrySetTitleBarIcon()
    {
        try
        {
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
            if (System.IO.File.Exists(iconPath))
                AppIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(iconPath));
        }
        catch (Exception)
        {
            // Ícone é cosmético.
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    private TotpWindow? _totpWindow;

    private void OnOpen2FaClick(object sender, RoutedEventArgs e)
    {
        // Reaproveita a janelinha se já estiver aberta (evita duplicatas).
        if (_totpWindow is not null)
        {
            _totpWindow.Activate();
            return;
        }

        _totpWindow = new TotpWindow();
        _totpWindow.Closed += (_, _) => _totpWindow = null;
        _totpWindow.Activate();
    }

    private static AccountItemViewModel? ItemOf(object sender) =>
        (sender as FrameworkElement)?.DataContext as AccountItemViewModel;

    private void OnSwitchClick(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item) ViewModel.SwitchCommand.Execute(item);
    }

    private void OnRefreshItemClick(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item) ViewModel.RefreshOneCommand.Execute(item);
    }

    private void OnRenameItemClick(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item) ViewModel.RenameCommand.Execute(item);
    }

    private void OnRemoveItemClick(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item) ViewModel.RemoveCommand.Execute(item);
    }

    private void OnMarkReLoginClick(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item) ViewModel.MarkNeedsReLoginCommand.Execute(item);
    }
}
