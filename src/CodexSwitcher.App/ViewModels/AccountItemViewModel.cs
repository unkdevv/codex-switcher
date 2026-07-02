using CodexSwitcher.App.Localization;
using CodexSwitcher.Core.Models;
using CodexSwitcher.Core.Support;

namespace CodexSwitcher.App.ViewModels;

/// <summary>Selo visual derivado do estado do perfil. Ver BUSINESS_RULES.md §3.4.</summary>
public enum AccountBadge
{
    ActiveNow,
    Healthy,
    RenewSoon,
    Refreshing,
    NeedsReLogin,
    Error,
    Unavailable,
}

/// <summary>
/// Snapshot de exibição de um perfil (imutável; a lista é reconstruída após cada operação).
/// Formata datas relativas e o selo de saúde conforme §3.4 e §8, no idioma detectado.
/// </summary>
public sealed class AccountItemViewModel
{
    private static Strings Loc => Strings.Current;

    public AccountItemViewModel(ProfileMetadata profile, DateTimeOffset now, AppSettings settings)
    {
        Profile = profile;
        Id = profile.Id;
        DisplayName = profile.DisplayName;
        IsActive = profile.IsActive;

        Subtitle = !string.IsNullOrWhiteSpace(profile.AccountEmail)
            ? profile.AccountEmail!
            : profile.AuthMode is { Length: > 0 } mode ? Loc.ModeFormat(mode) : Loc.CodexAccount;

        Initials = ComputeInitials(profile);
        Badge = ComputeBadge(profile);
        PlanText = profile.PlanType;

        LastSwitchedText = profile.LastSwitchedAt is null
            ? Loc.NeverUsedHere
            : Loc.SwitchedFormat(RelativeTime.Humanize(profile.LastSwitchedAt, now, Loc.Pt));
        LastSwitchedTooltip = profile.LastSwitchedAt?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "-";

        HealthText = ComputeHealthText(profile, now, settings);
        NeedsAttention = Badge is AccountBadge.NeedsReLogin or AccountBadge.Error;
        IsMarkedUsed = profile.MarkedUsedAt is { } markedAt && (now - markedAt) < TimeSpan.FromHours(24);
    }

    public ProfileMetadata Profile { get; }
    public Guid Id { get; }
    public string DisplayName { get; }
    public string Subtitle { get; }
    public string Initials { get; }
    public bool IsActive { get; }
    public AccountBadge Badge { get; }
    public string? PlanText { get; }
    public string LastSwitchedText { get; }
    public string LastSwitchedTooltip { get; }
    public string HealthText { get; }
    public bool NeedsAttention { get; }

    /// <summary>Marcado como "usado" nas últimas 24h. Ver <see cref="ProfileMetadata.MarkedUsedAt"/>.</summary>
    public bool IsMarkedUsed { get; }

    public bool CanSwitch => !IsActive && Badge != AccountBadge.Unavailable;
    public bool IsRefreshing => Badge == AccountBadge.Refreshing;

    // Rótulos localizados usados dentro do DataTemplate do card.
    public string SwitchLabel => Loc.Switch;
    public string InUseLabel => Loc.InUse;
    public string RefreshNowLabel => Loc.RefreshNow;
    public string RenameLabel => Loc.Rename;
    public string MarkReLoginLabel => Loc.MarkNeedsReLogin;
    public string MarkUsedLabel => Loc.MarkUsed;
    public string UnmarkUsedLabel => Loc.UnmarkUsed;
    public string RemoveLabel => Loc.Remove;
    public string MoreActionsLabel => Loc.MoreActions;

    private static AccountBadge ComputeBadge(ProfileMetadata p) => p.HealthStatus switch
    {
        HealthStatus.Unknown => AccountBadge.Unavailable,
        HealthStatus.Error => AccountBadge.Error,
        HealthStatus.NeedsReLogin => AccountBadge.NeedsReLogin,
        HealthStatus.Refreshing => AccountBadge.Refreshing,
        HealthStatus.Stale => p.IsActive ? AccountBadge.ActiveNow : AccountBadge.RenewSoon,
        _ => p.IsActive ? AccountBadge.ActiveNow : AccountBadge.Healthy,
    };

    private static string ComputeHealthText(ProfileMetadata p, DateTimeOffset now, AppSettings settings)
    {
        if (p.HealthStatus == HealthStatus.NeedsReLogin) return Loc.HealthNeedsReLogin;
        if (p.HealthStatus == HealthStatus.Error) return p.LastError?.Message ?? Loc.HealthError;
        if (p.HealthStatus == HealthStatus.Refreshing) return Loc.HealthRefreshing;
        if (p.HealthStatus == HealthStatus.Unknown) return Loc.HealthUnavailable;

        if (p.LastRefreshedAt is null) return Loc.HealthNeverRefreshed;

        var days = (now - p.LastRefreshedAt.Value).TotalDays;
        var baseText = Loc.RefreshedFormat(RelativeTime.Humanize(p.LastRefreshedAt, now, Loc.Pt));
        if (days >= settings.StaleHardLimitDays) return baseText + Loc.SuffixCanExpire;
        if (days >= settings.StaleWarningDays) return baseText + Loc.SuffixRenewSoon;
        return baseText;
    }

    private static string ComputeInitials(ProfileMetadata p)
    {
        var source = !string.IsNullOrWhiteSpace(p.Nickname) ? p.Nickname
            : !string.IsNullOrWhiteSpace(p.AccountEmail) ? p.AccountEmail! : "?";
        source = source.Trim();
        if (source.Length == 0) return "?";
        var parts = source.Split([' ', '.', '@', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return (char.ToUpperInvariant(parts[0][0]).ToString() + char.ToUpperInvariant(parts[1][0])).Trim();
        return char.ToUpperInvariant(source[0]).ToString();
    }
}
