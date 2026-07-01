using CodexSwitcher.App.Localization;
using CodexSwitcher.Core.Security;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;

namespace CodexSwitcher.App.Views;

/// <summary>
/// Janelinha do gerador de código 2FA (TOTP, RFC 6238). O usuário cola a chave secreta (ou um link
/// otpauth://) e vê o código de 6 dígitos com a contagem regressiva; ao expirar, um novo é gerado
/// automaticamente. A chave fica apenas em memória (nada é salvo em disco). Ver Totp/Base32 no Core.
/// </summary>
public sealed partial class TotpWindow : Window
{
    private readonly Strings _loc = Strings.Current;
    private readonly DispatcherTimer _timer;
    private TotpSecret? _secret;
    private string _currentCode = string.Empty;
    private DateTimeOffset _copiedFlashUntil = DateTimeOffset.MinValue;

    public TotpWindow()
    {
        InitializeComponent();

        HeaderText.Text = _loc.TotpTitle;
        SubtitleText.Text = _loc.TotpSubtitle;
        SecretLabel.Text = _loc.TotpSecretLabel;
        SecretBox.PlaceholderText = _loc.TotpSecretPlaceholder;
        EmptyHintText.Text = _loc.TotpEmptyHint;
        CopyButtonText.Text = _loc.TotpCopy;
        ToolTipService.SetToolTip(PasteButton, _loc.TotpPaste);
        ToolTipService.SetToolTip(CodeButton, _loc.TotpClickToCopy);
        Title = _loc.TotpTitle;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += OnTick;

        ConfigureWindow();
        Closed += (_, _) => _timer.Stop();
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

    private void OnSecretChanged(object sender, TextChangedEventArgs e)
    {
        var text = SecretBox.Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            _secret = null;
            _timer.Stop();
            ErrorText.Visibility = Visibility.Collapsed;
            ShowCode(false);
            return;
        }

        if (Totp.TryParse(text, out var parsed, out _))
        {
            _secret = parsed;
            ErrorText.Visibility = Visibility.Collapsed;
            AccountText.Text = DescribeAccount(parsed!);
            AccountText.Visibility = string.IsNullOrEmpty(AccountText.Text) ? Visibility.Collapsed : Visibility.Visible;
            ShowCode(true);
            Render();
            _timer.Start();
        }
        else
        {
            _secret = null;
            _timer.Stop();
            ShowCode(false);
            ErrorText.Text = _loc.TotpInvalid;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void OnTick(object? sender, object e) => Render();

    private void Render()
    {
        if (_secret is null) return;

        var code = _secret.Compute(DateTimeOffset.UtcNow);
        _currentCode = code.Code;
        CodeText.Text = code.Formatted;
        SecondsText.Text = code.SecondsRemaining.ToString();
        ExpiryText.Text = _loc.TotpExpiresIn(code.SecondsRemaining);
        Countdown.Value = code.Period <= 0 ? 0 : code.SecondsRemaining * 100.0 / code.Period;

        if (DateTimeOffset.UtcNow >= _copiedFlashUntil && CopiedPanel.Visibility == Visibility.Visible)
            CopiedPanel.Visibility = Visibility.Collapsed;
    }

    private static string DescribeAccount(TotpSecret secret)
    {
        if (!string.IsNullOrWhiteSpace(secret.Issuer)) return secret.Issuer!;
        if (!string.IsNullOrWhiteSpace(secret.Label)) return secret.Label!;
        return string.Empty;
    }

    private void ShowCode(bool visible)
    {
        CodeCard.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        CopyButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        EmptyHint.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;
        if (!visible) CopiedPanel.Visibility = Visibility.Collapsed;
    }

    private void OnCopyClick(object sender, RoutedEventArgs e) => CopyCurrentCode();

    private void OnCodeClick(object sender, RoutedEventArgs e) => CopyCurrentCode();

    /// <summary>Copia o código atual (sem espaços) e mostra o feedback "Copiado!".</summary>
    private void CopyCurrentCode()
    {
        if (string.IsNullOrEmpty(_currentCode)) return;
        try
        {
            var data = new DataPackage();
            data.SetText(_currentCode);
            Clipboard.SetContent(data);

            CopiedText.Text = _loc.TotpCopied;
            CopiedPanel.Visibility = Visibility.Visible;
            _copiedFlashUntil = DateTimeOffset.UtcNow.AddSeconds(2);
        }
        catch (Exception)
        {
            // Clipboard ocupado por outro app; ignorar silenciosamente.
        }
    }

    private async void OnPasteClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var content = Clipboard.GetContent();
            if (content.Contains(StandardDataFormats.Text))
            {
                var text = await content.GetTextAsync();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    SecretBox.Text = text.Trim();
                    SecretBox.SelectAll();
                }
            }
        }
        catch (Exception)
        {
            // Sem texto no clipboard ou acesso negado; ignorar.
        }
    }
}
